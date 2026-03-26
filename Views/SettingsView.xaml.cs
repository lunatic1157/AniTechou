using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class SettingsView : UserControl
    {
        private string _accountName;

        public SettingsView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            LoadSettings();

            // 显示数据路径
            string dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniTechou",
                "accounts"
            );
            DataPathText.Text = dataPath;
        }

        private void LoadSettings()
        {
            var config = ConfigManager.Load();

            // 设置平台选中项
            string platform = config.Platform;
            for (int i = 0; i < PlatformBox.Items.Count; i++)
            {
                var item = PlatformBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == platform)
                {
                    PlatformBox.SelectedIndex = i;
                    UpdateApiFields(item);
                    break;
                }
            }

            ApiKeyBox.Password = config.ApiKey;
            AutoLoginBox.IsChecked = config.AutoLogin;
        }

        private void Platform_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = PlatformBox.SelectedItem as ComboBoxItem;
            if (item != null)
            {
                UpdateApiFields(item);
            }
        }

        private void UpdateApiFields(ComboBoxItem item)
        {
            string apiUrl = item.Tag?.ToString() ?? "";
            string model = item.Tag?.ToString() ?? "";
            // 从 Tag 中获取 API URL，从 Model 属性获取模型
            if (item.Tag != null)
            {
                ApiUrlBox.Text = item.Tag.ToString();
            }
            // ComboBoxItem 没有 Model 属性，需要用其他方式
            // 改用 Content 来区分
            string selectedPlatform = item.Content?.ToString() ?? "";
            switch (selectedPlatform)
            {
                case "DeepSeek":
                    ModelBox.Text = "deepseek-chat";
                    break;
                case "智谱AI":
                    ModelBox.Text = "glm-4-flash";
                    break;
                case "阿里云百炼":
                    ModelBox.Text = "qwen-turbo";
                    break;
                case "OpenAI":
                    ModelBox.Text = "gpt-3.5-turbo";
                    break;
                default:
                    ModelBox.Text = "deepseek-chat";
                    break;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyBox.Password;
            string apiUrl = ApiUrlBox.Text;
            string model = ModelBox.Text;

            if (string.IsNullOrEmpty(apiKey))
            {
                TestStatusText.Text = "请输入API Key";
                TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                return;
            }

            TestStatusText.Text = "测试中...";
            TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90));

            try
            {
                var aiService = new AIService(apiKey, apiUrl, model);
                bool success = await aiService.TestConnection();

                if (success)
                {
                    TestStatusText.Text = "✓ 连接成功";
                    TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else
                {
                    TestStatusText.Text = "✗ 连接失败，请检查API Key";
                    TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
            catch (Exception ex)
            {
                TestStatusText.Text = $"✗ 连接失败：{ex.Message}";
                TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void EditPrompt_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.Load();
            
            var promptDialog = new Window
            {
                Title = "自定义 AI 系统提示词",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 244, 233))
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var helpText = new TextBlock
            {
                Text = "在这里您可以修改 AI 的核心指令。请谨慎修改 JSON 格式要求，否则可能导致软件解析失败。\n如果您改乱了，清空文本框并保存即可恢复默认提示词。",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 110, 90)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(helpText, 0);
            grid.Children.Add(helpText);

            var textBox = new TextBox
            {
                Text = string.IsNullOrEmpty(config.CustomSystemPrompt) ? AIService.GetDefaultSystemPrompt() : config.CustomSystemPrompt,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(10)
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var okBtn = new Button { Content = "保存", Width = 100, Height = 35, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61)), Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 10, 0) };
            var resetBtn = new Button { Content = "恢复默认", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "取消", Width = 100, Height = 35 };

            okBtn.Click += (s, args) =>
            {
                config.CustomSystemPrompt = textBox.Text.Trim();
                ConfigManager.Save(config);
                MessageBox.Show("AI 提示词已保存，将在下一次对话时生效。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                promptDialog.Close();
            };

            resetBtn.Click += (s, args) =>
            {
                if (MessageBox.Show("确定要恢复默认提示词吗？您的自定义修改将丢失。", "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    textBox.Text = AIService.GetDefaultSystemPrompt();
                }
            };

            cancelBtn.Click += (s, args) => promptDialog.Close();

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(resetBtn);
            btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            promptDialog.Content = grid;
            promptDialog.ShowDialog();
        }

        private void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出数据",
                Filter = "JSON文件|*.json|所有文件|*.*",
                FileName = $"anitechou_export_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var workService = new WorkService(_accountName);
                var data = workService.ExportAllData();
                File.WriteAllText(dialog.FileName, data);
                MessageBox.Show($"数据已导出到：{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            string dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniTechou"
            );

            if (Directory.Exists(dataPath))
            {
                Process.Start("explorer.exe", dataPath);
            }
            else
            {
                MessageBox.Show("数据文件夹不存在");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // 清除自动登录记录
                var config = ConfigManager.Load();
                config.AutoLogin = false;
                ConfigManager.Save(config);

                // 返回登录窗口
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.Logout();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPlatform = PlatformBox.SelectedItem as ComboBoxItem;
                string platform = selectedPlatform?.Content?.ToString() ?? "DeepSeek";

                var config = new AppConfig
                {
                    Platform = platform,
                    ApiKey = ApiKeyBox.Password,
                    ApiUrl = ApiUrlBox.Text,
                    Model = ModelBox.Text,
                    AutoLogin = AutoLoginBox.IsChecked ?? true
                };

                ConfigManager.Save(config);

                // 验证保存是否成功
                var loaded = ConfigManager.Load();
                if (loaded.ApiKey == config.ApiKey && loaded.ApiUrl == config.ApiUrl && loaded.Model == config.Model)
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证通过 - Platform: {loaded.Platform}");
                    MessageBox.Show("设置已保存并验证成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证失败");
                    MessageBox.Show("设置保存可能未完全生效，请检查文件权限", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证 - Platform: {loaded.Platform}, ApiKey: {(string.IsNullOrEmpty(loaded.ApiKey) ? "空" : "已设置")}");

                MessageBox.Show("设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // 返回上一页
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.RefreshCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
