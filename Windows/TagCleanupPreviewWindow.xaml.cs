using System;
using System.IO;
using System.Windows;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class TagCleanupPreviewWindow : Window
    {
        private readonly string _accountName;
        private readonly WorkService _workService;
        private readonly WorkService.TagCleanupPreview _preview;

        public TagCleanupPreviewWindow(string accountName, WorkService.TagCleanupPreview preview = null)
        {
            InitializeComponent();
            _accountName = accountName;
            _workService = new WorkService(accountName);
            _preview = preview ?? _workService.GenerateTagCleanupPreview();
            LoadPreview();
        }

        private void LoadPreview()
        {
            PreviewList.ItemsSource = _preview.Items;

            if (!_preview.HasChanges)
            {
                SummaryText.Text = "没有发现需要清理的标签。";
                BackupText.Text = "";
                ApplyButton.IsEnabled = false;
                return;
            }

            SummaryText.Text =
                $"将影响 {_preview.TotalWorksAffected} 部作品，删除 {_preview.TagsToRemoveCount} 个冗余标签，新增/转换 {_preview.TagsToAddCount} 个标准标签。";
            BackupText.Text = "应用清理前会先导出 .zip 备份到 AniTechou\\backups。";
            ApplyButton.IsEnabled = true;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!_preview.HasChanges)
                return;

            bool confirmed = AppMessageDialog.Show(
                this,
                "确认应用标签清理",
                $"即将清理 {_preview.TotalWorksAffected} 部作品的标签。\n\n会先自动导出备份；清理只删除预览中列出的冗余标签，并新增标准化人员标签。是否继续？",
                showCancel: true,
                confirmText: "备份并应用",
                cancelText: "取消");

            if (!confirmed)
                return;

            try
            {
                string backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AniTechou",
                    "backups");
                Directory.CreateDirectory(backupDir);

                string backupPath = Path.Combine(
                    backupDir,
                    $"tag_cleanup_{_accountName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                _workService.ExportPortableBackup(backupPath);
                var result = _workService.ApplyTagCleanupPreview(_preview);

                SummaryText.Text =
                    $"已完成：影响 {result.AffectedWorks} 部作品，删除 {result.RemovedTags} 个标签，新增/转换 {result.AddedTags} 个标签。";
                BackupText.Text = $"备份已保存：{backupPath}";
                ApplyButton.IsEnabled = false;

                AppMessageDialog.Show(this, "标签清理完成", $"{SummaryText.Text}\n\n{BackupText.Text}");
                DialogResult = true;
            }
            catch (Exception ex)
            {
                AppMessageDialog.Show(this, "标签清理失败", $"清理未完成：{ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
