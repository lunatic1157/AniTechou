using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class AIBatchAddView : UserControl
    {
        private string _accountName;
        private ObservableCollection<AIBatchWorkItem> _works = new ObservableCollection<AIBatchWorkItem>();

        public AIBatchAddView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            ResultsList.ItemsSource = _works;
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string query = QueryBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("请输入搜索内容");
                return;
            }

            try
            {
                LoadingText.Visibility = Visibility.Visible;
                ResultsList.Visibility = Visibility.Collapsed;
                StatusText.Text = "正在搜索...";

                var config = ConfigManager.Load();
                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    StatusText.Text = "请先在设置中配置API Key";
                    LoadingText.Visibility = Visibility.Collapsed;
                    return;
                }

                var aiService = new AIService(config.ApiKey, config.ApiUrl, config.Model);
                var results = await aiService.BatchSearchWorks(query);

                _works.Clear();
                foreach (var result in results)
                {
                    string info = "";
                    if (!string.IsNullOrEmpty(result.year))
                        info += $"{result.year}年 ";
                    if (!string.IsNullOrEmpty(result.company))
                        info += result.company;

                    _works.Add(new AIBatchWorkItem
                    {
                        Title = result.title,
                        OriginalTitle = result.originalTitle,
                        Type = result.type,
                        Year = result.year,
                        Season = result.season,
                        SourceType = result.sourceType,
                        CoverUrl = result.coverUrl,
                        Company = result.company,
                        Info = info.Trim(),
                        IsSelected = true
                    });
                }

                if (_works.Count == 0)
                {
                    StatusText.Text = "未找到相关作品，请尝试其他关键词";
                }
                else
                {
                    StatusText.Text = $"找到 {_works.Count} 部作品";
                    ResultsList.Visibility = Visibility.Visible;
                }

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"搜索失败：{ex.Message}";
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void ViewDetail_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var work = btn?.Tag as AIBatchWorkItem;
            if (work != null)
            {
                MessageBox.Show($"标题：{work.Title}\n原名：{work.OriginalTitle}\n类型：{work.Type}\n年份：{work.Year}\n制作：{work.Company}",
                                "作品详情", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _works.All(w => w.IsSelected);
            foreach (var work in _works)
            {
                work.IsSelected = !allSelected;
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = _works.Count(w => w.IsSelected);
            SelectedCountText.Text = $"已选择 {count} 项";
        }

        private void AddSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedWorks = _works.Where(w => w.IsSelected).ToList();
            if (selectedWorks.Count == 0)
            {
                MessageBox.Show("请至少选择一部作品");
                return;
            }

            try
            {
                var workService = new WorkService(_accountName);
                int addedCount = 0;

                foreach (var work in selectedWorks)
                {
                    // 转换类型
                    string typeEn = work.Type switch
                    {
                        "动画" => "Anime",
                        "漫画" => "Manga",
                        "轻小说" => "LightNovel",
                        "游戏" => "Game",
                        _ => "Anime"
                    };

                    int workId = workService.AddWork(
                        work.Title,
                        work.OriginalTitle,
                        typeEn,
                        work.Company,
                        work.Year,
                        work.Season ?? "",
                        work.SourceType ?? "原创",
                        "",  // episodes
                        "",  // progress
                        "wish",  // status
                        0,   // rating
                        "",  // synopsis
                        ""   // coverPath
                    );

                    if (workId > 0)
                    {
                        addedCount++;
                        // 异步下载封面
                        if (!string.IsNullOrEmpty(work.CoverUrl))
                        {
                            var currentWork = work;
                            var id = workId;
                            _ = Task.Run(async () =>
                            {
                                await workService.DownloadAndSaveCoverAsync(currentWork.CoverUrl, id);
                            });
                        }
                    }
                }

                MessageBox.Show($"成功添加 {addedCount} 部作品", "添加完成", MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新列表
                Search_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败：{ex.Message}");
            }
        }
    }

    public class AIBatchWorkItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Title { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public string Type { get; set; } = "Anime";
        public string Year { get; set; } = "";
        public string Season { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string CoverUrl { get; set; } = "";
        public string Company { get; set; } = "";
        public string Info { get; set; } = "";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
