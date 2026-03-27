using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AniTechou.Controls;
using AniTechou.Services;

using System.Windows.Threading;
using ToolGood.Words;

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
        private bool _isInitializing = true;
        private bool _isTagsExpanded = false;
        private DispatcherTimer _tagSearchTimer;
        private List<string> _allTags = new List<string>();

        public void SetTagFilter(string tag)
        {
            _allTags = _workService.GetAllTags();
            _selectedTags.Clear();
            _selectedTags.Add(tag);
            FilterAndDisplayTags();
            LoadWorks();
        }

        public WorksView(string accountName, string type, string status)
        {
            InitializeComponent();
            _workService = new WorkService(accountName);
            _currentType = type;
            _currentStatus = status;

            _tagSearchTimer = new DispatcherTimer();
            _tagSearchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _tagSearchTimer.Tick += TagSearchTimer_Tick;

            InitializeFilterOptions();
            LoadYearOptions();
            LoadStudioOptions();
            LoadTagFilters();

            UpdateTypeButtonStyles();
            UpdateStatusButtonStyles();
            UpdateTitleText();
            _isInitializing = false;
            LoadWorks();
        }

        private void InitializeFilterOptions()
        {
            SeasonFilter.ItemsSource = new List<string> { "全部季节", "春", "夏", "秋", "冬" };
            SeasonFilter.SelectedIndex = 0;

            SourceTypeFilter.ItemsSource = new List<string> { "全部原作", "原创", "漫改", "小说改", "游戏改", "其他" };
            SourceTypeFilter.SelectedIndex = 0;

            RatingFilter.ItemsSource = new List<string> { "全部评分", "★ 1-2分", "★★ 3-4分", "★★★ 5-6分", "★★★★ 7-8分", "★★★★★ 9-10分" };
            RatingFilter.SelectedIndex = 0;
        }

        private void LoadYearOptions()
        {
            var years = _workService.GetAllYears();
            var items = new List<string> { "全部年份" };
            items.AddRange(years);
            YearFilter.ItemsSource = items;
            YearFilter.SelectedIndex = 0;
        }

        private void LoadStudioOptions()
        {
            var studios = _workService.GetAllStudios();
            var items = new List<string> { "全部制作" };
            foreach (var studio in studios)
            {
                if (!string.IsNullOrEmpty(studio)) items.Add(studio);
            }
            StudioFilter.ItemsSource = items;
            StudioFilter.SelectedIndex = 0;
        }

        private void LoadTagFilters()
        {
            _allTags = _workService.GetAllTags();
            FilterAndDisplayTags();
        }

        private void FilterAndDisplayTags(string filterText = "")
        {
            string normalizedFilter = (filterText ?? string.Empty).Trim();

            var filteredTags = string.IsNullOrEmpty(normalizedFilter)
                ? _allTags
                : _allTags.Where(t => 
                    t.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) || 
                    WordsHelper.GetPinyin(t).Replace(" ", "").Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                    WordsHelper.GetFirstPinyin(t).Replace(" ", "").Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            bool showExpandButton = false;
            if (!_isTagsExpanded && filteredTags.Count > 24)
            {
                filteredTags = filteredTags.Take(24).ToList();
                showExpandButton = true;
            }

            TagsFilterItemsControl.ItemsSource = filteredTags.Select(tag =>
            {
                var btn = new Button
                {
                    Content = tag,
                    Tag = tag,
                    Style = (Style)FindResource("FilterButton")
                };
                ApplySelectableButtonState(btn, _selectedTags.Contains(tag));
                btn.Click += TagFilter_Click;
                return btn;
            });

            ExpandTagsButton.Visibility = showExpandButton || _isTagsExpanded ? Visibility.Visible : Visibility.Collapsed;
            ExpandTagsButton.Content = _isTagsExpanded ? "收起 ▲" : "展开更多 ▼";
        }

        private void TagSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _tagSearchTimer.Stop();
            _tagSearchTimer.Start();
        }

        private void TagSearchTimer_Tick(object sender, EventArgs e)
        {
            _tagSearchTimer.Stop();
            FilterAndDisplayTags(TagSearchBox.Text.Trim());
        }

        private void ExpandTags_Click(object sender, RoutedEventArgs e)
        {
            _isTagsExpanded = !_isTagsExpanded;
            if (_isTagsExpanded)
            {
                TagsScrollViewer.MaxHeight = 220;
                TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                TagsScrollViewer.MaxHeight = 104;
                TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }

            FilterAndDisplayTags(TagSearchBox.Text.Trim());
        }

        private void TagFilter_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string tag = btn?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            if (_selectedTags.Contains(tag))
            {
                _selectedTags.Remove(tag);
                ApplySelectableButtonState(btn, false);
            }
            else
            {
                _selectedTags.Add(tag);
                ApplySelectableButtonState(btn, true);
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
            ApplySelectableButtonState(AllButton, _currentType == "all");
            ApplySelectableButtonState(AnimeButton, _currentType == "Anime");
            ApplySelectableButtonState(MangaButton, _currentType == "Manga");
            ApplySelectableButtonState(LightNovelButton, _currentType == "LightNovel");
            ApplySelectableButtonState(GameButton, _currentType == "Game");
        }

        private void UpdateStatusButtonStyles()
        {
            ApplySelectableButtonState(AllStatusButton, _currentStatus == "all");
            ApplySelectableButtonState(WishButton, _currentStatus == "wish");
            ApplySelectableButtonState(DoingButton, _currentStatus == "doing");
            ApplySelectableButtonState(DoneButton, _currentStatus == "done");
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
            _currentYear = YearFilter.SelectedItem as string ?? "全部年份";
            LoadWorks();
        }

        private void SeasonFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _currentSeason = SeasonFilter.SelectedItem as string ?? "全部季节";
            LoadWorks();
        }

        private void SourceTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _currentSourceType = SourceTypeFilter.SelectedItem as string ?? "全部原作";
            LoadWorks();
        }

        private void StudioFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _currentStudio = StudioFilter.SelectedItem as string ?? "全部制作";
            LoadWorks();
        }

        private void RatingFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _currentRating = RatingFilter.SelectedItem as string ?? "全部评分";
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
            _isTagsExpanded = false;

            YearFilter.SelectedIndex = 0;
            SeasonFilter.SelectedIndex = 0;
            SourceTypeFilter.SelectedIndex = 0;
            StudioFilter.SelectedIndex = 0;
            RatingFilter.SelectedIndex = 0;
            TagSearchBox.Text = string.Empty;
            TagsScrollViewer.MaxHeight = 104;
            TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            FilterAndDisplayTags();

            LoadWorks();
        }

        private async void LoadWorks()
        {
            try
            {
                if (LoadingText != null) LoadingText.Visibility = Visibility.Visible;
                if (WorksItemsControl != null) WorksItemsControl.Visibility = Visibility.Collapsed;
                if (EmptyText != null) EmptyText.Visibility = Visibility.Collapsed;

                var works = await Task.Run(() => _workService.GetWorksAsync(
                    _currentType, _currentStatus, _currentYear, _currentSeason,
                    _currentSourceType, _currentStudio, _currentRating, _selectedTags));

                Dispatcher.Invoke(() => {
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
                });
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

        private void AddWork_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowAddWorkOptions();
        }

        private static void ApplySelectableButtonState(Button button, bool isSelected)
        {
            if (button == null) return;

            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.BorderBrushProperty);
            if (isSelected)
            {
                button.SetResourceReference(Control.BackgroundProperty, "AccentSoftBrush");
                button.SetResourceReference(Control.BorderBrushProperty, "AccentSoftBrush");
            }
            else
            {
                button.SetResourceReference(Control.BackgroundProperty, "Surface3Brush");
                button.BorderBrush = Brushes.Transparent;
            }
        }
    }
}
