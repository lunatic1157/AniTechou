using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class AddWorkForm : UserControl
    {
        private string _accountName;
        private string _selectedCoverPath = "";
        public event Action WorkAdded;

        public AddWorkForm(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            InitializeOptions();
        }

        private void InitializeOptions()
        {
            TypeBox.ItemsSource = new List<string> { "动画", "漫画", "轻小说", "游戏" };
            TypeBox.SelectedIndex = 0;

            SeasonBox.ItemsSource = new List<string> { "春", "夏", "秋", "冬", "" };
            SeasonBox.SelectedIndex = 0;

            SourceTypeBox.ItemsSource = new List<string> { "无", "原创", "漫改", "小说改", "游戏改", "其他" };
            SourceTypeBox.SelectedIndex = 0;

            StatusBox.ItemsSource = new List<string> { "想看", "在看", "看过" };
            StatusBox.SelectedIndex = 0;

            RatingBox.ItemsSource = new List<string>
            {
                "未评分",
                "★☆☆☆☆ (1-2分)",
                "★★☆☆☆ (3-4分)",
                "★★★☆☆ (5-6分)",
                "★★★★☆ (7-8分)",
                "★★★★★ (9-10分)"
            };
            RatingBox.SelectedIndex = 0;
        }

        private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeBox.SelectedItem is string selectedItem)
            {
                UpdateDynamicLabels(selectedItem);
            }
        }

        private void UpdateDynamicLabels(string type)
        {
            var companyLabel = FindName("CompanyLabel") as TextBlock;
            if (companyLabel != null)
            {
                if (type == "漫画" || type == "轻小说")
                {
                    companyLabel.Text = "作者";
                }
                else
                {
                    companyLabel.Text = "制作公司";
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "请输入作品标题");
                return;
            }

            try
            {
                // 类型转换
                string typeText = TypeBox.SelectedItem as string ?? "动画";
                string typeEn = typeText switch
                {
                    "动画" => "Anime",
                    "漫画" => "Manga",
                    "轻小说" => "LightNovel",
                    "游戏" => "Game",
                    _ => "Anime"
                };

                // 状态转换
                string status = StatusBox.SelectedItem as string ?? "想看";
                string statusEn = status switch
                {
                    "想看" => "wish",
                    "在看" => "doing",
                    "看过" => "done",
                    _ => "wish"
                };

                // 评分转换
                string ratingText = RatingBox.SelectedItem as string ?? "未评分";
                int rating = ratingText switch
                {
                    "★☆☆☆☆ (1-2分)" => 2,
                    "★★☆☆☆ (3-4分)" => 4,
                    "★★★☆☆ (5-6分)" => 6,
                    "★★★★☆ (7-8分)" => 8,
                    "★★★★★ (9-10分)" => 10,
                    _ => 0
                };

                // 年份季度
                string year = YearBox.Text.Trim();
                string season = SeasonBox.SelectedItem as string ?? "";

                // 原作类型
                string sourceType = SourceTypeBox.SelectedItem as string ?? "";
                if (sourceType == "无") sourceType = "";

                string company = typeEn == "Anime" || typeEn == "Game" ? CompanyBox.Text.Trim() : "";
                string author = typeEn == "Manga" || typeEn == "LightNovel" ? CompanyBox.Text.Trim() : "";
                string originalWork = "";
                var originalWorkBox = FindName("OriginalWorkBox") as TextBox;
                if (originalWorkBox != null)
                {
                    originalWork = originalWorkBox.Text.Trim();
                }

                var workService = new WorkService(_accountName);

                int workId = workService.AddWork(
                    title,
                    OriginalTitleBox.Text.Trim(),
                    typeEn,                    // 英文类型
                    company,
                    year,
                    season,
                    sourceType,                // 原作类型
                    EpisodesBox.Text.Trim(),
                    ProgressBox.Text.Trim(),
                    statusEn,                  // 英文状态
                    rating,
                    SynopsisBox.Text.Trim(),
                    _selectedCoverPath,
                    author,
                    originalWork
                );

                if (workId > 0)
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "成功", "添加成功！");
                    WorkAdded?.Invoke();
                }
                else
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "失败", "保存失败，请检查输入信息");
                }
            }
            catch (Exception ex)
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "错误", $"保存失败：{ex.Message}");
            }
        }

        private void SelectCover_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择封面图片",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedCoverPath = dialog.FileName;
                CoverPathText.Text = System.IO.Path.GetFileName(_selectedCoverPath);
                CoverPlaceholder.Visibility = Visibility.Collapsed;

                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(_selectedCoverPath));
                    CoverPreview.Source = bitmap;
                    CoverPreview.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 图片加载失败
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            WorkAdded?.Invoke();
        }
    }
}
