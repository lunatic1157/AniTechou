using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AniTechou.Controls;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class WorksView : UserControl
    {
        private WorkService _workService;
        private string _currentType = "all";
        private string _currentStatus = "all";
        private string _currentYear = "全部年份";
        private string _currentSeason = "全部季节";
        private string _currentSourceType = "全部原作";
        private string _currentStudio = "全部制作";
        private string _currentRating = "全部评分";
        private List<string> _selectedTags = new List<string>();
        private bool _isGridView = true;
        private bool _isInitializing = true;

        private SolidColorBrush _selectedBrush = new SolidColorBrush(Color.FromRgb(224, 213, 192));
        private SolidColorBrush _normalBrush = new SolidColorBrush(Color.FromRgb(240, 233, 221));

        /// <summary>
        /// 设置标签筛选（用于从个人主页跳转到作品列表）
        /// </summary>
        public void SetTagFilter(string tag)
        {
            // 先重新加载标签（确保包含目标标签）
            LoadTagFilters();

            // 选中目标标签
            _selectedTags.Clear();
            _selectedTags.Add(tag);

            // 更新按钮样式
            foreach (var child in TagsFilterPanel.Children)
            {
                if (child is Button btn)
                {
                    if (btn.Tag?.ToString() == tag)
                    {
                        btn.Background = _selectedBrush;
                    }
                    else
                    {
                        btn.Background = _normalBrush;
                    }
                }
            }

            // 加载数据
            LoadWorks();
        }

        public WorksView(string accountName, string type, string status)
        {
            InitializeComponent();
            _workService = new WorkService(accountName);
            _currentType = type;
            _currentStatus = status;

            // 加载年份选项
            LoadYearOptions();
            // 加载制作公司选项
            LoadStudioOptions();
            // 加载标签筛选
            LoadTagFilters();

            // 设置选中状态
            UpdateTypeButtonStyles();
            UpdateStatusButtonStyles();

            // 设置标题
            UpdateTitleText();

            // 设置初始按钮样式
            UpdateButtonStyles();

            // 标记初始化完成
            _isInitializing = false;

            // 加载数据
            LoadWorks();
        }

        private void LoadYearOptions()
        {
            var years = _workService.GetAllYears();
            YearFilter.Items.Clear();
            YearFilter.Items.Add(new ComboBoxItem { Content = "全部年份", IsSelected = true });
            foreach (var year in years)
            {
                YearFilter.Items.Add(new ComboBoxItem { Content = year });
            }
        }

        private void LoadStudioOptions()
        {
            var studios = _workService.GetAllStudios();
            StudioFilter.Items.Clear();
            StudioFilter.Items.Add(new ComboBoxItem { Content = "全部制作", IsSelected = true });
            foreach (var studio in studios)
            {
                if (!string.IsNullOrEmpty(studio))
                    StudioFilter.Items.Add(new ComboBoxItem { Content = studio });
            }
        }

        private void LoadTagFilters()
        {
            var tags = _workService.GetAllTags();
            TagsFilterPanel.Children.Clear();

            foreach (var tag in tags)
            {
                var btn = new Button
                {
                    Content = tag,
                    Tag = tag,
                    Style = (Style)FindResource("FilterButton"),
                    Background = new SolidColorBrush(Color.FromRgb(240, 233, 221))
                };
                btn.Click += TagFilter_Click;
                TagsFilterPanel.Children.Add(btn);
            }
        }

        private void TagFilter_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string tag = btn?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            if (_selectedTags.Contains(tag))
            {
                _selectedTags.Remove(tag);
                btn.Background = new SolidColorBrush(Color.FromRgb(240, 233, 221));
            }
            else
            {
                _selectedTags.Add(tag);
                btn.Background = _selectedBrush;
            }
            LoadWorks();
        }

        private void UpdateTitleText()
        {
            string typeName = _currentType switch
            {
                "Anime" => "动画",
                "Manga" => "漫画",
                "LightNovel" => "轻小说",
                "Game" => "游戏",
                _ => "全部作品"
            };

            string statusName = _currentStatus switch
            {
                "wish" => " · 想看",
                "doing" => " · 在看",
                "done" => " · 看过",
                _ => ""
            };

            TitleText.Text = $"{typeName}{statusName}";
        }

        private void UpdateTypeButtonStyles()
        {
            AllButton.Background = _currentType == "all" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            AnimeButton.Background = _currentType == "Anime" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            MangaButton.Background = _currentType == "Manga" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            LightNovelButton.Background = _currentType == "LightNovel" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            GameButton.Background = _currentType == "Game" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
        }

        private void UpdateStatusButtonStyles()
        {
            AllStatusButton.Background = _currentStatus == "all" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            WishButton.Background = _currentStatus == "wish" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            DoingButton.Background = _currentStatus == "doing" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
            DoneButton.Background = _currentStatus == "done" ? _selectedBrush : new SolidColorBrush(Color.FromRgb(240, 233, 221));
        }

        private void TypeFilter_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            _currentType = btn?.Tag?.ToString() ?? "all";
            UpdateTypeButtonStyles();
            UpdateTitleText();
            LoadWorks();
        }

        private void StatusFilter_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            _currentStatus = btn?.Tag?.ToString() ?? "all";
            UpdateStatusButtonStyles();
            UpdateTitleText();
            LoadWorks();
        }

        private void YearFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var item = YearFilter.SelectedItem as ComboBoxItem;
            _currentYear = item?.Content?.ToString() ?? "全部年份";
            LoadWorks();
        }

        private void SeasonFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var item = SeasonFilter.SelectedItem as ComboBoxItem;
            _currentSeason = item?.Content?.ToString() ?? "全部季节";
            LoadWorks();
        }

        private void SourceTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var item = SourceTypeFilter.SelectedItem as ComboBoxItem;
            _currentSourceType = item?.Content?.ToString() ?? "全部原作";
            LoadWorks();
        }

        private void StudioFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var item = StudioFilter.SelectedItem as ComboBoxItem;
            _currentStudio = item?.Content?.ToString() ?? "全部制作";
            LoadWorks();
        }

        private void RatingFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var item = RatingFilter.SelectedItem as ComboBoxItem;
            _currentRating = item?.Content?.ToString() ?? "全部评分";
            LoadWorks();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _currentYear = "全部年份";
            _currentSeason = "全部季节";
            _currentSourceType = "全部原作";
            _currentStudio = "全部制作";
            _currentRating = "全部评分";
            _selectedTags.Clear();

            YearFilter.SelectedIndex = 0;
            SeasonFilter.SelectedIndex = 0;
            SourceTypeFilter.SelectedIndex = 0;
            StudioFilter.SelectedIndex = 0;
            RatingFilter.SelectedIndex = 0;

            // 重置标签按钮样式
            foreach (var child in TagsFilterPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(240, 233, 221));
                }
            }

            LoadWorks();
        }

        private async void LoadWorks()
        {
            try
            {
                if (LoadingText != null) LoadingText.Visibility = Visibility.Visible;
                if (WorksItemsControl != null) WorksItemsControl.Visibility = Visibility.Collapsed;
                if (EmptyText != null) EmptyText.Visibility = Visibility.Collapsed;

                var works = await _workService.GetWorksAsync(
                    _currentType, _currentStatus, _currentYear, _currentSeason,
                    _currentSourceType, _currentStudio, _currentRating, _selectedTags);

                if (CountText != null) CountText.Text = $"({works.Count})";

                if (works.Count == 0)
                {
                    if (WorksItemsControl != null) WorksItemsControl.Visibility = Visibility.Collapsed;
                    if (EmptyText != null) EmptyText.Visibility = Visibility.Visible;
                }
                else
                {
                    if (WorksItemsControl != null)
                    {
                        WorksItemsControl.Visibility = Visibility.Visible;
                        WorksItemsControl.ItemsSource = works;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}");
            }
            finally
            {
                if (LoadingText != null)
                    LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void OnWorkCardClick(object sender, RoutedEventArgs e)
        {
            var card = sender as WorkCard;
            if (card != null)
            {
                // 获取完整的作品信息
                var work = _workService.GetWorkById(card.WorkId);
                var userWork = _workService.GetUserWorkByWorkId(card.WorkId);

                if (work != null && userWork != null)
                {
                    var detailView = new WorkDetailView(card.WorkId, userWork.Id, _workService.GetCurrentAccount());
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.ShowDetailView(detailView);
                }
            }
        }

        private void SetGridView_Click(object sender, RoutedEventArgs e)
        {
            if (_isGridView) return;
            _isGridView = true;

            // 切换面板模板
            WorksItemsControl.ItemsPanel = (ItemsPanelTemplate)FindResource("GridPanel");
            UpdateButtonStyles();
        }

        private void SetListView_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGridView) return;
            _isGridView = false;

            // 切换面板模板
            WorksItemsControl.ItemsPanel = (ItemsPanelTemplate)FindResource("ListPanel");
            UpdateButtonStyles();
        }

        private void UpdateButtonStyles()
        {
            if (_isGridView)
            {
                GridButton.Background = _selectedBrush;
                ListButton.Background = _normalBrush;
            }
            else
            {
                GridButton.Background = _normalBrush;
                ListButton.Background = _selectedBrush;
            }
        }

        private void AddWork_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowAddWorkOptions();
        }
    }
}
