using System;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class EditWorkInfoWindow : Window
    {
        private int _workId;
        private string _accountName;
        private WorkService _workService;

        public EditWorkInfoWindow(int workId, string accountName)
        {
            InitializeComponent();
            _workId = workId;
            _accountName = accountName;
            _workService = new WorkService(accountName);
            LoadData();
        }

        private void LoadData()
        {
            var work = _workService.GetWorkById(_workId);
            if (work != null)
            {
                TitleBox.Text = work.Title;
                OriginalTitleBox.Text = work.OriginalTitle ?? "";

                // 设置类型选中项 (兼容历史数据中可能存在的中/英文存储)
                string displayType = work.Type switch
                {
                    "Anime" => "动画",
                    "动画" => "动画",
                    "Manga" => "漫画",
                    "漫画" => "漫画",
                    "LightNovel" => "轻小说",
                    "轻小说" => "轻小说",
                    "Game" => "游戏",
                    "游戏" => "游戏",
                    _ => "动画"
                };

                for (int i = 0; i < TypeBox.Items.Count; i++)
                {
                    if (TypeBox.Items[i] is ComboBoxItem item)
                    {
                        if (item.Content.ToString() == displayType)
                        {
                            TypeBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                // 动态更新标签文本
                UpdateDynamicLabels(displayType);

                var normalizedType = displayType switch
                {
                    "动画" => "Anime",
                    "漫画" => "Manga",
                    "轻小说" => "LightNovel",
                    "游戏" => "Game",
                    _ => "Anime"
                };

                CompanyBox.Text = normalizedType == "Manga" || normalizedType == "LightNovel" ? (work.Author ?? "") : (work.Company ?? "");
                var originalWorkBox = FindName("OriginalWorkBox") as TextBox;
                if (originalWorkBox != null)
                {
                    originalWorkBox.Text = work.OriginalWork ?? "";
                }

                // 设置年份
                YearBox.Text = work.Year ?? "";

                // 设置季度
                string season = work.Season ?? "";
                int seasonIndex = season switch
                {
                    "春" => 1,
                    "夏" => 2,
                    "秋" => 3,
                    "冬" => 4,
                    _ => 0
                };
                SeasonBox.SelectedIndex = seasonIndex;

                // 设置原作类型
                string sourceType = work.SourceType ?? "";
                if (string.IsNullOrEmpty(sourceType)) sourceType = "无";

                for (int i = 0; i < SourceTypeBox.Items.Count; i++)
                {
                    if (SourceTypeBox.Items[i] is ComboBoxItem item)
                    {
                        if (item.Content.ToString() == sourceType)
                        {
                            SourceTypeBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                EpisodesBox.Text = work.EpisodesVolumes ?? "";
                SynopsisBox.Text = work.Synopsis ?? "";
                CoverPathBox.Text = work.CoverPath ?? "";
            }
        }

        private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeBox.SelectedItem is ComboBoxItem selectedItem)
            {
                UpdateDynamicLabels(selectedItem.Content.ToString());
            }
        }

        private void UpdateDynamicLabels(string type)
        {
            var companyLabel = FindName("CompanyLabel") as TextBlock;
            if (companyLabel != null)
            {
                if (type == "Manga" || type == "LightNovel" || type == "漫画" || type == "轻小说")
                {
                    companyLabel.Text = "作者：";
                }
                else
                {
                    companyLabel.Text = "制作公司：";
                }
            }
        }

        private void BrowseCover_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择封面图片",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                CoverPathBox.Text = dialog.FileName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = TypeBox.SelectedItem as ComboBoxItem;
                string type = selectedItem?.Content?.ToString() switch
                {
                    "动画" => "Anime",
                    "漫画" => "Manga",
                    "轻小说" => "LightNovel",
                    "游戏" => "Game",
                    _ => "Anime"
                };

                // 分别获取年份和季度
                string year = YearBox.Text.Trim();
                string season = (SeasonBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                string sourceType = (SourceTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                if (sourceType == "无") sourceType = "";

                string company = type == "Anime" || type == "Game" ? CompanyBox.Text.Trim() : "";
                string author = type == "Manga" || type == "LightNovel" ? CompanyBox.Text.Trim() : "";
                string originalWork = "";
                var originalWorkBox = FindName("OriginalWorkBox") as TextBox;
                if (originalWorkBox != null)
                {
                    originalWork = originalWorkBox.Text.Trim();
                }

                bool success = _workService.UpdateWorkInfo(
                    _workId,
                    TitleBox.Text.Trim(),
                    OriginalTitleBox.Text.Trim(),
                    type,
                    company,
                    year,
                    season,
                    sourceType,
                    EpisodesBox.Text.Trim(),
                    SynopsisBox.Text.Trim(),
                    CoverPathBox.Text.Trim(),
                    author,
                    originalWork
                );

                if (success)
                {
                    MessageBox.Show("保存成功！");
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("保存失败，请重试");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
