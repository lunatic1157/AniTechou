using System;
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
                MessageBox.Show("请输入作品标题");
                return;
            }

            try
            {
                // 类型转换
                string typeText = ((ComboBoxItem)TypeBox.SelectedItem).Content.ToString();
                string typeEn = typeText switch
                {
                    "动画" => "Anime",
                    "漫画" => "Manga",
                    "轻小说" => "LightNovel",
                    "游戏" => "Game",
                    _ => "Anime"
                };

                // 状态转换
                string status = ((ComboBoxItem)StatusBox.SelectedItem).Content.ToString();
                string statusEn = status switch
                {
                    "想看" => "wish",
                    "在看" => "doing",
                    "看过" => "done",
                    _ => "wish"
                };

                // 评分转换
                string ratingText = ((ComboBoxItem)RatingBox.SelectedItem).Content.ToString();
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
                string season = ((ComboBoxItem)SeasonBox.SelectedItem).Content.ToString();

                // 原作类型
                string sourceType = ((ComboBoxItem)SourceTypeBox.SelectedItem)?.Content.ToString() ?? "";
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
                    MessageBox.Show("添加成功！");
                    WorkAdded?.Invoke();
                }
                else
                {
                    MessageBox.Show("保存失败，请检查输入信息");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}");
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