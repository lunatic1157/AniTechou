using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AniTechou.Services;

namespace AniTechou
{
    public partial class MainWindow : Window
    {
        private AccountManager _accountManager;
        private bool _isAIPanelVisible = true;

        private double _lastAIPanelWidth = 320;
        public MainWindow()
        {
            InitializeComponent();
            _accountManager = new AccountManager();
            
            _accountManager.AccountSwitched += OnAccountSwitched;

            // 监听分割条拖动，保存宽度并限制最小宽度
            var splitter = this.FindName("AIPanelSplitter") as GridSplitter;
            if (splitter != null)
            {
                splitter.DragCompleted += (s, e) =>
                {
                    double currentWidth = AIPanelColumn.Width.Value;
                    if (currentWidth > 0)
                    {
                        // 限制最小宽度为250
                        if (currentWidth < 250)
                        {
                            AIPanelColumn.Width = new GridLength(250);
                            currentWidth = 250;
                        }
                        _lastAIPanelWidth = currentWidth;
                    }
                };
            }

            // 确保AI面板默认展开
            _isAIPanelVisible = true;
            AIPanelColumn.Width = new GridLength(320);

            // 尝试自动登录
            var config = ConfigManager.Load();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] AutoLogin: {config.AutoLogin}");

            if (config.AutoLogin && _accountManager.TryAutoLogin())
            {
                _currentAccountName = _accountManager.CurrentAccount?.UserName ?? "default";
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Logged in as: {_currentAccountName}");
                LoadMainContent();
            }
            else
            {
                ShowLoginWindow();
            }
        }

        // ========== 窗口控制方法 ==========
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeWindow_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ========== 账号相关 ==========
        private void OnAccountSwitched(object sender, Account account)
        {
            // 可选：更新窗口标题或用户信息
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow(_accountManager);
            loginWindow.ShowDialog();
            
            if (_accountManager.CurrentAccount != null)
            {
                LoadMainContent();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        // ========== 侧边栏功能 ==========
        private void QuickNote_Click(object sender, RoutedEventArgs e)
        {
            // 打开空白笔记编辑器
            var editor = new Views.NoteEditor(_currentAccountName, null, Views.EditorSource.QuickNote);
            editor.NoteSaved += () => RefreshCurrentView();
            editor.NoteCancelled += () => RefreshCurrentView();
            ShowDetailView(editor);
        }

        private void SidebarMenu_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string tag = button.Tag?.ToString() ?? "";
            string type = "all";
            string status = "all";

            switch (tag)
            {
                case "anime": type = "Anime"; break;      // 改成大写
                case "manga": type = "Manga"; break;      // 改成大写
                case "lightnovel": type = "LightNovel"; break;  // 改成大写
                case "game": type = "Game"; break;        // 改成大写
                case "wish": status = "wish"; break;
                case "doing": status = "doing"; break;
                case "done": status = "done"; break;
                case "all": type = "all"; break;
                case "notes":
                    var notesView = new Views.NotesView(_currentAccountName);
                    ShowDetailView(notesView);
                    return;
                case "tags":
                    ContentPlaceholder.Text = $"当前选择：{button.Content}";
                    MainContentArea.Content = null;
                    return;
            }
            
            if (type != "all" || status != "all" || tag == "all")
            {
                string accountName = _accountManager.CurrentAccount?.UserName ?? "default";
                var worksView = new Views.WorksView(accountName, type, status);
                MainContentArea.Content = worksView;
                ContentPlaceholder.Visibility = Visibility.Collapsed;
                MainContentArea.Visibility = Visibility.Visible;
            }
            else
            {
                ContentPlaceholder.Text = $"当前选择：{button.Content}";
                ContentPlaceholder.Visibility = Visibility.Visible;
                MainContentArea.Visibility = Visibility.Collapsed;
            }
        }

        // ========== 顶部栏功能 ==========
        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            ShowProfile();
        }

        private void ShowProfile()
        {
            if (string.IsNullOrEmpty(_currentAccountName)) return;
            var profileView = new Views.ProfileView(_currentAccountName);
            MainContentArea.Content = profileView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        private void RunAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dbPath = DatabaseHelper.GetDatabasePath(_currentAccountName);
                if (!File.Exists(dbPath)) return;

                using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string statsSql = "SELECT count(*) as total, SUM(CASE WHEN CoverPath IS NOT NULL AND CoverPath != '' THEN 1 ELSE 0 END) as with_cover FROM Works;";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(statsSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int total = Convert.ToInt32(reader["total"]);
                            int withCover = Convert.ToInt32(reader["with_cover"]);
                            double coverage = total > 0 ? (withCover * 100.0 / total) : 0;
                            AddMessageToChat($"📊 数据库核查结果：\n总记录数：{total}\n已有关联封面：{withCover}\n覆盖率：{coverage:F2}%", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 核查失败：{ex.Message}", false);
            }
        }

        public void ShowWorksWithTag(string tag)
        {
            if (string.IsNullOrEmpty(_currentAccountName)) return;
            var worksView = new Views.WorksView(_currentAccountName, "all", "all");
            worksView.SetTagFilter(tag);
            MainContentArea.Content = worksView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsView = new Views.SettingsView(_currentAccountName);
            ShowDetailView(settingsView);
        }

        private void ToggleAIPanel_Click(object sender, RoutedEventArgs e)
        {
            if (AIPanelColumn.Width.Value > 0)
            {
                // 收起：保存当前宽度，然后设为0
                _lastAIPanelWidth = AIPanelColumn.Width.Value;
                AIPanelColumn.Width = new GridLength(0);
            }
            else
            {
                // 展开：恢复到上次的宽度
                double targetWidth = _lastAIPanelWidth;
                // 确保展开时不小于最小宽度
                if (targetWidth < 250) targetWidth = 250;
                if (targetWidth > 450) targetWidth = 450;
                AIPanelColumn.Width = new GridLength(targetWidth);
            }
        }

        // ========== AI对话功能 ==========
        private void AddMessageToChat(string message, bool isUser)
        {
            var border = new Border
            {
                Style = isUser ? (Style)FindResource("UserMessageStyle") : (Style)FindResource("AIMessageStyle"),
                Child = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
            };

            ChatMessagesPanel.Children.Add(border);

            // 滚动到底部
            ChatScrollViewer.ScrollToBottom();
        }

        private async void SendAIChat_Click(object sender, RoutedEventArgs e)
        {
            string message = AIChatInputBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            AddMessageToChat(message, true);
            AIChatInputBox.Text = "";

            var loadingMsg = AddLoadingMessage();

            try
            {
                var config = ConfigManager.Load();
                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    AddMessageToChat("请先在设置中配置API Key", false);
                    RemoveLoadingMessage(loadingMsg);
                    return;
                }

                var aiService = new AIService(config.ApiKey, config.ApiUrl, config.Model);
                var response = await aiService.SmartChat(message);

                RemoveLoadingMessage(loadingMsg);

                switch (response.intent)
                {
                    case "WORK_SEARCH":
                        if (response.works != null && response.works.Count > 0)
                        {
                            ShowSearchResults(response.works);
                        }
                        else
                        {
                            AddMessageToChat("未找到相关作品，试试其他关键词？", false);
                        }
                        break;

                case "WORK_UPDATE":
                    if (response.updateInfo != null)
                    {
                        if (response.updateInfo.title.Contains("全部") || response.updateInfo.title.Contains("所有"))
                        {
                            await HandleBatchWorkUpdate(response.updateInfo);
                        }
                        else
                        {
                            await HandleWorkUpdate(response.updateInfo);
                        }
                    }
                    else
                    {
                        AddMessageToChat(response.answer, false);
                    }
                    break;

                    case "INFO_QUERY":
                        AddMessageToChat(response.answer, false);
                        break;

                    default:
                        AddMessageToChat(response.answer, false);
                        break;
                }
            }
            catch (Exception ex)
            {
                RemoveLoadingMessage(loadingMsg);
                AddMessageToChat($"抱歉，出了点问题：{ex.Message}", false);
            }
        }

        private Border AddLoadingMessage()
        {
            var loadingBorder = new Border
            {
                Style = (Style)FindResource("AIMessageStyle"),
                Child = new TextBlock
                {
                    Text = "🤖 正在思考...",
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(127, 110, 90))
                }
            };
            ChatMessagesPanel.Children.Add(loadingBorder);
            ChatScrollViewer.ScrollToBottom();
            return loadingBorder;
        }

        private void RemoveLoadingMessage(Border loadingMsg)
        {
            ChatMessagesPanel.Children.Remove(loadingMsg);
        }

        private string GetTypeDisplay(string type)
        {
            return type switch
            {
                "Anime" => "动画",
                "Manga" => "漫画",
                "LightNovel" => "轻小说",
                "Game" => "游戏",
                _ => type
            };
        }

        private async Task HandleBatchWorkUpdate(AIUpdateInfo info)
        {
            var workService = new WorkService(_currentAccountName);
            // 获取所有作品
            var allWorks = await Task.Run(() => workService.GetWorksAsync("all", "all"));

            if (allWorks.Count == 0)
            {
                AddMessageToChat("❌ 你的列表中没有任何作品", false);
                return;
            }

            int successCount = 0;
            var updates = info.updates;

            foreach (var workItem in allWorks)
            {
                // 为每个作品补全信息
                bool updated = false;
                foreach (var kvp in updates)
                {
                    string field = kvp.Key.ToLower();
                    string value = kvp.Value;

                    switch (field)
                    {
                        case "season":
                            if (workService.UpdateWorkSeason(workItem.Id, value)) updated = true;
                            break;
                        case "sourcetype":
                            if (workService.UpdateWorkSourceType(workItem.Id, value)) updated = true;
                            break;
                    }
                }
                if (updated) successCount++;
            }

            AddMessageToChat($"✅ 批量更新完成，共处理 {allWorks.Count} 部作品，成功更新 {successCount} 部", false);
            RefreshCurrentView();
        }

        private async Task HandleWorkUpdate(AIUpdateInfo info)
        {
            if (string.IsNullOrEmpty(info.title)) return;

            var workService = new WorkService(_currentAccountName);
            var works = await workService.SearchWorksByNameAsync(info.title);

            if (works.Count == 0)
            {
                AddMessageToChat($"❌ 未能在你的列表中找到《{info.title}》", false);
                return;
            }

            var work = works[0];
            var updates = info.updates;
            if (updates == null || updates.Count == 0)
            {
                AddMessageToChat($"❓ 未能提取到有效的更新信息", false);
                return;
            }

            List<string> updatedFields = new List<string>();

            foreach (var kvp in updates)
            {
                string field = kvp.Key.ToLower();
                string value = kvp.Value;
                bool success = false;

                switch (field)
                {
                    case "status":
                        string status = value.ToLower() switch
                        {
                            "想看" or "wish" => "wish",
                            "在看" or "doing" => "doing",
                            "看过" or "done" => "done",
                            _ => "wish"
                        };
                        success = workService.UpdateWorkStatus(work.Id, status);
                        if (success) updatedFields.Add("状态");
                        break;

                    case "progress":
                        success = workService.UpdateWorkProgress(work.Id, value);
                        if (success) updatedFields.Add("进度");
                        break;

                    case "rating":
                        if (int.TryParse(value, out int rating))
                        {
                            success = workService.UpdateWorkRating(work.Id, rating);
                            if (success) updatedFields.Add("评分");
                        }
                        break;

                    case "episodes":
                        success = workService.UpdateWorkEpisodes(work.Id, value);
                        if (success) updatedFields.Add("总集数");
                        break;

                    case "season":
                        success = workService.UpdateWorkSeason(work.Id, value);
                        if (success) updatedFields.Add("季度");
                        break;

                    case "sourcetype":
                        success = workService.UpdateWorkSourceType(work.Id, value);
                        if (success) updatedFields.Add("原作类型");
                        break;

                    case "synopsis":
                        success = workService.UpdateWorkSynopsis(work.Id, value);
                        if (success) updatedFields.Add("简介");
                        break;

                    case "coverurl":
                        if (!string.IsNullOrEmpty(value))
                        {
                            _ = Task.Run(async () =>
                            {
                                string localPath = await workService.DownloadAndSaveCoverAsync(value, work.Id);
                                if (!string.IsNullOrEmpty(localPath))
                                {
                                    Dispatcher.Invoke(() => AddMessageToChat($"🖼️ 《{work.Title}》封面已更新并保存", false));
                                    RefreshCurrentView();
                                }
                            });
                            updatedFields.Add("封面链接");
                        }
                        break;
                }
            }

            if (updatedFields.Count > 0)
            {
                AddMessageToChat($"✅ 已更新《{work.Title}》的：{string.Join("、", updatedFields)}", false);
                RefreshCurrentView();
            }
            else
            {
                AddMessageToChat($"❌ 更新《{work.Title}》失败", false);
            }
        }

        private void ShowSearchResults(List<AIWorkSearchResult> works)
        {
            // 添加AI回复
            var resultBorder = new Border
            {
                Style = (Style)FindResource("AIMessageStyle"),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel();

            // 推荐语
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"🎯 为你推荐以下作品：",
                FontSize = 12,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (var work in works)
            {
                var workBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 244, 233)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var workStack = new StackPanel();

                // 标题和类型
                var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
                headerStack.Children.Add(new TextBlock
                {
                    Text = $"📖 {work.title}",
                    FontSize = 13,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61))
                });
                headerStack.Children.Add(new TextBlock
                {
                    Text = $" [{GetTypeDisplay(work.type)}]",
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                    Margin = new Thickness(5, 2, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                workStack.Children.Add(headerStack);

                // 详细信息
                string info = "";
                if (!string.IsNullOrEmpty(work.year)) info += $"{work.year}年 ";
                if (!string.IsNullOrEmpty(work.company)) info += $"· {work.company} ";
                if (!string.IsNullOrEmpty(work.episodes)) info += $"· {work.episodes}集";
                if (!string.IsNullOrEmpty(info))
                {
                    workStack.Children.Add(new TextBlock
                    {
                        Text = info,
                        FontSize = 10,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                // 简介
                if (!string.IsNullOrEmpty(work.synopsis))
                {
                    workStack.Children.Add(new TextBlock
                    {
                        Text = work.synopsis.Length > 80 ? work.synopsis.Substring(0, 80) + "..." : work.synopsis,
                        FontSize = 10,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                }

                // 标签
                if (work.tags != null && work.tags.Count > 0)
                {
                    var tagsPanel = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                    foreach (var tag in work.tags)
                    {
                        tagsPanel.Children.Add(new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 213, 192)),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 4, 2),
                            Child = new TextBlock
                            {
                                Text = $"#{tag}",
                                FontSize = 9,
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61))
                            }
                        });
                    }
                    workStack.Children.Add(tagsPanel);
                }

                // 添加按钮
                var addBtn = new Button
                {
                    Content = "+ 添加到列表",
                    Width = 90,
                    Height = 26,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61)),
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 8, 0, 0),
                    Tag = work,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                addBtn.Click += async (s, args) => await AddWorkFromSearch(s, args);
                workStack.Children.Add(addBtn);

                workBorder.Child = workStack;
                stackPanel.Children.Add(workBorder);
            }

            // 推荐其他作品
            stackPanel.Children.Add(new TextBlock
            {
                Text = "✨ 如果喜欢这些，还可以试试问：'更多治愈系作品'",
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                Margin = new Thickness(0, 5, 0, 0)
            });

            resultBorder.Child = stackPanel;
            ChatMessagesPanel.Children.Add(resultBorder);
            ChatScrollViewer.ScrollToBottom();
        }

        private async Task AddWorkFromSearch(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var work = btn?.Tag as AIWorkSearchResult;
            if (work == null) return;

            System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] 开始添加作品: title={work.title}, type={work.type}, year={work.year}");
            System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] AccountName: {_currentAccountName}");

            try
            {
                // 转换类型
                string typeEn = work.type switch
                {
                    "动画" or "Anime" => "Anime",
                    "漫画" or "Manga" => "Manga",
                    "轻小说" or "LightNovel" => "LightNovel",
                    "游戏" or "Game" => "Game",
                    _ => "Anime"
                };

                System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] 转换后的type: {typeEn}");

                if (string.IsNullOrEmpty(work.title))
                {
                    AddMessageToChat("❌ 作品标题为空，无法添加", false);
                    return;
                }

                var workService = new WorkService(_currentAccountName);

                // 确保所有参数完整（Rating 为 0 表示未评分，数据库允许 0-10）
                int workId = workService.AddWork(
                    work.title,                    // 标题
                    work.originalTitle ?? "",      // 原名
                    typeEn,                        // 类型
                    work.company ?? "",            // 制作
                    work.year ?? "",               // 年份
                    work.season ?? "",             // 季度
                    work.sourceType ?? "",         // 原作类型
                    work.episodes ?? "",           // 集数
                    "",                            // 进度
                    "wish",                        // 状态
                    0,                             // 评分（0存入NULL）
                    work.synopsis ?? "",           // 简介
                    ""                             // 封面先留空，下载后再更新
                );

                System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] AddWork返回: workId={workId}");

                if (workId > 0)
                {
                    AddMessageToChat($"✅ 已添加《{work.title}》到列表", false);

                    // 异步下载封面
                    if (!string.IsNullOrEmpty(work.coverUrl))
                    {
                        _ = Task.Run(async () =>
                        {
                            string localPath = await workService.DownloadAndSaveCoverAsync(work.coverUrl, workId);
                            if (!string.IsNullOrEmpty(localPath))
                            {
                                Dispatcher.Invoke(() => AddMessageToChat($"🖼️ 《{work.title}》封面已自动下载并保存", false));
                            }
                        });
                    }

                    // 添加标签
                    if (work.tags != null && work.tags.Count > 0)
                    {
                        foreach (var tag in work.tags)
                        {
                            workService.AddWorkTag(workId, tag);
                        }
                        AddMessageToChat($"🏷️ 已添加标签：{string.Join("、", work.tags)}", false);
                    }

                    RefreshCurrentView();
                }
                else
                {
                    AddMessageToChat($"❌ 添加《{work.title}》失败，请手动添加", false);
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 添加失败：{ex.Message}", false);
                System.Diagnostics.Debug.WriteLine($"添加失败详情: {ex}");

                // 尝试同步添加
                try
                {
                    var ws = new WorkService(_currentAccountName);
                    int id = ws.AddWork(
                        work.title ?? "未知",
                        work.originalTitle ?? "",
                        "Anime",
                        work.company ?? "",
                        work.year ?? "",
                        "", "", "", "", "wish", 0, "", "");
                    if (id > 0)
                    {
                        AddMessageToChat($"✅ 备用方式添加成功：《{work.title}》", false);
                        RefreshCurrentView();
                    }
                }
                catch { }
            }
        }

        private void AIBatchAdd_Click(object sender, RoutedEventArgs e)
        {
            var batchView = new Views.AIBatchAddView(_currentAccountName);
            ShowDetailView(batchView);
        }

        private void AIChatInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendAIChat_Click(sender, e);
            }
        }

        private string GetAIReply(string userMessage)
        {
            // 模拟回复，后续替换为真实的AI API调用
            if (userMessage.Contains("推荐") || userMessage.Contains("治愈"))
            {
                return "推荐几部治愈系作品：\n• 葬送的芙莉莲\n• 夏目友人帐\n• 摇曳露营△\n• 妖精森林的小不点";
            }
            else if (userMessage.Contains("芙莉莲"))
            {
                return "《葬送的芙莉莲》是由MADHOUSE制作的动画，讲述了勇者一行击败魔王后，精灵魔法使芙莉莲重新踏上旅途的故事。这是一部非常治愈的作品，共28集。";
            }
            else
            {
                return $"收到你的问题：\"{userMessage}\"\n\n我正在学习中，后续会接入AI服务为你提供更准确的回答。你可以尝试问一些关于动漫推荐的问题。";
            }
        }

        // 添加成员变量
        private string _currentAccountName;

        // 在 LoadMainContent 中保存当前账号名
        private void LoadMainContent()
        {
            _currentAccountName = _accountManager.CurrentAccount?.UserName ?? "default";
            var worksView = new Views.WorksView(_currentAccountName, "all", "all");
            MainContentArea.Content = worksView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        // 显示添加作品选择界面
        public void ShowAddWorkOptions()
        {
            var addWorkView = new Views.AddWorkChoiceView();
            addWorkView.AIAddRequested += () =>
            {
                // 展开AI面板
                if (!_isAIPanelVisible)
                {
                    ToggleAIPanel_Click(AIAssistantButton, null);
                }
                AIChatInputBox.Focus();
                AIChatInputBox.Text = "";
                AIChatInputBox.ToolTip = "输入作品名称，AI帮你搜索";

                // 返回主内容
                RefreshCurrentView();
            };
            addWorkView.ManualAddRequested += () =>
            {
                var addForm = new Views.AddWorkForm(_currentAccountName);
                addForm.WorkAdded += () => RefreshCurrentView();
                MainContentArea.Content = addForm;
                ContentPlaceholder.Visibility = Visibility.Collapsed;
                MainContentArea.Visibility = Visibility.Visible;
            };

            MainContentArea.Content = addWorkView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        // 刷新当前视图
        public void RefreshCurrentView()
        {
            var worksView = new Views.WorksView(_currentAccountName, "all", "all");
            MainContentArea.Content = worksView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        // 显示笔记列表
        public void ShowNotesView()
        {
            var notesView = new Views.NotesView(_currentAccountName);
            MainContentArea.Content = notesView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        public void ShowDetailView(UserControl detailView)
        {
            MainContentArea.Content = detailView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        public void Logout()
        {
            // 清除当前账号
            _accountManager.Logout();

            // 显示登录窗口
            var loginWindow = new LoginWindow(_accountManager);
            loginWindow.ShowDialog();

            if (_accountManager.CurrentAccount != null)
            {
                _currentAccountName = _accountManager.CurrentAccount.UserName;
                LoadMainContent();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}