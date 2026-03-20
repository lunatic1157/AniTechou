using System;
using System.Collections.Generic;
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

            LoadRecentItems();
            
            // 确保AI面板默认展开
            _isAIPanelVisible = true;
            AIPanelColumn.Width = new GridLength(320);
            
            // 尝试自动登录
            if (_accountManager.TryAutoLogin())
            {
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

        private void LoadRecentItems()
        {
            var recent = new List<dynamic>
            {
                new { Title = "葬送的芙莉莲" },
                new { Title = "孤独摇滚" },
                new { Title = "我独自升级" }
            };
            RecentList.ItemsSource = recent;
        }

        private void LoadMainContent()
        {
            string accountName = _accountManager.CurrentAccount?.UserName ?? "default";
            var worksView = new Views.WorksView(accountName, "all", "all");
            MainContentArea.Content = worksView;
            ContentPlaceholder.Visibility = Visibility.Collapsed;
            MainContentArea.Visibility = Visibility.Visible;
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
            MessageBox.Show("打开快速笔记窗口");
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
                case "anime": type = "anime"; break;
                case "manga": type = "manga"; break;
                case "lightnovel": type = "lightnovel"; break;
                case "game": type = "game"; break;
                case "wish": status = "wish"; break;
                case "doing": status = "doing"; break;
                case "done": status = "done"; break;
                case "all": type = "all"; break;
                case "notes":
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

        private void RecentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = RecentList.SelectedItem;
            if (selected != null)
            {
                MessageBox.Show($"打开：{selected.GetType().GetProperty("Title")?.GetValue(selected)}");
                RecentList.SelectedItem = null;
            }
        }

        // ========== 顶部栏功能 ==========
        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("打开个人主页");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("设置功能开发中");
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
                    Text = isUser ? $"👤 {message}" : $"🤖 {message}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
            };
            
            ChatMessagesPanel.Children.Add(border);
            
            // 滚动到底部
            ChatScrollViewer.ScrollToBottom();
        }

        private void SendAIChat_Click(object sender, RoutedEventArgs e)
        {
            string message = AIChatInputBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;
            
            // 添加用户消息
            AddMessageToChat(message, true);
            
            // 清空输入框
            AIChatInputBox.Text = "";
            
            // 模拟AI回复（后续接入真实API）
            string aiReply = GetAIReply(message);
            AddMessageToChat(aiReply, false);
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
    }
}