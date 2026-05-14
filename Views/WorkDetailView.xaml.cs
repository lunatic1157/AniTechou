using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;
using AniTechou.Windows;

namespace AniTechou.Views
{
    public partial class WorkDetailView : UserControl
    {
        private int _workId;
        private int _userListId;
        private string _accountName;
        private WorkService _workService;
        private List<string> _currentTags = new List<string>();
        private List<WorkService.NoteListItem> _relatedNotes = new List<WorkService.NoteListItem>();
        private int? _progressTotal;

        public WorkDetailView(int workId, int userListId, string accountName)
        {
            InitializeComponent();
            _workId = workId;
            _userListId = userListId;
            _accountName = accountName;
            _workService = new WorkService(accountName);

            StatusBox.ItemsSource = new List<string> { "想看", "在看", "看过", "搁置", "抛弃" };
            StatusBox.SelectedIndex = 0;
            // 评分已是数字文本框

            LoadData();
        }

        private void LoadData()
        {
            var work = _workService.GetWorkById(_workId);
            var userWork = _workService.GetUserWorkByWorkId(_workId);

            if (work != null)
            {
                _progressTotal = ExtractFirstPositiveInt(work.EpisodesVolumes);
                TitleText.Text = work.Title;
                OriginalTitleText.Text = work.OriginalTitle ?? "";
                TypeText.Text = $"类型：{GetTypeDisplayName(work.Type)}";

                // 根据作品类型动态显示制作公司或作者
                if (work.Type == "Manga" || work.Type == "LightNovel")
                {
                    CompanyText.Text = string.IsNullOrEmpty(work.Author) ? "" : $"作者：{work.Author}";
                    CompanyText.Visibility = string.IsNullOrEmpty(work.Author) ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    CompanyText.Text = string.IsNullOrEmpty(work.Company) ? "" : $"制作：{work.Company}";
                    CompanyText.Visibility = string.IsNullOrEmpty(work.Company) ? Visibility.Collapsed : Visibility.Visible;
                }

                // 显示原作类型和原作名称
                string sourceType = work.SourceType ?? "";
                string originalWork = work.OriginalWork ?? "";
                
                // 原作类型
                if (string.IsNullOrEmpty(sourceType) || sourceType == "无")
                {
                    SourceTypeText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SourceTypeText.Text = $"原作类型：{sourceType}";
                    SourceTypeText.Visibility = Visibility.Visible;
                }

                // 原作
                var originalWorkText = FindName("OriginalWorkText") as TextBlock;
                if (originalWorkText != null)
                {
                    if (string.IsNullOrEmpty(originalWork))
                    {
                        originalWorkText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        originalWorkText.Text = $"原作：{originalWork}";
                        originalWorkText.Visibility = Visibility.Visible;
                    }
                }

                // 显示年份和季度
                string year = work.Year ?? "";
                string season = work.Season ?? "";
                if (!string.IsNullOrEmpty(year))
                {
                    string displaySeason = season switch
                    {
                        "春" => "春",
                        "夏" => "夏",
                        "秋" => "秋",
                        "冬" => "冬",
                        _ => ""
                    };
                    YearText.Text = $"年份：{year} {(string.IsNullOrEmpty(displaySeason) ? "" : displaySeason)}";
                }
                else
                {
                    YearText.Text = "年份：未知";
                }

                EpisodesText.Text = $"集数/卷数：{work.EpisodesVolumes ?? "未知"}";

                string synopsis = work.Synopsis ?? "";
                SynopsisTextBlock.Text = string.IsNullOrEmpty(synopsis) ? "暂无简介" : synopsis;

                // 加载封面
                LoadCoverImage(work.CoverPath);

                // 设置外部链接
                SetupExternalLinks(work);
            }

            // 加载个人状态
            if (userWork != null)
            {
                // 设置状态
                string statusText = userWork.Status switch
                {
                    "wish" => "想看",
                    "doing" => "在看",
                    "done" => "看过",
                    "on_hold" => "搁置",
                    "dropped" => "抛弃",
                    _ => "想看"
                };
                StatusBox.SelectedItem = statusText;
                if (StatusBox.SelectedItem == null) StatusBox.SelectedIndex = 0;
                
                // 设置进度
                ProgressBox.Text = NormalizeProgressInput(userWork.Progress ?? "");
                
                // 设置评分 (1-10 数字)
                double ratingVal = userWork.Rating;
                System.Diagnostics.Debug.WriteLine($"[Rating] 加载: userWork.Rating={userWork.Rating} → 显示为 '{ratingVal:F1}'");
                RatingBox.Text = ratingVal > 0 ? ratingVal.ToString("F1") : "";

                // 设置开始/完成日期
                if (DateTime.TryParse(userWork.StartedDate, out var started))
                    StartedDatePicker.SelectedDate = started;
                else
                    StartedDatePicker.SelectedDate = null;

                if (DateTime.TryParse(userWork.FinishedDate, out var finished))
                    FinishedDatePicker.SelectedDate = finished;
                else
                    FinishedDatePicker.SelectedDate = null;
            }

            // 加载标签
            _currentTags = _workService.GetWorkTags(_workId);
            RefreshTagsPanel();

            // 加载关联作品
            LoadRelatedWorks();

            // 加载关联笔记
            LoadRelatedNotes();
        }

        private void SetupExternalLinks(Models.WorkInfo work)
        {
            ExternalLinksWrap.Children.Clear();
            bool hasLinks = false;

            if (!string.IsNullOrEmpty(work.BangumiId))
            {
                var btn = CreateLinkButton("Bangumi 番组计划", $"https://bgm.tv/subject/{work.BangumiId}");
                ExternalLinksWrap.Children.Add(btn);
                hasLinks = true;
            }

            if (!string.IsNullOrEmpty(work.MALId))
            {
                var btn = CreateLinkButton("MyAnimeList", $"https://myanimelist.net/anime/{work.MALId}");
                ExternalLinksWrap.Children.Add(btn);
                hasLinks = true;
            }

            if (!string.IsNullOrEmpty(work.AniListId))
            {
                var btn = CreateLinkButton("AniList", $"https://anilist.co/anime/{work.AniListId}");
                ExternalLinksWrap.Children.Add(btn);
                hasLinks = true;
            }

            ExternalLinksPanel.Visibility = hasLinks ? Visibility.Visible : Visibility.Collapsed;
        }

        private Button CreateLinkButton(string label, string url)
        {
            var btn = new Button
            {
                Content = $"🔗 {label}",
                Width = 160,
                Height = 34,
                Margin = new Thickness(0, 0, 10, 6),
                Tag = url
            };
            btn.Style = (Style)FindResource("AppSecondaryButtonStyle");
            btn.Click += (s, e) =>
            {
                if ((s as Button)?.Tag is string link)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkDetail] 打开链接失败: {ex.Message}");
                    }
                }
            };
            return btn;
        }

        private void LoadRelatedWorks()
        {
            var relatedWorks = _workService.GetRelatedWorks(_workId);
            RelationsList.ItemsSource = relatedWorks;
            NoRelationsText.Visibility = relatedWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RelationsList.Visibility = relatedWorks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddRelation_Click(object sender, RoutedEventArgs e)
        {
            // 获取所有作品供选择（排除自己和已关联的）
            var allWorks = _workService.GetAllWorksForSearch();
            var relatedWorks = _workService.GetRelatedWorks(_workId);
            var relatedIds = relatedWorks.Select(w => w.Id).ToList();
            relatedIds.Add(_workId);

            var availableWorks = allWorks.Where(w => !relatedIds.Contains(w.Id)).ToList();

            if (availableWorks.Count == 0)
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "没有可关联的其他作品。");
                return;
            }

            var dialog = new Window
            {
                Title = "添加关联作品",
                Width = 460,
                Height = 560,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                FontFamily = FontFamily
            };
            dialog.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");

            var panel = new StackPanel { Margin = new Thickness(18) };
            
            var header = new TextBlock
            {
                Text = "搜索并选择要关联的作品",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var searchBox = new TextBox 
            { 
                Margin = new Thickness(0, 0, 0, 10),
                Height = 38
            };
            searchBox.Style = (Style)FindResource("AppTextBoxStyle");
            
            var listBox = new ListBox
            {
                ItemsSource = availableWorks,
                DisplayMemberPath = "Title",
                Height = 350,
                Margin = new Thickness(0, 0, 0, 12),
                BorderThickness = new Thickness(1)
            };
            listBox.SetResourceReference(Control.BackgroundProperty, "Surface1Brush");
            listBox.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            listBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");

            searchBox.TextChanged += (s, args) =>
            {
                string text = searchBox.Text.ToLower();
                listBox.ItemsSource = string.IsNullOrEmpty(text) 
                    ? availableWorks 
                    : availableWorks.Where(w => (w.Title != null && w.Title.ToLower().Contains(text)) || (w.OriginalTitle != null && w.OriginalTitle.ToLower().Contains(text))).ToList();
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "确定", Width = 88, Height = 36, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("AppPrimaryButtonStyle") };
            var cancelBtn = new Button { Content = "取消", Width = 88, Height = 36, Style = (Style)FindResource("AppSecondaryButtonStyle") };

            okBtn.Click += (s, args) =>
            {
                if (listBox.SelectedItem is WorkService.WorkCardData selectedWork)
                {
                    if (_workService.AddWorkRelation(_workId, selectedWork.Id))
                    {
                        LoadRelatedWorks();
                    }
                }
                dialog.Close();
            };

            cancelBtn.Click += (s, args) => dialog.Close();

            listBox.MouseDoubleClick += (s, args) =>
            {
                if (listBox.SelectedItem is WorkService.WorkCardData selectedWork)
                {
                    if (_workService.AddWorkRelation(_workId, selectedWork.Id))
                    {
                        LoadRelatedWorks();
                    }
                    dialog.Close();
                }
            };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(header);
            panel.Children.Add(searchBox);
            panel.Children.Add(listBox);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;

            dialog.Loaded += (s, args) => searchBox.Focus();

            dialog.ShowDialog();
        }

        private void RemoveRelation_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is int targetId)
            {
                if (_workService.RemoveWorkRelation(_workId, targetId))
                {
                    LoadRelatedWorks();
                }
            }
            e.Handled = true; // 阻止事件冒泡到卡片点击
        }

        private void RelatedWork_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is WorkService.WorkCardData work)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowDetailView(new WorkDetailView(work.Id, _userListId, _accountName));
            }
        }

        private void LoadRelatedNotes()
        {
            var allNotes = _workService.GetAllNotes();
            _relatedNotes = allNotes.Where(n => n.WorkIds.Contains(_workId)).ToList();

            foreach (var note in _relatedNotes)
            {
                note.DisplayTitle = string.IsNullOrEmpty(note.Title) ? "无标题" : note.Title;
                note.CreatedTimeDisplay = note.CreatedTime.ToString("yyyy-MM-dd");
            }

            NotesList.ItemsSource = _relatedNotes;
            NoNotesText.Visibility = _relatedNotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NotesList.Visibility = _relatedNotes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshTagsPanel()
        {
            TagsPanel.Children.Clear();
            foreach (string tag in _currentTags)
            {
                var tagBorder = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5)
                };
                tagBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                var text = new TextBlock { Text = tag, FontSize = 12 };
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                stack.Children.Add(text);
                var removeBtn = new Button
                {
                    Content = "✕",
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(5, 0, 0, 0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderBrush = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(0),
                    Tag = tag
                };
                removeBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                removeBtn.Click += (s, e) => RemoveTag(tag);
                stack.Children.Add(removeBtn);
                tagBorder.Child = stack;
                TagsPanel.Children.Add(tagBorder);
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            string newTag = NewTagBox.Text.Trim();
            if (string.IsNullOrEmpty(newTag)) return;

            if (_workService.AddWorkTag(_workId, newTag))
            {
                if (!_currentTags.Contains(newTag))
                {
                    _currentTags.Add(newTag);
                }
                RefreshTagsPanel();
                NewTagBox.Text = "";
            }
            else
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "添加标签失败或标签已存在");
            }
        }

        private void RemoveTag(string tag)
        {
            if (_workService.RemoveWorkTag(_workId, tag))
            {
                _currentTags.Remove(tag);
                RefreshTagsPanel();
            }
        }

        private void NoteItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is WorkService.NoteListItem note)
            {
                var fullNote = _workService.GetNoteById(note.Id);
                if (fullNote != null)
                {
                    var editor = new NoteEditor(_accountName, fullNote, EditorSource.WorkDetail, _workId);
                    editor.NoteSaved += () => LoadData();
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.ShowDetailView(editor);
                }
            }
        }

        private void LoadCoverImage(string coverPath)
        {
            if (string.IsNullOrEmpty(coverPath))
            {
                CoverImage.Source = null;
                return;
            }

            try
            {
                // 尝试直接路径
                if (System.IO.File.Exists(coverPath))
                {
                    SetImageSource(coverPath);
                    return;
                }
                
                // 尝试相对路径
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, coverPath);
                if (System.IO.File.Exists(fullPath))
                {
                    SetImageSource(fullPath);
                    return;
                }
                
                // 尝试应用程序数据目录
                string appDataPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AniTechou",
                    "covers",
                    System.IO.Path.GetFileName(coverPath));
                if (System.IO.File.Exists(appDataPath))
                {
                    SetImageSource(appDataPath);
                    return;
                }
                
                CoverImage.Source = null;
            }
            catch
            {
                CoverImage.Source = null;
            }
        }

        private void SetImageSource(string path)
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            CoverImage.Source = bitmap;
        }

        private string GetTypeDisplayName(string type)
        {
            return type switch
            {
                "Anime" => "动画",
                "Manga" => "漫画",
                "LightNovel" => "轻小说",
                "Game" => "游戏",
                _ => type
            };
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取状态
                string statusText = StatusBox.SelectedItem as string ?? "在看";
                string statusEn = statusText switch
                {
                    "想看" => "wish",
                    "在看" => "doing",
                    "看过" => "done",
                    "搁置" => "on_hold",
                    "抛弃" => "dropped",
                    _ => "wish"
                };
                
                // 获取评分 (1-10)
                double.TryParse(RatingBox.Text.Trim(), out double rating);
                System.Diagnostics.Debug.WriteLine($"[Rating] 输入框: '{RatingBox.Text}' → 解析: {rating}");
                if (rating < 0) rating = 0;
                if (rating > 10) rating = 10;
                
                // 进度
                string progress = NormalizeProgressInput(ProgressBox.Text);
                ProgressBox.Text = progress;

                // 开始/完成日期
                string startedDate = StartedDatePicker.SelectedDate.HasValue
                    ? StartedDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : null;
                string finishedDate = FinishedDatePicker.SelectedDate.HasValue
                    ? FinishedDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") : null;

                // 更新数据库
                _workService.UpdateUserWork(_userListId, statusEn, progress, rating, startedDate, finishedDate);
                
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "成功", "更新成功！");
                
                // 重新加载数据以确认更新
                LoadData();
            }
            catch (Exception ex)
            {
                Windows.AppMessageDialog.Show(Application.Current.MainWindow, "错误", $"更新失败：{ex.Message}");
            }
        }

        private string NormalizeProgressInput(string rawProgress)
        {
            string progress = (rawProgress ?? "").Trim();
            if (string.IsNullOrEmpty(progress)) return "";

            if (progress.Contains("/"))
            {
                var match = Regex.Match(progress, @"^\s*(\d+)\s*/\s*(\d*)\s*$");
                if (match.Success)
                {
                    string current = match.Groups[1].Value;
                    string total = match.Groups[2].Value;
                    if (string.IsNullOrEmpty(total) && _progressTotal.HasValue)
                    {
                        total = _progressTotal.Value.ToString();
                    }
                    return string.IsNullOrEmpty(total) ? current : $"{current}/{total}";
                }

                return progress;
            }

            if (_progressTotal.HasValue && int.TryParse(progress, out int currentValue))
            {
                return $"{currentValue}/{_progressTotal.Value}";
            }

            return progress;
        }

        private static int? ExtractFirstPositiveInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var match = Regex.Match(text, @"\d+");
            if (!match.Success) return null;

            return int.TryParse(match.Value, out int value) && value > 0 ? value : null;
        }

        private void EditInfo_Click(object sender, RoutedEventArgs e)
        {
            var editDialog = new EditWorkInfoWindow(_workId, _accountName);
            editDialog.Owner = Application.Current.MainWindow;
            if (editDialog.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void DeleteWork_Click(object sender, RoutedEventArgs e)
        {
            if (Windows.AppMessageDialog.Show(Application.Current.MainWindow, "确认删除", $"确定要删除《{TitleText.Text}》吗？相关的所有笔记关联也将被移除。", true))
            {
                if (_workService.DeleteWork(_workId))
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "成功", "删除成功！");
                    Back_Click(null, null);
                }
                else
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "失败", "删除失败，请稍后再试。");
                }
            }
        }

        private void WriteNote_Click(object sender, RoutedEventArgs e)
        {
            var editor = new NoteEditor(_accountName, null, EditorSource.WorkDetail, _workId);
            // 自动关联当前作品
            editor.PreSelectWork(_workId);
            editor.NoteSaved += () => LoadData();
            editor.NoteCancelled += () => LoadData();
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowDetailView(editor);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.RefreshCurrentView();
        }
    }
}
