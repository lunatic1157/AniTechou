using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            public bool IsCustom { get; set; }
            public override string ToString() => Name;
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
            VersionText.Text = $"版本 {GetAppVersion()}";
        }

        private void InitializePlatforms()
        {
            _platforms = new List<PlatformOption>
            {
                new PlatformOption { Name = "DeepSeek", ApiUrl = "https://api.deepseek.com/v1", Model = "deepseek-chat" },
                new PlatformOption { Name = "Kimi (月之暗面)", ApiUrl = "https://api.moonshot.cn/v1", Model = "moonshot-v1-8k" },
                new PlatformOption { Name = "OpenAI", ApiUrl = "https://api.openai.com/v1", Model = "gpt-4o-mini" },
                new PlatformOption { Name = "自定义", ApiUrl = "", Model = "", IsCustom = true }
            };

            PlatformBox.ItemsSource = _platforms;
            PlatformBox.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            var config = ConfigManager.Load();

            // 设置平台选中项
            string platform = config.Platform;
            var selected = _platforms.FirstOrDefault(p => p.Name == platform);
            if (selected == null && !string.IsNullOrEmpty(config.ApiUrl))
            {
                // 自定义平台
                selected = _platforms.FirstOrDefault(p => p.IsCustom);
                if (selected != null)
                {
                    selected.ApiUrl = config.ApiUrl;
                    selected.Model = config.Model;
                }
            }
            if (selected == null)
                selected = _platforms.FirstOrDefault();

            if (selected != null)
            {
                PlatformBox.SelectedItem = selected;
                UpdateApiFields(selected);
                // 覆盖：总是用已保存的值
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    ApiUrlBox.Text = config.ApiUrl;
                if (!string.IsNullOrEmpty(config.Model))
                    ModelBox.Text = config.Model;
            }

            ApiKeyBox.Password = config.ApiKey;
            AutoLoginBox.IsChecked = config.AutoLogin;
            DalianPetBox.IsChecked = config.EnableDesktopPet;
            WebSearchBox.IsChecked = config.EnableWebSearch;
            BangumiSearchBox.IsChecked = config.EnableBangumiSearch;
            MALSearchBox.IsChecked = config.EnableMALSearch;
            AniListSearchBox.IsChecked = config.EnableAniListSearch;
            BangumiUserBox.Text = config.BangumiUsername ?? "";
            SelectProxyMode(config.ProxyMode);
            ProxyAddressBox.Text = config.ProxyAddress ?? "";
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
            ApiUrlBox.Text = item.IsCustom ? "" : (item.ApiUrl ?? "");
            ModelBox.Text = item.IsCustom ? "" : (item.Model ?? "");
        }

        // 当用户手动修改 API 地址或模型时，自动切换到自定义平台
        private void ApiUrlOrModel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PlatformBox.SelectedItem is PlatformOption selected && selected.IsCustom)
                return; // 已经是自定义，不用再切

            // 检查当前内容是否与选中平台预设一致
            if (PlatformBox.SelectedItem is PlatformOption current)
            {
                if (ApiUrlBox.Text != current.ApiUrl || ModelBox.Text != current.Model)
                {
                    // 内容变了，切换到自定义
                    var custom = _platforms.FirstOrDefault(p => p.IsCustom);
                    if (custom != null)
                    {
                        PlatformBox.SelectedItem = custom;
                    }
                }
            }
        }

        private static string GetAppVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                int plusIndex = informationalVersion.IndexOf('+');
                return plusIndex > 0 ? informationalVersion.Substring(0, plusIndex) : informationalVersion;
            }

            return assembly.GetName().Version?.ToString(3) ?? "未知";
        }

        private void SelectProxyMode(string proxyMode)
        {
            string normalized = NetworkClientFactory.NormalizeProxyMode(proxyMode);
            foreach (var item in ProxyModeBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    ProxyModeBox.SelectedItem = item;
                    return;
                }
            }
            ProxyModeBox.SelectedIndex = 0;
        }

        private string GetSelectedProxyMode()
        {
            return (ProxyModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                   ?? NetworkClientFactory.ProxyModeSystem;
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

        private async void NetworkDiagnostic_Click(object sender, RoutedEventArgs e)
        {
            NetworkDiagnosticStatusText.Text = "诊断中...";
            NetworkDiagnosticStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            try
            {
                var tempConfig = ConfigManager.Load();
                tempConfig.ProxyMode = GetSelectedProxyMode();
                tempConfig.ProxyAddress = ProxyAddressBox.Text.Trim();
                ConfigManager.Save(tempConfig);

                var results = await NetworkClientFactory.RunDiagnosticsAsync(
                    ApiUrlBox.Text.Trim(),
                    ApiKeyBox.Password,
                    ModelBox.Text.Trim());

                NetworkDiagnosticStatusText.Text = string.Join("\n", results.Select(r =>
                    $"{(r.Success ? "OK" : "FAIL")} {r.Name}: {r.Message} ({r.ElapsedMilliseconds}ms)"));

                bool allCoreOk = results.Where(r => r.Name != "AI API").Any(r => r.Success);
                NetworkDiagnosticStatusText.SetResourceReference(TextBlock.ForegroundProperty,
                    allCoreOk ? "SuccessBrush" : "DangerBrush");
            }
            catch (Exception ex)
            {
                NetworkDiagnosticStatusText.Text = $"诊断失败：{ex.Message}";
                NetworkDiagnosticStatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
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
                Filter = "AniTechou备份文件|*.zip|JSON文件|*.json|所有文件|*.*",
                FileName = $"anitechou_backup_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var workService = new WorkService(_accountName);
                string ext = Path.GetExtension(dialog.FileName) ?? "";
                if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    workService.ExportPortableBackup(dialog.FileName);
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导出成功", $"可移植备份已导出到：{dialog.FileName}");
                }
                else
                {
                    var data = workService.ExportAllData();
                    File.WriteAllText(dialog.FileName, data);
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "导出成功", $"数据已导出到：{dialog.FileName}");
                }
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入数据",
                Filter = "AniTechou备份文件|*.zip|JSON文件|*.json|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var workService = new WorkService(_accountName);
                WorkService.ImportResult result;
                string ext = Path.GetExtension(dialog.FileName) ?? "";
                if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    result = workService.ImportPortableBackup(dialog.FileName);
                }
                else
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
                    result = workService.ImportAllData(json);
                }
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

        private void TagCleanupPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var workService = new WorkService(_accountName);
                var preview = workService.GenerateTagCleanupPreview();
                var dialog = new Windows.TagCleanupPreviewWindow(_accountName, preview)
                {
                    Owner = Application.Current.MainWindow
                };

                bool? applied = dialog.ShowDialog();
                if (applied == true)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.RefreshCurrentView();
                }
            }
            catch (Exception ex)
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "标签清理预览失败", ex.Message);
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
                config.EnableDesktopPet = DalianPetBox.IsChecked ?? false;
                config.EnableWebSearch = WebSearchBox.IsChecked ?? false;
                config.EnableBangumiSearch = BangumiSearchBox.IsChecked ?? true;
                config.EnableMALSearch = MALSearchBox.IsChecked ?? false;
                config.EnableAniListSearch = AniListSearchBox.IsChecked ?? false;
                config.BangumiUsername = BangumiUserBox.Text.Trim();
                config.ProxyMode = GetSelectedProxyMode();
                config.ProxyAddress = ProxyAddressBox.Text.Trim();

                ConfigManager.Save(config);
                DesktopPetService.Instance.RefreshFromConfig();

                // 验证保存是否成功
                var loaded = ConfigManager.Load();
                if (loaded.ApiKey == config.ApiKey &&
                    loaded.ApiUrl == config.ApiUrl &&
                    loaded.Model == config.Model &&
                    loaded.EnableDesktopPet == config.EnableDesktopPet)
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

        private async void SyncBangumi_Click(object sender, RoutedEventArgs e)
        {
            string username = BangumiUserBox.Text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                SyncStatusText.Text = "请输入 Bangumi 用户名";
                return;
            }
            SyncStatusText.Text = "正在从 Bangumi 同步...";
            var service = new SyncService(_accountName);
            var result = await service.SyncFromBangumiAsync(username);
            if (result.Success)
            {
                string detailSummary = result.Details.Count > 0
                    ? "\n" + string.Join("\n", result.Details.Take(10))
                    : "";
                if (result.Details.Count > 10)
                    detailSummary += $"\n... 等 {result.Details.Count} 项变更";
                SyncStatusText.Text = $"✅ Bangumi 同步完成\n更新 {result.UpdatedWorks} | 跳过 {result.SkippedWorks} | 未匹配 {result.Unmatched}{detailSummary}";
            }
            else
            {
                SyncStatusText.Text = $"❌ {result.ErrorMessage}";
            }
        }

    }
}
