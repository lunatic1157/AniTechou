using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class SettingsView : UserControl
    {
        private string _accountName;
        private List<PlatformOption> _platforms = new List<PlatformOption>();

        private class PlatformOption
        {
            public string Name { get; set; }
            public string ApiUrl { get; set; }
            public string Model { get; set; }
        }

        public SettingsView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            InitializePlatforms();
            LoadSettings();

            // 显示数据路径
            string dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniTechou",
                "accounts"
            );
            DataPathText.Text = dataPath;
        }

        private void InitializePlatforms()
        {
            _platforms = new List<PlatformOption>
            {
                new PlatformOption { Name = "DeepSeek", ApiUrl = "https://api.deepseek.com/v1", Model = "deepseek-chat" },
                new PlatformOption { Name = "智谱AI", ApiUrl = "https://open.bigmodel.cn/api/paas/v4", Model = "glm-4-flash" },
                new PlatformOption { Name = "阿里云百炼", ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", Model = "qwen-turbo" },
                new PlatformOption { Name = "OpenAI", ApiUrl = "https://api.openai.com/v1", Model = "gpt-3.5-turbo" }
            };

            PlatformBox.ItemsSource = _platforms;
            PlatformBox.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            var config = ConfigManager.Load();

            // 设置平台选中项
            string platform = config.Platform;
            var selected = _platforms.FirstOrDefault(p => p.Name == platform) ?? _platforms.FirstOrDefault();
            if (selected != null)
            {
                PlatformBox.SelectedItem = selected;
                UpdateApiFields(selected);
            }

            ApiKeyBox.Password = config.ApiKey;
            AutoLoginBox.IsChecked = config.AutoLogin;
        }

        private void Platform_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlatformBox.SelectedItem is PlatformOption item)
            {
                UpdateApiFields(item);
            }
        }

        private void UpdateApiFields(PlatformOption item)
        {
            ApiUrlBox.Text = item.ApiUrl ?? "";
            ModelBox.Text = item.Model ?? "";
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyBox.Password;
            string apiUrl = ApiUrlBox.Text;
            string model = ModelBox.Text;

            if (string.IsNullOrEmpty(apiKey))
            {
                TestStatusText.Text = "请输入API Key";
                TestStatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
                return;
            }

            TestStatusText.Text = "测试中...";
            TestStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            try
            {
                var aiService = new AIService(apiKey, apiUrl, model);
                bool success = await aiService.TestConnection();

                if (success)
                {
                    TestStatusText.Text = "✓ 连接成功";
                    TestStatusText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
                }
                else
                {
                    TestStatusText.Text = "✗ 连接失败，请检查API Key";
                    TestStatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
                }
            }
            catch (Exception ex)
            {
                TestStatusText.Text = $"✗ 连接失败：{ex.Message}";
                TestStatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
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
                FontFamily = FontFamily
            };
            promptDialog.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var helpText = new TextBlock
            {
                Text = "在这里您可以修改 AI 的核心指令。请谨慎修改 JSON 格式要求，否则可能导致软件解析失败。\n如果您改乱了，清空文本框并保存即可恢复默认提示词。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            helpText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
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
            textBox.Style = (Style)FindResource("AppTextBoxStyle");
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var okBtn = new Button { Content = "保存", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("AppPrimaryButtonStyle") };
            var resetBtn = new Button { Content = "恢复默认", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("AppSecondaryButtonStyle") };
            var cancelBtn = new Button { Content = "取消", Width = 100, Height = 35, Style = (Style)FindResource("AppGhostButtonStyle") };

            okBtn.Click += (s, args) =>
            {
                config.CustomSystemPrompt = textBox.Text.Trim();
                ConfigManager.Save(config);
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "保存成功", "AI 提示词已保存，将在下一次对话时生效。");
                promptDialog.Close();
            };

            resetBtn.Click += (s, args) =>
            {
                if (Windows.AppMessageDialog.Show(Application.Current.MainWindow, "确认恢复", "确定要恢复默认提示词吗？您的自定义修改将丢失。", true))
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
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导出成功", $"数据已导出到：{dialog.FileName}");
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入数据",
                Filter = "JSON文件|*.json|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string json;
                try
                {
                    json = File.ReadAllText(dialog.FileName);
                }
                catch (Exception ex)
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导入失败", $"读取文件失败：{ex.Message}");
                    return;
                }

                var workService = new WorkService(_accountName);
                var result = workService.ImportAllData(json);
                if (!result.Success)
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导入失败", result.ErrorMessage);
                    return;
                }

                string message =
                    $"作品：新增 {result.NewWorks}，更新 {result.UpdatedWorks}，跳过 {result.SkippedWorks}，无效 {result.InvalidWorks}\n" +
                    $"笔记：新增 {result.NewNotes}，更新 {result.UpdatedNotes}，跳过 {result.SkippedNotes}，无效 {result.InvalidNotes}";
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导入完成", message);
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
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "数据文件夹不存在");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (Windows.AppMessageDialog.Show(Application.Current.MainWindow, "确认", "确定要退出登录吗？", true))
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
                string platform = (PlatformBox.SelectedItem as PlatformOption)?.Name ?? "DeepSeek";
                var config = ConfigManager.Load();
                config.Platform = platform;
                config.ApiKey = ApiKeyBox.Password;
                config.ApiUrl = ApiUrlBox.Text;
                config.Model = ModelBox.Text;
                config.AutoLogin = AutoLoginBox.IsChecked ?? true;

                ConfigManager.Save(config);

                // 验证保存是否成功
                var loaded = ConfigManager.Load();
                if (loaded.ApiKey == config.ApiKey &&
                    loaded.ApiUrl == config.ApiUrl &&
                    loaded.Model == config.Model)
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证通过 - Platform: {loaded.Platform}");
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "成功", "设置已保存并生效");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证失败");
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "警告", "设置保存可能未完全生效，请检查文件权限");
                }
                System.Diagnostics.Debug.WriteLine($"[Settings] 保存后验证 - Platform: {loaded.Platform}, ApiKey: {(string.IsNullOrEmpty(loaded.ApiKey) ? "空" : "已设置")}");

                // 返回上一页
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.RefreshCurrentView();
            }
            catch (Exception ex)
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "错误", $"保存失败：{ex.Message}");
            }
        }
    }
}
