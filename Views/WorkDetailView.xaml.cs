using System;
using System.Collections.Generic;
using System.Linq;
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

        public WorkDetailView(int workId, int userListId, string accountName)
        {
            InitializeComponent();
            _workId = workId;
            _userListId = userListId;
            _accountName = accountName;
            _workService = new WorkService(accountName);

            LoadData();
        }

        private void LoadData()
        {
            var work = _workService.GetWorkById(_workId);
            var userWork = _workService.GetUserWorkByWorkId(_workId);

            if (work != null)
            {
                TitleText.Text = work.Title;
                OriginalTitleText.Text = work.OriginalTitle ?? "";
                TypeText.Text = $"类型：{GetTypeDisplayName(work.Type)}";

                // 显示原作类型
                string sourceType = work.SourceType ?? "";
                SourceTypeText.Text = string.IsNullOrEmpty(sourceType) ? "" : $"原作类型：{sourceType}";

                CompanyText.Text = $"制作：{work.Company ?? "未知"}";

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
                    _ => "想看"
                };
                
                for (int i = 0; i < StatusBox.Items.Count; i++)
                {
                    var item = StatusBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == statusText)
                    {
                        StatusBox.SelectedIndex = i;
                        break;
                    }
                }
                
                // 设置进度
                ProgressBox.Text = userWork.Progress ?? "";
                
                // 设置评分
                int ratingIndex = userWork.Rating switch
                {
                    2 => 1,
                    4 => 2,
                    6 => 3,
                    8 => 4,
                    10 => 5,
                    _ => 0
                };
                RatingBox.SelectedIndex = ratingIndex;
            }

            // 加载标签
            _currentTags = _workService.GetWorkTags(_workId);
            RefreshTagsPanel();

            // 加载关联笔记
            LoadRelatedNotes();
        }

        private void LoadRelatedNotes()
        {
            var allNotes = _workService.GetAllNotes();
            _relatedNotes = allNotes.Where(n => n.WorkIds.Contains(_workId)).ToList();

            foreach (var note in _relatedNotes)
            {
                note.DisplayTitle = string.IsNullOrEmpty(note.Title) ? "无标题" : note.Title;
                note.Preview = string.IsNullOrEmpty(note.Content) ? "" :
                    (note.Content.Length > 80 ? note.Content.Substring(0, 80) + "..." : note.Content);
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
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 213, 192)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5)
                };
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(new TextBlock { Text = tag, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 78, 61)) });
                var removeBtn = new Button
                {
                    Content = "✕",
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(5, 0, 0, 0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = tag
                };
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
                MessageBox.Show("添加标签失败或标签已存在");
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
                var statusItem = StatusBox.SelectedItem as ComboBoxItem;
                string statusText = statusItem?.Content.ToString() ?? "在看";
                string statusEn = statusText switch
                {
                    "想看" => "wish",
                    "在看" => "doing",
                    "看过" => "done",
                    _ => "wish"
                };
                
                // 获取评分
                var ratingItem = RatingBox.SelectedItem as ComboBoxItem;
                string ratingText = ratingItem?.Content.ToString() ?? "未评分";
                int rating = ratingText switch
                {
                    "★☆☆☆☆ (1-2分)" => 2,
                    "★★☆☆☆ (3-4分)" => 4,
                    "★★★☆☆ (5-6分)" => 6,
                    "★★★★☆ (7-8分)" => 8,
                    "★★★★★ (9-10分)" => 10,
                    _ => 0
                };
                
                // 进度
                string progress = ProgressBox.Text.Trim();
                
                // 更新数据库
                _workService.UpdateUserWork(_userListId, statusEn, progress, rating);
                
                MessageBox.Show("更新成功！");
                
                // 重新加载数据以确认更新
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败：{ex.Message}");
            }
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