using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

using System.IO;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                    
                    // 加载头像
                    if (!string.IsNullOrEmpty(account.AvatarPath) && File.Exists(account.AvatarPath))
                    {
                        LoadAvatarImage(account.AvatarPath);
                    }
                }

                // 昵称 - 从数据库获取
                string nickname = _workService.GetNickname();
                NicknameText.Text = string.IsNullOrEmpty(nickname) ? "ACGN爱好者" : nickname;

                // 年度总结
                LoadYearStats(stats.YearStats);

                // 标签云
                LoadTagCloud(stats.TagStats);
                
                // 再次尝试从 MainWindow 中获取最新状态的头像
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null && mainWindow._accountManager.CurrentAccount != null)
                {
                    string currentAvatar = mainWindow._accountManager.CurrentAccount.AvatarPath;
                    if (!string.IsNullOrEmpty(currentAvatar) && File.Exists(currentAvatar))
                    {
                        LoadAvatarImage(currentAvatar);
                    }
                }
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
                    Margin = new Thickness(5, 3, 5, 3),
                    FontSize = fontSize
                };
                tagButton.Style = (Style)FindResource("AppGhostButtonStyle");
                tagButton.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
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
                Width = 360,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                FontFamily = FontFamily
            };
            inputDialog.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");

            var panel = new StackPanel { Margin = new Thickness(20) };
            var label = new TextBlock { Text = "请输入新昵称：" };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            var textBox = new TextBox { Text = NicknameText.Text, Height = 42, Padding = new Thickness(12, 6, 12, 6) };
            textBox.Style = (Style)FindResource("AppTextBoxStyle");
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
            var okButton = new Button { Content = "确定", Width = 88, Height = 36, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("AppPrimaryButtonStyle") };
            var cancelButton = new Button { Content = "取消", Width = 88, Height = 36, Style = (Style)FindResource("AppSecondaryButtonStyle") };

            okButton.Click += (s, args) =>
            {
                string newNickname = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(newNickname))
                {
                    if (_workService.UpdateNickname(newNickname))
                    {
                        NicknameText.Text = newNickname;
                        Windows.AppMessageDialog.Show(Application.Current.MainWindow, "成功", "昵称更新成功！");
                    }
                    else
                    {
                        Windows.AppMessageDialog.Show(Application.Current.MainWindow, "失败", "昵称更新失败");
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

        private void Avatar_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择头像",
                Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|所有文件 (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 将图片复制到应用数据目录
                    string appDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AniTechou",
                        "avatars"
                    );

                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }

                    string extension = Path.GetExtension(openFileDialog.FileName);
                    string fileName = $"{_accountName}_avatar_{DateTime.Now.Ticks}{extension}";
                    string targetPath = Path.Combine(appDataDir, fileName);

                    File.Copy(openFileDialog.FileName, targetPath, true);

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        if (mainWindow._accountManager.UpdateAvatar(_accountName, targetPath))
                        {
                            LoadAvatarImage(targetPath);
                            MessageBox.Show("头像更新成功！");
                        }
                        else
                        {
                            MessageBox.Show("保存头像路径失败。");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"设置头像失败: {ex.Message}");
                }
            }
        }

        private void LoadAvatarImage(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    ShowDefaultAvatar();
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // 解决文件被锁定的问题，允许覆盖保存
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                
                var avatarImage = FindName("AvatarImage") as Image;
                var defaultAvatarText = FindName("DefaultAvatarText") as TextBlock;

                if (avatarImage != null) avatarImage.Source = bitmap;
                if (defaultAvatarText != null) defaultAvatarText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileView] LoadAvatarImage Error: {ex.Message}");
                ShowDefaultAvatar();
            }
        }

        private void ShowDefaultAvatar()
        {
            var avatarImage = FindName("AvatarImage") as Image;
            var defaultAvatarText = FindName("DefaultAvatarText") as TextBlock;

            if (avatarImage != null) avatarImage.Source = null;
            if (defaultAvatarText != null) defaultAvatarText.Visibility = Visibility.Visible;
        }
    }
}
