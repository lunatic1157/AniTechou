using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AniTechou.Services;
using AniTechou.Utilities;

namespace AniTechou
{
    public partial class MainWindow : Window
    {
        public AccountManager _accountManager;
        private bool _isAIPanelVisible = true;
        private readonly Dictionary<string, Button> _topThemeButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private double _lastAIPanelWidth = 320;
        public MainWindow()
        {
            InitializeComponent();
            _accountManager = new AccountManager();
            InitializeTopThemeSwitcher();
            StateChanged += (s, e) => UpdateWindowControlGlyph();
            Loaded += (s, e) => UpdateWindowControlGlyph();
            
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
            ApplyTopThemeSelection(ThemeManager.NormalizeAccent(config.ThemeAccent));
            ApplyTopThemeModeSelection(ThemeManager.NormalizeMode(config.ThemeMode));
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
            UpdateWindowControlGlyph();
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
            SetSidebarSelection(QuickNoteButton);
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
            SetSidebarSelection(button);

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
                    
                    // 1. 统计基本覆盖率
                    string statsSql = @"
                        SELECT 
                            count(*) as total, 
                            SUM(CASE WHEN CoverPath IS NOT NULL AND CoverPath != '' THEN 1 ELSE 0 END) as with_cover,
                            SUM(CASE WHEN Season IS NOT NULL AND Season != '' THEN 1 ELSE 0 END) as with_season,
                            SUM(CASE WHEN Synopsis IS NOT NULL AND Synopsis != '' THEN 1 ELSE 0 END) as with_synopsis
                        FROM Works;";
                    
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(statsSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int total = Convert.ToInt32(reader["total"]);
                            int withCover = Convert.ToInt32(reader["with_cover"]);
                            int withSeason = Convert.ToInt32(reader["with_season"]);
                            int withSynopsis = Convert.ToInt32(reader["with_synopsis"]);
                            
                            string report = $"📊 深度核查报告：\n" +
                                           $"------------------\n" +
                                           $"总作品数：{total}\n" +
                                           $"🖼️ 封面覆盖：{withCover} ({ (total>0?withCover*100/total:0) }%)\n" +
                                           $"📅 季度信息：{withSeason} ({ (total>0?withSeason*100/total:0) }%)\n" +
                                           $"📝 简介补全：{withSynopsis} ({ (total>0?withSynopsis*100/total:0) }%)";
                            
                            AddMessageToChat(report, false);

                            if (withCover < total)
                            {
                                AddMessageToChat("💡 提示：检测到部分作品缺失封面，你可以对我说“完善所有作品封面”来尝试自动补全。", false);
                            }
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

        private void ThemeQuickButton_Click(object sender, RoutedEventArgs e)
        {
            TopThemePopup.IsOpen = !TopThemePopup.IsOpen;
        }

        private void TopThemeModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Primitives.ToggleButton button || button.Tag is not string modeKey)
            {
                return;
            }

            var config = ConfigManager.Load();
            config.ThemeMode = ThemeManager.NormalizeMode(modeKey);
            ConfigManager.Save(config);
            ThemeManager.ApplyTheme(config.ThemeAccent, config.ThemeMode);
            ApplyTopThemeModeSelection(config.ThemeMode);
            ApplyTopThemeSelection(config.ThemeAccent);
        }

        private void ToggleAIPanel_Click(object sender, RoutedEventArgs e)
        {
            _isAIPanelVisible = !_isAIPanelVisible;

            DoubleAnimation animation = new DoubleAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(300);
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (_isAIPanelVisible)
            {
                animation.From = 0;
                animation.To = _lastAIPanelWidth;
                var splitter = this.FindName("AIPanelSplitter") as GridSplitter;
                if (splitter != null) splitter.Visibility = Visibility.Visible;
            }
            else
            {
                _lastAIPanelWidth = AIPanelColumn.Width.Value;
                animation.From = _lastAIPanelWidth;
                animation.To = 0;
                var splitter = this.FindName("AIPanelSplitter") as GridSplitter;
                if (splitter != null) splitter.Visibility = Visibility.Collapsed;
            }

            // 使用 Storyboard 对 GridColumn 的 Width 进行动画处理
            Storyboard sb = new Storyboard();
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath("AIPanelWidth"));
            sb.Children.Add(animation);
            sb.Begin();
        }

        // 定义一个依赖属性用于动画化列宽
        public static readonly DependencyProperty AIPanelWidthProperty =
            DependencyProperty.Register("AIPanelWidth", typeof(double), typeof(MainWindow),
                new PropertyMetadata(320.0, OnAIPanelWidthChanged));

        public double AIPanelWidth
        {
            get { return (double)GetValue(AIPanelWidthProperty); }
            set { SetValue(AIPanelWidthProperty, value); }
        }

        private static void OnAIPanelWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window)
            {
                window.AIPanelColumn.Width = new GridLength((double)e.NewValue);
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
                    FontSize = 12,
                    Foreground = ThemeManager.GetBrush("TextPrimaryBrush")
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

                // 优化：仅在涉及本地操作时才注入列表上下文
                string collectionSummary = "";
                string lowerMsg = message.ToLower();
                bool isCollectionRelated = lowerMsg.Contains("我") || 
                                         lowerMsg.Contains("列表") || 
                                         lowerMsg.Contains("全部") || 
                                         lowerMsg.Contains("所有") || 
                                         lowerMsg.Contains("完善") || 
                                         lowerMsg.Contains("补全") || 
                                         lowerMsg.Contains("删除") || 
                                         lowerMsg.Contains("更新");

                if (isCollectionRelated)
                {
                    var workService = new WorkService(_currentAccountName);
                    var allWorks = await Task.Run(() => workService.GetWorksAsync("all", "all"));
                    // 只发送标题，极大减少 Token 消耗
                    collectionSummary = string.Join(", ", allWorks.Select(w => $"《{w.Title}》"));
                }

                var aiService = new AIService(config.ApiKey, config.ApiUrl, config.Model);
                var response = await aiService.SmartChat(message, collectionSummary);

                RemoveLoadingMessage(loadingMsg);

                switch (response.intent)
                {
                    case "GENERAL_CHAT":
                    case "INFO_QUERY":
                    case "WORK_SEARCH":
                        AddMessageToChat(response.answer, false);
                        if (response.works != null && response.works.Count > 0)
                        {
                            ShowSearchResults(response.works);
                        }
                        break;

                    case "WORK_UPDATE":
                        if (response.updateInfo != null)
                        {
                            if (response.updateInfo.action == "TAG_UNIFY")
                            {
                                await HandleTagUnify(response.updateInfo);
                            }
                            else if (response.updateInfo.isBatchUpdate)
                            {
                                // 只有明确的布尔值 isBatchUpdate 为 true 才执行全库更新
                                AddMessageToChat(response.answer, false);
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

                    case "WORK_DELETE":
                        if (response.updateInfo != null && !string.IsNullOrEmpty(response.updateInfo.title))
                        {
                            await HandleWorkDelete(response.updateInfo.title);
                        }
                        else
                        {
                            AddMessageToChat(response.answer, false);
                        }
                        break;

                    case "NOTE_CREATE":
                        if (response.noteInfo != null)
                        {
                            await HandleNoteCreate(response.noteInfo);
                        }
                        else
                        {
                            AddMessageToChat(response.answer, false);
                        }
                        break;

                    case "NOTE_SEARCH":
                        if (response.noteInfo != null && !string.IsNullOrEmpty(response.noteInfo.searchTerm))
                        {
                            await HandleNoteSearch(response.noteInfo.searchTerm);
                        }
                        else
                        {
                            AddMessageToChat(response.answer, false);
                        }
                        break;

                    case "NOTE_UPDATE":
                        if (response.noteInfo != null)
                        {
                            await HandleNoteUpdate(response.noteInfo);
                        }
                        else
                        {
                            AddMessageToChat(response.answer, false);
                        }
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

        private System.Threading.CancellationTokenSource _batchUpdateCancellationTokenSource;

        private async Task HandleBatchWorkUpdate(AIUpdateInfo info)
        {
            try
            {
                var workService = new WorkService(_currentAccountName);
                var aiService = new AIService();
                
                // 获取所有作品
                var allWorks = await Task.Run(() => workService.GetWorksAsync("all", "all"));

                if (allWorks.Count == 0)
                {
                    AddMessageToChat("❌ 你的列表中没有任何作品", false);
                    return;
                }

                // 取消之前的任务（如果存在）
                _batchUpdateCancellationTokenSource?.Cancel();
                _batchUpdateCancellationTokenSource = new System.Threading.CancellationTokenSource();
                var token = _batchUpdateCancellationTokenSource.Token;

                // 创建包含停止按钮的 UI
                var batchControlPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 10) };
                batchControlPanel.Children.Add(new TextBlock
                {
                    Text = $"⏳ 启动全量补完计划，共计 {allWorks.Count} 部作品...",
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                
                var stopBtn = new Button
                {
                    Content = "⏹ 停止更新",
                    Padding = new Thickness(10, 4, 10, 4),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 80, 80)),
                    Foreground = System.Windows.Media.Brushes.White,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                stopBtn.Click += (s, e) => 
                {
                    _batchUpdateCancellationTokenSource?.Cancel();
                    stopBtn.IsEnabled = false;
                    stopBtn.Content = "已发送停止指令...";
                };
                batchControlPanel.Children.Add(stopBtn);

                var msgBorder = new Border
                {
                    Style = (Style)FindResource("AIMessageStyle"),
                    Child = batchControlPanel
                };
                ChatMessagesPanel.Children.Add(msgBorder);
                ChatScrollViewer.ScrollToBottom();

                int successCount = 0;
                int current = 0;

                foreach (var workItem in allWorks)
                {
                    if (token.IsCancellationRequested)
                    {
                        AddMessageToChat("🛑 全量更新已被用户手动中止。", false);
                        break;
                    }

                    current++;
                    try
                    {
                        // 1. 请求 AI 获取该作品的增强信息
                        var enhancedInfo = await aiService.GetEnhancedWorkInfo(workItem.Title, "", "");
                        
                        if (enhancedInfo != null)
                        {
                            bool updated = false;
                            
                            // 2. 逐字段补全 (只在原字段为空或未知时才覆盖)
                            var existingWork = workService.GetWorkById(workItem.Id);
                            
                            if (string.IsNullOrEmpty(existingWork.Season) && !string.IsNullOrEmpty(enhancedInfo.season))
                            {
                                if (workService.UpdateWorkSeason(workItem.Id, enhancedInfo.season)) updated = true;
                            }
                            
                            if (string.IsNullOrEmpty(existingWork.SourceType) || existingWork.SourceType == "未知")
                            {
                                var normalizedSourceType = WorkDataRules.NormalizeSourceType(enhancedInfo.sourceType);
                                var currentType = WorkDataRules.NormalizeTypeToEnglish(existingWork.Type);
                                if (!string.IsNullOrEmpty(normalizedSourceType) && !(currentType == "Game" && normalizedSourceType == "游戏改"))
                                {
                                    if (workService.UpdateWorkSourceType(workItem.Id, normalizedSourceType)) updated = true;
                                }
                            }
                            
                            if (string.IsNullOrEmpty(existingWork.Synopsis) && !string.IsNullOrEmpty(enhancedInfo.synopsis))
                            {
                                if (workService.UpdateWorkSynopsis(workItem.Id, enhancedInfo.synopsis)) updated = true;
                            }
                            
                            if (string.IsNullOrEmpty(existingWork.EpisodesVolumes) && !string.IsNullOrEmpty(enhancedInfo.episodes))
                            {
                                if (workService.UpdateWorkEpisodes(workItem.Id, enhancedInfo.episodes)) updated = true;
                            }

                            if (string.IsNullOrEmpty(existingWork.OriginalWork) && !string.IsNullOrEmpty(enhancedInfo.originalWork))
                            {
                                if (workService.UpdateWorkOriginalWork(workItem.Id, enhancedInfo.originalWork)) updated = true;
                            }

                            // 3. 异步下载封面（如果原本没封面）
                            if (string.IsNullOrEmpty(workItem.CoverPath) && !string.IsNullOrEmpty(enhancedInfo.coverUrl))
                            {
                                var currentId = workItem.Id;
                                var currentUrl = enhancedInfo.coverUrl;
                                _ = Task.Run(async () =>
                                {
                                    string localPath = await workService.DownloadAndSaveCoverAsync(currentUrl, currentId);
                                    if (!string.IsNullOrEmpty(localPath))
                                    {
                                        Dispatcher.Invoke(() => RefreshCurrentView());
                                    }
                                });
                            }

                            if (updated) successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"批量补全作品《{workItem.Title}》失败: {ex.Message}");
                    }

                    // 实时进度反馈
                    if (current % 3 == 0 || current == allWorks.Count)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            AddMessageToChat($"🔄 进度反馈：已处理 {current}/{allWorks.Count}...", false);
                        }
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    if (successCount > 0)
                    {
                        AddMessageToChat($"✅ 批量补完完成！成功完善了 {successCount} 部作品的信息。", false);
                        RefreshCurrentView();
                    }
                    else
                    {
                        AddMessageToChat($"ℹ️ 批量处理结束。未发现需要更新的信息或更新未成功。", false);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 批量处理发生错误：{ex.Message}", false);
            }
        }

    private async Task HandleWorkDelete(string title)
        {
            var workService = new WorkService(_currentAccountName);
            var works = await workService.SearchWorksByNameAsync(title);

            if (works.Count == 0)
            {
                AddMessageToChat($"❌ 未能在你的列表中找到《{title}》", false);
                return;
            }

            var work = works[0];
            var result = MessageBox.Show($"AI 想要帮你删除《{work.Title}》，确定吗？", 
                                       "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (workService.DeleteWork(work.Id))
                {
                    AddMessageToChat($"✅ 已成功从你的列表中删除《{work.Title}》", false);
                    RefreshCurrentView();
                }
                else
                {
                    AddMessageToChat($"❌ 删除《{work.Title}》失败，请重试", false);
                }
            }
            else
            {
                AddMessageToChat($"👌 已取消删除《{work.Title}》的操作", false);
            }
        }

        private async Task HandleTagUnify(AIUpdateInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.targetTag) || string.IsNullOrWhiteSpace(info.newTag))
            {
                AddMessageToChat("❌ AI未能正确识别要统一的标签，请重试。", false);
                return;
            }

            try
            {
                var workService = new WorkService(_currentAccountName);
                int count = await Task.Run(() => workService.UnifyTags(info.targetTag, info.newTag));

                if (count > 0)
                {
                    AddMessageToChat($"✅ 操作成功！已将 {count} 个作品的标签‘{info.targetTag}’统一为‘{info.newTag}’。", false);
                    RefreshCurrentView(); // 刷新视图以更新标签列表
                }
                else
                {
                    AddMessageToChat($"ℹ️ 未找到包含标签‘{info.targetTag}’的作品，无需操作。", false);
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 统一标签时发生错误：{ex.Message}", false);
            }
        }

        private async Task HandleNoteCreate(AINoteInfo info)
        {
            try
            {
                var workService = new WorkService(_currentAccountName);
                var note = new WorkService.NoteInfo
                {
                    Title = string.IsNullOrEmpty(info.title) ? $"AI 快速笔记 {DateTime.Now:MM-dd HH:mm}" : info.title,
                    Content = info.content,
                    Tags = info.tags ?? new List<string>()
                };

                // 关联作品
                if (info.relatedWorks != null && info.relatedWorks.Count > 0)
                {
                    foreach (var workTitle in info.relatedWorks)
                    {
                        var works = await workService.SearchWorksByNameAsync(workTitle);
                        if (works.Count > 0)
                        {
                            note.WorkIds.Add(works[0].Id);
                        }
                    }
                }

                int noteId = workService.SaveNote(note);
                if (noteId > 0)
                {
                    AddMessageToChat($"📝 已为你记录笔记：\n【{note.Title}】\n{note.Content}", false);
                    if (note.WorkIds.Count > 0)
                    {
                        AddMessageToChat($"🔗 已自动关联作品：{string.Join("、", info.relatedWorks)}", false);
                    }
                }
                else
                {
                    AddMessageToChat("❌ 笔记保存失败，请重试", false);
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 记事出错：{ex.Message}", false);
            }
        }

        private async Task HandleNoteSearch(string searchTerm)
        {
            try
            {
                var workService = new WorkService(_currentAccountName);
                var allNotes = await Task.Run(() => workService.GetAllNotes());
                
                var results = allNotes.Where(n => 
                    n.Title.Contains(searchTerm) || 
                    n.Content.Contains(searchTerm) || 
                    n.Tags.Contains(searchTerm)
                ).ToList();

                if (results.Count > 0)
                {
                    AddMessageToChat($"🔍 找到 {results.Count} 条相关笔记：", false);
                    foreach (var note in results.Take(3))
                    {
                        string noteInfo = $"【{note.DisplayTitle}】(ID: {note.Id})\n{note.Preview}";
                        if (!string.IsNullOrEmpty(note.Tags)) noteInfo += $"\n🏷️ {note.Tags}";
                        AddMessageToChat(noteInfo, false);
                    }
                    if (results.Count > 3)
                    {
                        AddMessageToChat("... 还有更多笔记，请点击侧边栏 [笔记] 查看全部。", false);
                    }
                }
                else
                {
                    AddMessageToChat($"❌ 未能找到包含“{searchTerm}”的笔记", false);
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 搜索笔记出错：{ex.Message}", false);
            }
        }

        private async Task HandleNoteUpdate(AINoteInfo info)
        {
            try
            {
                var workService = new WorkService(_currentAccountName);
                
                // 1. 尝试找到目标笔记
                var allNotes = await Task.Run(() => workService.GetAllNotes());
                WorkService.NoteListItem targetNoteListInfo = null;

                if (info.noteId > 0)
                {
                    targetNoteListInfo = allNotes.FirstOrDefault(n => n.Id == info.noteId);
                }
                else if (!string.IsNullOrEmpty(info.searchTerm))
                {
                    // 根据关键词寻找最匹配的一条笔记
                    targetNoteListInfo = allNotes.FirstOrDefault(n => n.Title.Contains(info.searchTerm) || n.Content.Contains(info.searchTerm));
                }

                if (targetNoteListInfo == null)
                {
                    AddMessageToChat($"❌ 未找到需要更新的笔记（关键词：{info.searchTerm}）。请提供更准确的标题或内容片段。", false);
                    return;
                }

                // 需要转换回 NoteInfo 才能保存
                var targetNote = new WorkService.NoteInfo
                {
                    Id = targetNoteListInfo.Id,
                    Title = targetNoteListInfo.Title,
                    Content = targetNoteListInfo.Content,
                    Tags = targetNoteListInfo.Tags != null ? targetNoteListInfo.Tags.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>()
                };

                // 2. 更新信息
                bool isUpdated = false;
                if (!string.IsNullOrEmpty(info.title) && targetNote.Title != info.title)
                {
                    targetNote.Title = info.title;
                    isUpdated = true;
                }
                if (!string.IsNullOrEmpty(info.content) && targetNote.Content != info.content)
                {
                    // 追加或覆盖？这里采取智能覆盖策略：如果AI返回了完整新内容则覆盖
                    targetNote.Content = info.content; 
                    isUpdated = true;
                }
                
                // 3. 保存更新
                if (isUpdated)
                {
                    int noteId = workService.SaveNote(targetNote);
                    if (noteId > 0)
                    {
                        AddMessageToChat($"✅ 笔记已更新：\n【{targetNote.Title}】\n{targetNote.Content}", false);
                    }
                    else
                    {
                        AddMessageToChat("❌ 笔记更新保存失败。", false);
                    }
                }
                else
                {
                    AddMessageToChat("ℹ️ 笔记内容没有发生变化。", false);
                }
            }
            catch (Exception ex)
            {
                AddMessageToChat($"❌ 更新笔记出错：{ex.Message}", false);
            }
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

            // 如果有多个同名作品，尝试优先匹配类型一致的
            var requestedType = WorkDataRules.NormalizeTypeToEnglish(info.type);
            var work = works[0];
            if (!string.IsNullOrEmpty(requestedType))
            {
                var matchedWork = works.FirstOrDefault(w => string.Equals(WorkDataRules.NormalizeTypeToEnglish(w.Type), requestedType, StringComparison.OrdinalIgnoreCase));
                if (matchedWork != null)
                {
                    work = matchedWork;
                }
                else if (works.Count > 1)
                {
                    AddMessageToChat($"❌ 找到了多部《{info.title}》，但没有找到类型为 {GetTypeDisplay(requestedType)} 的那一部，请更精确地说明要更新哪一项。", false);
                    return;
                }
            }

            var updates = info.updates;
            if (updates == null || updates.Count == 0)
            {
                // 如果没有 updates，但是传了 coverUrl 或 bangumiId，或者这是一个“只更新标签”的请求
                if (!string.IsNullOrEmpty(info.targetTag) || !string.IsNullOrEmpty(info.newTag))
                {
                    // 这是一个标签修改，不应该在这里处理，但如果进来了，直接跳过
                    return;
                }
                
                // 如果是单纯的 AI 找不到信息，或者它懒得写 updates
                AddMessageToChat($"❓ 未能提取到《{info.title}》有效的更新信息，AI 可能认为当前信息已是最新", false);
                return;
            }

            var detailedWork = workService.GetWorkById(work.Id);
            List<string> updatedFields = new List<string>();
            bool forceCoverUpdate = updates.Keys.Any(k => string.Equals(k, "force_cover_update", StringComparison.OrdinalIgnoreCase));
            bool forceSourceTypeUpdate = updates.Keys.Any(k => string.Equals(k, "force_source_type_update", StringComparison.OrdinalIgnoreCase));

            foreach (var kvp in updates)
            {
                string field = kvp.Key.ToLower();
                string value = (kvp.Value ?? "").Trim();
                bool success = false;
                if (string.IsNullOrEmpty(value)) continue;

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
                        var normalizedSourceType = WorkDataRules.NormalizeSourceType(value);
                        if (!string.IsNullOrEmpty(normalizedSourceType))
                        {
                            var currentType = WorkDataRules.NormalizeTypeToEnglish(detailedWork?.Type ?? work.Type);
                            var currentSourceType = detailedWork?.SourceType ?? "";
                            var canUpdateSourceType =
                                string.IsNullOrEmpty(currentSourceType) ||
                                string.Equals(currentSourceType, "未知", StringComparison.OrdinalIgnoreCase) ||
                                forceSourceTypeUpdate;

                            if (canUpdateSourceType && !(currentType == "Game" && normalizedSourceType == "游戏改"))
                            {
                                success = workService.UpdateWorkSourceType(work.Id, normalizedSourceType);
                                if (success) updatedFields.Add("原作类型");
                            }
                        }
                        break;

                    case "synopsis":
                        success = workService.UpdateWorkSynopsis(work.Id, value);
                        if (success) updatedFields.Add("简介");
                        break;

                    case "company":
                        success = workService.UpdateWorkCompany(work.Id, value);
                        if (success) updatedFields.Add("制作公司");
                        break;
                        
                    case "author":
                        success = workService.UpdateWorkAuthor(work.Id, value);
                        if (success) updatedFields.Add("作者");
                        break;
                        
                    case "originalwork":
                        success = workService.UpdateWorkOriginalWork(work.Id, value);
                        if (success) updatedFields.Add("原作");
                        break;

                    case "coverurl":
                        if (!string.IsNullOrEmpty(value))
                        {
                            // 如果原有封面不为空，并且用户没有在 prompt 里明确要求“更新封面”，则跳过
                            if (!string.IsNullOrEmpty(work.CoverPath) && !forceCoverUpdate)
                            {
                                break;
                            }

                            string targetUrl = value;
                            var bgmId = updates.FirstOrDefault(p => string.Equals(p.Key, "bangumiId", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Key, "bangumiid", StringComparison.OrdinalIgnoreCase)).Value;
                            if (!string.IsNullOrEmpty(bgmId))
                            {
                                targetUrl = $"bgm_id:{bgmId}|{value}";
                            }

                            _ = Task.Run(async () =>
                            {
                                // 如果 AI 连 ID 都不给，或者给的链接太扯，我们直接传 work.Title 让 WorkService 去搜
                                string searchParam = targetUrl.Contains("lain.bgm.tv") && !targetUrl.Contains("bgm_id:") 
                                    ? $"search_title:{work.Title}" // 强制使用标题搜索模式
                                    : targetUrl;

                                string localPath = await workService.DownloadAndSaveCoverAsync(searchParam, work.Id);
                                if (!string.IsNullOrEmpty(localPath))
                                {
                                    Dispatcher.Invoke(() => AddMessageToChat($"🖼️ 《{work.Title}》封面已更新并保存", false));
                                    RefreshCurrentView();
                                }
                                else
                                {
                                    Dispatcher.Invoke(() => AddMessageToChat($"⚠️ 《{work.Title}》的封面下载失败，可能是网络原因或找不到图片", false));
                                }
                            });
                            updatedFields.Add("封面链接");
                        }
                        break;
                    case "bangumiid":
                        // 这个字段主要用于辅助下载封面，不需要单独保存到数据库（除非您想扩充数据库字段）
                        // 标记为成功，避免因为只传了 bangumiId 和 coverurl 而导致最终判定失败
                        updatedFields.Add("BangumiID(辅助)");
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
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (var work in works)
            {
                var workBorder = new Border
                {
                    Background = ThemeManager.GetBrush("Surface2Brush"),
                    BorderBrush = ThemeManager.GetBrush("BorderSubtleBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var workStack = new StackPanel();

                // 标题和类型
                var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
                headerStack.Children.Add(new TextBlock
                {
                    Text = $"📖 {work.title}",
                    FontSize = 14,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = ThemeManager.GetBrush("TextPrimaryBrush")
                });
                headerStack.Children.Add(new TextBlock
                {
                    Text = $" [{GetTypeDisplay(work.type)}]",
                    FontSize = 11,
                    Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
                    Margin = new Thickness(5, 2, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                workStack.Children.Add(headerStack);

                // 声优与角色信息
                if (!string.IsNullOrEmpty(work.voiceActorInfo))
                {
                    workStack.Children.Add(new TextBlock
                    {
                        Text = $"🎙️ {work.voiceActorInfo}",
                        FontSize = 11,
                        FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = ThemeManager.GetBrush("AccentStrongBrush"),
                        Margin = new Thickness(0, 4, 0, 2)
                    });
                }

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
                        FontSize = 11,
                        Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                // 简介
                if (!string.IsNullOrEmpty(work.synopsis))
                {
                    workStack.Children.Add(new TextBlock
                    {
                        Text = work.synopsis.Length > 80 ? work.synopsis.Substring(0, 80) + "..." : work.synopsis,
                        FontSize = 11,
                        Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 6, 0, 0)
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
                            Background = ThemeManager.GetBrush("AccentSoftBrush"),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 4, 2),
                            Child = new TextBlock
                            {
                                Text = $"#{tag}",
                                FontSize = 9,
                                Foreground = ThemeManager.GetBrush("TextPrimaryBrush")
                            }
                        });
                    }
                    workStack.Children.Add(tagsPanel);
                }

                // 添加按钮
                var addBtn = new Button
                {
                    Content = "+ 添加到列表",
                    Width = 108,
                    Height = 32,
                    Style = (Style)FindResource("AppPrimaryButtonStyle"),
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
                Foreground = ThemeManager.GetBrush("TextMutedBrush"),
                Margin = new Thickness(0, 6, 0, 0)
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
                string typeEn = WorkDataRules.NormalizeTypeToEnglish(work.type);
                if (string.IsNullOrEmpty(typeEn))
                {
                    typeEn = "Anime";
                }

                System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] 转换后的type: {typeEn}");

                if (string.IsNullOrEmpty(work.title))
                {
                    AddMessageToChat("❌ 作品标题为空，无法添加", false);
                    return;
                }

                var workService = new WorkService(_currentAccountName);

                // 深度查重：同时检查标题和原名/别名，并且严格匹配类型
                var existingWorks = await workService.SearchWorksByNameAsync(work.title);
                // 如果标题没搜到，尝试用 AI 返回的原名搜索
                if (existingWorks.Count == 0 && !string.IsNullOrEmpty(work.originalTitle))
                {
                    existingWorks = await workService.SearchWorksByNameAsync(work.originalTitle);
                }

                // 过滤掉类型不一致的
                existingWorks = existingWorks.Where(w => WorkDataRules.IsSameWork(w.Title, w.OriginalTitle, w.Type, work.title, work.originalTitle, typeEn)).ToList();

                int workId = 0;
                var existingWork = existingWorks.FirstOrDefault();

                if (existingWork != null)
                {
                    workId = existingWork.Id;
                    AddMessageToChat($"💡 检测到《{existingWork.Title}》({existingWork.Type})已在列表中，正在更新信息...", false);
                    
                    // 获取该作品的详细信息以进行补全判断
                    var detailedWork = workService.GetWorkById(workId);
                    
                    if (detailedWork != null)
                    {
                        // 更新已有作品的信息 (补全空白)
                        if (!string.IsNullOrEmpty(work.company) && string.IsNullOrEmpty(detailedWork.Company)) workService.UpdateWorkCompany(workId, work.company);
                        if (!string.IsNullOrEmpty(work.season) && string.IsNullOrEmpty(detailedWork.Season)) workService.UpdateWorkSeason(workId, work.season);
                        var normalizedSourceType = WorkDataRules.NormalizeSourceType(work.sourceType);
                        if (!string.IsNullOrEmpty(normalizedSourceType) && string.IsNullOrEmpty(detailedWork.SourceType))
                        {
                            if (!(typeEn == "Game" && normalizedSourceType == "游戏改"))
                            {
                                workService.UpdateWorkSourceType(workId, normalizedSourceType);
                            }
                        }
                        if (!string.IsNullOrEmpty(work.episodes) && string.IsNullOrEmpty(detailedWork.EpisodesVolumes)) workService.UpdateWorkEpisodes(workId, work.episodes);
                        if (!string.IsNullOrEmpty(work.synopsis) && string.IsNullOrEmpty(detailedWork.Synopsis)) workService.UpdateWorkSynopsis(workId, work.synopsis);
                        if (!string.IsNullOrEmpty(work.author) && string.IsNullOrEmpty(detailedWork.Author)) workService.UpdateWorkAuthor(workId, work.author);
                        if (!string.IsNullOrEmpty(work.originalWork) && string.IsNullOrEmpty(detailedWork.OriginalWork)) workService.UpdateWorkOriginalWork(workId, work.originalWork);
                    }
                }

                if (workId == 0)
                {
                    // 处理原作类型
                    string sourceType = WorkDataRules.NormalizeSourceType(work.sourceType);
                    if (typeEn == "Game" && sourceType == "游戏改") sourceType = "";

                    // 确保所有参数完整（Rating 为 0 表示未评分，数据库允许 0-10）
                    workId = workService.AddWork(
                        work.title,                    // 标题
                        work.originalTitle ?? "",      // 原名
                        typeEn,                        // 类型
                        work.company ?? "",            // 制作
                        work.year ?? "",               // 年份
                        work.season ?? "",             // 季度
                        sourceType,                    // 原作类型
                        work.episodes ?? "",           // 集数
                        "",                            // 进度
                        "wish",                        // 状态
                        0,                             // 评分（0存入NULL）
                        work.synopsis ?? "",           // 简介
                        "",                            // 封面先留空，下载后再更新
                        work.author ?? "",             // 作者
                        work.originalWork ?? ""        // 原作
                    );
                }

                System.Diagnostics.Debug.WriteLine($"[AddWorkFromSearch] AddWork/Update返回: workId={workId}");

                if (workId > 0)
                {
                    if (existingWork == null) 
                        AddMessageToChat($"✅ 已添加《{work.title}》到列表", false);
                    else
                        AddMessageToChat($"✅ 《{work.title}》信息已同步更新", false);

                    // 异步下载封面 (如果原来没有封面)
                    if (!string.IsNullOrEmpty(work.coverUrl) && (existingWork == null || string.IsNullOrEmpty(existingWork.CoverPath)))
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
                        var currentTags = workService.GetWorkTags(workId);
                        var currentTagSet = new HashSet<string>(currentTags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
                        var newTags = new List<string>();
                        foreach (var tag in work.tags)
                        {
                            string trimmed = (tag ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(trimmed)) continue;
                            if (!currentTagSet.Contains(trimmed))
                            {
                                if (workService.AddWorkTag(workId, trimmed, source: "AI"))
                                {
                                    newTags.Add(trimmed);
                                    currentTagSet.Add(trimmed);
                                }
                            }
                        }
                        
                        // 特殊处理：如果是漫画或小说，并且有作者，把作者也当作一个标签加进去
                        if ((typeEn == "Manga" || typeEn == "LightNovel") && !string.IsNullOrEmpty(work.author))
                        {
                            string author = work.author.Trim();
                            if (!string.IsNullOrWhiteSpace(author) && !currentTagSet.Contains(author))
                            {
                                if (workService.AddWorkTag(workId, author, category: "作者", source: "AI"))
                                {
                                    newTags.Add(author);
                                    currentTagSet.Add(author);
                                }
                            }
                        }

                        // 特殊处理：如果是动画，并且有原作，把原作也当作一个标签加进去
                        if (typeEn == "Anime" && !string.IsNullOrEmpty(work.originalWork))
                        {
                            string originalWork = work.originalWork.Trim();
                            if (!string.IsNullOrWhiteSpace(originalWork) && !currentTagSet.Contains(originalWork))
                            {
                                if (workService.AddWorkTag(workId, originalWork, category: "原作", source: "AI"))
                                {
                                    newTags.Add(originalWork);
                                    currentTagSet.Add(originalWork);
                                }
                            }
                        }

                        if (newTags.Count > 0)
                        {
                            AddMessageToChat($"🏷️ 已新增标签：{string.Join("、", newTags)}", false);
                        }
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

        private void AIChatInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double targetHeight = 42;
                int lastCharacterIndex = Math.Max(textBox.Text.Length - 1, 0);
                Rect contentRect = textBox.GetRectFromCharacterIndex(lastCharacterIndex, true);

                if (!contentRect.IsEmpty)
                {
                    targetHeight = Math.Max(42, contentRect.Bottom + 20);
                }

                textBox.Height = Math.Min(180, targetHeight);
                textBox.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Background);
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
            SetSidebarSelectionByTag("all");
        }

        // 显示添加作品选择界面
        public void ShowAddWorkOptions()
        {
            var addForm = new Views.AddWorkForm(_currentAccountName);
            addForm.WorkAdded += () => RefreshCurrentView();
            MainContentArea.Content = addForm;
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
            SetSidebarSelectionByTag("all");
        }

        // 显示笔记列表
        public void ShowNotesView()
        {
            var notesView = new Views.NotesView(_currentAccountName);
            MainContentArea.Content = notesView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
            SetSidebarSelectionByTag("notes");
        }

        public void ShowDetailView(UserControl detailView)
        {
            MainContentArea.Content = detailView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
        }

        private void InitializeTopThemeSwitcher()
        {
            if (TopThemePalettePanel == null) return;

            TopThemePalettePanel.Children.Clear();
            _topThemeButtons.Clear();

            foreach (var accent in ThemeManager.AccentOptions)
            {
                var button = new Button
                {
                    ToolTip = accent.Label,
                    Tag = accent.Key,
                    Background = new SolidColorBrush(accent.Color),
                    Style = (Style)FindResource("TopThemeSwatchButtonStyle")
                };
                button.SetResourceReference(Button.BorderBrushProperty, "BorderBrush");
                button.Click += TopThemeAccentButton_Click;
                _topThemeButtons[accent.Key] = button;
                TopThemePalettePanel.Children.Add(button);
            }
        }

        private void TopThemeAccentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string accentKey)
            {
                return;
            }

            var config = ConfigManager.Load();
            config.ThemeAccent = ThemeManager.NormalizeAccent(accentKey);
            ConfigManager.Save(config);
            ThemeManager.ApplyTheme(config.ThemeAccent, config.ThemeMode);
            ApplyTopThemeSelection(config.ThemeAccent);
            ApplyTopThemeModeSelection(config.ThemeMode);
            TopThemePopup.IsOpen = false;
        }

        private void ApplyTopThemeSelection(string accentKey)
        {
            var accent = ThemeManager.GetAccentOption(accentKey);
            TopThemeHintText.Text = $"主题色 · {accent.Label}";
            ThemeQuickButton.ToolTip = $"当前主题色：{accent.Label}";

            foreach (var pair in _topThemeButtons)
            {
                if (pair.Key.Equals(accent.Key, StringComparison.OrdinalIgnoreCase))
                {
                    pair.Value.SetResourceReference(Button.BorderBrushProperty, "TextPrimaryBrush");
                }
                else
                {
                    pair.Value.SetResourceReference(Button.BorderBrushProperty, "BorderBrush");
                }
            }
        }

        private void ApplyTopThemeModeSelection(string modeKey)
        {
            bool isDark = string.Equals(modeKey, "Dark", StringComparison.OrdinalIgnoreCase);
            if (TopLightModeButton != null) TopLightModeButton.IsChecked = !isDark;
            if (TopDarkModeButton != null) TopDarkModeButton.IsChecked = isDark;
        }

        private void UpdateWindowControlGlyph()
        {
            if (MaximizeRestoreButton == null)
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                MaximizeRestoreButton.Content = "\uE923";
                MaximizeRestoreButton.ToolTip = "还原";
            }
            else
            {
                MaximizeRestoreButton.Content = "\uE922";
                MaximizeRestoreButton.ToolTip = "最大化";
            }
        }

        private void SetSidebarSelectionByTag(string tag)
        {
            if (SidebarPanel == null) return;
            var targetButton = SidebarPanel.Children.OfType<Button>().FirstOrDefault(x => string.Equals(x.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
            if (targetButton != null)
            {
                SetSidebarSelection(targetButton);
            }
        }

        private void SetSidebarSelection(Button selectedButton)
        {
            if (SidebarPanel == null) return;

            foreach (var button in SidebarPanel.Children.OfType<Button>())
            {
                button.ClearValue(Button.BackgroundProperty);
                button.ClearValue(Button.BorderBrushProperty);
            }

            if (selectedButton != null)
            {
                selectedButton.SetResourceReference(Button.BackgroundProperty, "AccentSoftBrush");
                selectedButton.SetResourceReference(Button.BorderBrushProperty, "AccentSoftBrush");
            }
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
