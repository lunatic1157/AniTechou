using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class NotesView : UserControl
    {
        private string _accountName;
        private List<WorkService.NoteListItem> _notes;

        public NotesView(string accountName)
        {
            InitializeComponent();
            _accountName = accountName;
            
            // 添加转换器
            if (!Resources.Contains("BoolToVisibility"))
            {
                Resources.Add("BoolToVisibility", new BoolToVisibilityConverter());
            }
            
            LoadNotes();
        }

        private async void LoadNotes()
        {
            try
            {
                LoadingText.Visibility = Visibility.Visible;
                NotesItemsControl.Visibility = Visibility.Collapsed;
                EmptyText.Visibility = Visibility.Collapsed;

                var workService = new WorkService(_accountName);
                var notes = await System.Threading.Tasks.Task.Run(() => workService.GetAllNotes());
                _notes = notes;

                // 处理显示格式
                foreach (var note in _notes)
                {
                    note.CreatedTimeDisplay = note.CreatedTime.ToString("yyyy-MM-dd HH:mm");
                    note.TagsDisplay = string.IsNullOrEmpty(note.Tags) ? "" : $"#{note.Tags.Replace(",", " #")}";
                    note.HasTags = !string.IsNullOrEmpty(note.Tags);
                    note.WorkTitlesDisplay = string.Join(" · ", note.WorkTitles.Take(3));
                    if (note.WorkTitles.Count > 3)
                    {
                        note.WorkTitlesDisplay += $" 等{note.WorkTitles.Count}部";
                    }
                }

                NotesItemsControl.ItemsSource = _notes;

                if (_notes.Count == 0)
                {
                    EmptyText.Visibility = Visibility.Visible;
                }
                else
                {
                    NotesItemsControl.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误：{ex.Message}\n\n{ex.StackTrace}");
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
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
                var result = MessageBox.Show("确定删除这篇笔记吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var workService = new WorkService(_accountName);
                    if (workService.DeleteNote(noteId))
                    {
                        LoadNotes();
                        MessageBox.Show("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
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