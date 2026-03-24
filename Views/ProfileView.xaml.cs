using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class ProfileView : UserControl
    {
        private string _accountName;
        private WorkService _workService;

        public ProfileView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            _workService = new WorkService(accountName);

            LoadProfile();
        }

        private void LoadProfile()
        {
            try
            {
                // 获取统计数据
                var stats = _workService.GetStats();

                // 统计看板
                AnimeCount.Text = stats.AnimeCount.ToString();
                MangaCount.Text = stats.MangaCount.ToString();
                GameCount.Text = stats.GameCount.ToString();
                TotalWorksText.Text = stats.TotalWorks.ToString();
                TotalNotesText.Text = stats.TotalNotes.ToString();

                // 加入时间 - 从 AccountManager 获取
                var accountManager = new AccountManager();
                var account = accountManager.CurrentAccount;
                if (account != null)
                {
                    JoinDateText.Text = $"加入时间：{account.CreatedTime:yyyy年MM月dd日}";
                }

                // 昵称 - 从数据库获取
                string nickname = _workService.GetNickname();
                NicknameBox.Text = string.IsNullOrEmpty(nickname) ? "ACGN爱好者" : nickname;

                // 年度总结
                LoadYearStats(stats.YearStats);

                // 标签云
                LoadTagCloud(stats.TagStats);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}");
            }
        }

        private void LoadYearStats(Dictionary<int, int> yearStats)
        {
            if (yearStats == null || yearStats.Count == 0)
            {
                NoYearDataText.Visibility = Visibility.Visible;
                YearStatsList.Visibility = Visibility.Collapsed;
                return;
            }

            NoYearDataText.Visibility = Visibility.Collapsed;
            YearStatsList.Visibility = Visibility.Visible;

            int maxCount = yearStats.Values.Max();
            var items = new List<object>();

            foreach (var kv in yearStats.OrderByDescending(x => x.Key))
            {
                int barWidth = maxCount > 0 ? (int)(kv.Value * 200.0 / maxCount) : 0;
                items.Add(new { Year = kv.Key.ToString(), Count = kv.Value, CountDisplay = $"{kv.Value}部", BarWidth = barWidth });
            }

            YearStatsList.ItemsSource = items;
        }

        private void LoadTagCloud(Dictionary<string, int> tagStats)
        {
            TagCloudPanel.Children.Clear();

            if (tagStats == null || tagStats.Count == 0)
            {
                NoTagText.Visibility = Visibility.Visible;
                return;
            }

            NoTagText.Visibility = Visibility.Collapsed;

            int maxCount = tagStats.Values.Max();

            foreach (var kv in tagStats.OrderByDescending(x => x.Value).Take(30))
            {
                int fontSize = 10 + (int)(kv.Value * 20.0 / maxCount);

                var tagButton = new Button
                {
                    Content = kv.Key,
                    Tag = kv.Key,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(5, 3, 5, 3),
                    FontSize = fontSize,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(92, 78, 61))
                };
                tagButton.Click += TagButton_Click;
                TagCloudPanel.Children.Add(tagButton);
            }
        }

        private void TagButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string tag = btn?.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            // 跳转到作品列表并筛选该标签
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowWorksWithTag(tag);
        }

        private void EditNickname_Click(object sender, RoutedEventArgs e)
        {
            // 使用简单的输入框
            var inputDialog = new Window
            {
                Title = "编辑昵称",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            var label = new TextBlock { Text = "请输入新昵称：" };
            var textBox = new TextBox { Text = NicknameBox.Text };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
            var okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "取消", Width = 80 };

            okButton.Click += (s, args) =>
            {
                string newNickname = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(newNickname))
                {
                    if (_workService.UpdateNickname(newNickname))
                    {
                        NicknameBox.Text = newNickname;
                        MessageBox.Show("昵称更新成功！");
                    }
                    else
                    {
                        MessageBox.Show("昵称更新失败");
                    }
                }
                inputDialog.DialogResult = true;
                inputDialog.Close();
            };

            cancelButton.Click += (s, args) =>
            {
                inputDialog.DialogResult = false;
                inputDialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(label);
            panel.Children.Add(textBox);
            panel.Children.Add(buttonPanel);
            inputDialog.Content = panel;

            inputDialog.ShowDialog();
        }
    }
}
