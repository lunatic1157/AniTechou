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
