using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class NotesView : UserControl
    {
        private string _accountName;
        private List<WorkService.NoteListItem> _allNotes;
        private List<WorkService.NoteListItem> _filteredNotes;
        private string _currentTagFilter = "";
        private string _currentSearchText = "";

        public NotesView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;

            if (!Resources.Contains("BoolToVisibility"))
            {
                Resources.Add("BoolToVisibility", new BoolToVisibilityConverter());
            }

            LoadTags();
            LoadNotes();
        }

        private void LoadTags()
        {
            var workService = new WorkService(_accountName);
            var tags = workService.GetAllNoteTags();

            TagFilterPanel.Children.Clear();

            var allBtn = new Button
            {
                Content = "全部",
                Tag = "",
                Style = (Style)FindResource("FilterButton")
            };
            ApplyTagButtonState(allBtn, string.IsNullOrEmpty(_currentTagFilter));
            allBtn.Click += TagFilter_Click;
            TagFilterPanel.Children.Add(allBtn);

            foreach (var tag in tags)
            {
                var btn = new Button
                {
                    Content = tag,
                    Tag = tag,
                    Style = (Style)FindResource("FilterButton")
                };
                ApplyTagButtonState(btn, string.Equals(_currentTagFilter, tag, StringComparison.Ordinal));
                btn.Click += TagFilter_Click;
                TagFilterPanel.Children.Add(btn);
            }
        }

        private void TagFilter_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            _currentTagFilter = btn?.Tag?.ToString() ?? "";

            foreach (var child in TagFilterPanel.Children)
            {
                if (child is Button b)
                {
                    ApplyTagButtonState(b, false);
                }
            }
            ApplyTagButtonState(btn, true);

            ApplyFilters();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _currentSearchText = SearchBox.Text.Trim();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allNotes == null) return;

            _filteredNotes = _allNotes.Where(n =>
            {
                if (!string.IsNullOrEmpty(_currentTagFilter))
                {
                    var tags = n.Tags?.Split(',') ?? new string[0];
                    if (!tags.Contains(_currentTagFilter)) return false;
                }

                if (!string.IsNullOrEmpty(_currentSearchText))
                {
                    bool titleMatch = n.Title?.Contains(_currentSearchText) ?? false;
                    if (!titleMatch) return false;
                }

                return true;
            }).ToList();

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            foreach (var note in _filteredNotes)
            {
                note.DisplayTitle = string.IsNullOrEmpty(note.Title) ? "无标题" : note.Title;
                note.DisplayContent = note.Preview ?? "";
                note.CreatedTimeDisplay = note.CreatedTime.ToString("yyyy-MM-dd HH:mm");
                note.TagsDisplay = string.IsNullOrEmpty(note.Tags) ? "" : $"#{note.Tags.Replace(",", " #")}";
                note.HasTags = !string.IsNullOrEmpty(note.Tags);
                note.WorkTitlesDisplay = string.Join(" · ", note.WorkTitles.Take(3));
                if (note.WorkTitles.Count > 3)
                {
                    note.WorkTitlesDisplay += $" 等{note.WorkTitles.Count}部";
                }
            }

            NotesItemsControl.ItemsSource = _filteredNotes;
            CountText.Text = $"({_filteredNotes.Count})";

            if (_filteredNotes.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                NotesItemsControl.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyText.Visibility = Visibility.Collapsed;
                NotesItemsControl.Visibility = Visibility.Visible;
            }
        }

        private async void LoadNotes()
        {
            try
            {
                if (LoadingText != null) LoadingText.Visibility = Visibility.Visible;
                if (NotesItemsControl != null) NotesItemsControl.Visibility = Visibility.Collapsed;
                if (EmptyText != null) EmptyText.Visibility = Visibility.Collapsed;

                var workService = new WorkService(_accountName);
                _allNotes = await System.Threading.Tasks.Task.Run(() => workService.GetAllNotes());

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}");
            }
            finally
            {
                if (LoadingText != null) LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            var editor = new NoteEditor(_accountName, null, EditorSource.NotesList);
            editor.NoteSaved += () => LoadNotes();
            editor.NoteCancelled += () => LoadNotes();
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowDetailView(editor);
        }

        private void Note_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is WorkService.NoteListItem note)
            {
                var workService = new WorkService(_accountName);
                var fullNote = workService.GetNoteById(note.Id);
                if (fullNote != null)
                {
                    var editor = new NoteEditor(_accountName, fullNote, EditorSource.NotesList);
                    editor.NoteSaved += () => LoadNotes();
                    editor.NoteCancelled += () => LoadNotes();
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.ShowDetailView(editor);
                }
            }
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int noteId)
            {
                if (Windows.AppMessageDialog.Show(Application.Current.MainWindow, "确认删除", "确定删除这篇笔记吗？", true))
                {
                    var workService = new WorkService(_accountName);
                    if (workService.DeleteNote(noteId))
                    {
                        LoadNotes();
                        Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "删除成功");
                    }
                    else
                    {
                        Windows.AppMessageDialog.Show(Application.Current.MainWindow, "错误", "删除失败");
                    }
                }
            }
        }

        private static void ApplyTagButtonState(Button button, bool isSelected)
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

    // 布尔值转可见性转换器
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}
