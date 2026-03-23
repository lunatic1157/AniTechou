using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;
using AniTechou.Windows;

namespace AniTechou.Views
{
    public enum EditorSource
    {
        NotesList,      // 来自笔记列表
        WorkDetail,     // 来自作品详情页
        QuickNote       // 来自快速笔记
    }

    public partial class NoteEditor : UserControl
    {
        private string _accountName;
        private WorkService.NoteInfo _note;
        private List<WorkService.WorkListItem> _allWorks;
        private List<int> _selectedWorkIds = new List<int>();
        private List<string> _tags = new List<string>();
        private EditorSource _source;
        private int _sourceWorkId;
        public event Action NoteSaved;
        public event Action NoteCancelled;

        public NoteEditor(string accountName, WorkService.NoteInfo note = null, EditorSource source = EditorSource.NotesList, int workId = 0)
        {
            InitializeComponent();
            _accountName = accountName;
            _note = note ?? new WorkService.NoteInfo();
            _source = source;
            _sourceWorkId = workId;

            LoadWorks();
            LoadData();
        }

        private void LoadWorks()
        {
            var workService = new WorkService(_accountName);
            _allWorks = workService.GetWorksForSelection();
        }

        private void LoadData()
        {
            if (_note.Id > 0)
            {
                TitleBox.Text = _note.Title;
                ContentBox.Text = _note.Content;
                _selectedWorkIds = new List<int>(_note.WorkIds);
                _tags = new List<string>(_note.Tags);
            }
            RefreshWorksPanel();
            RefreshTagsPanel();
        }

        private void RefreshWorksPanel()
        {
            WorksPanel.Children.Clear();
            foreach (int workId in _selectedWorkIds)
            {
                var work = _allWorks.FirstOrDefault(w => w.Id == workId);
                if (work != null)
                {
                    var tag = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 213, 192)),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 5, 5)
                    };
                    var stack = new StackPanel { Orientation = Orientation.Horizontal };
                    stack.Children.Add(new TextBlock { Text = work.Title, FontSize = 12 });
                    var removeBtn = new Button { Content = "✕", Width = 18, Height = 18, Margin = new Thickness(5, 0, 0, 0), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                    removeBtn.Click += (s, e) => { _selectedWorkIds.Remove(workId); RefreshWorksPanel(); };
                    stack.Children.Add(removeBtn);
                    tag.Child = stack;
                    WorksPanel.Children.Add(tag);
                }
            }
        }

        private void RefreshTagsPanel()
        {
            TagsPanel.Children.Clear();
            foreach (string tag in _tags)
            {
                var tagBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 213, 192)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5)
                };
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(new TextBlock { Text = $"#{tag}", FontSize = 12 });
                var removeBtn = new Button { Content = "✕", Width = 18, Height = 18, Margin = new Thickness(5, 0, 0, 0), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                removeBtn.Click += (s, e) => { _tags.Remove(tag); RefreshTagsPanel(); };
                stack.Children.Add(removeBtn);
                tagBorder.Child = stack;
                TagsPanel.Children.Add(tagBorder);
            }
        }

        private void AddWork_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WorkSelectionDialog(_allWorks, _selectedWorkIds);
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                _selectedWorkIds = dialog.SelectedWorkIds;
                RefreshWorksPanel();
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            string newTag = NewTagBox.Text.Trim();
            if (!string.IsNullOrEmpty(newTag) && !_tags.Contains(newTag))
            {
                _tags.Add(newTag);
                RefreshTagsPanel();
                NewTagBox.Text = "";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string content = ContentBox.Text.Trim();
            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("请输入笔记内容");
                return;
            }

            _note.Title = TitleBox.Text.Trim();
            _note.Content = content;
            _note.WorkIds = _selectedWorkIds;
            _note.Tags = _tags;

            var workService = new WorkService(_accountName);
            int noteId = workService.SaveNote(_note);

            if (noteId > 0)
            {
                MessageBox.Show("保存成功！");
                NoteSaved?.Invoke();
                ReturnToPrevious();
            }
            else
            {
                MessageBox.Show("保存失败");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            NoteCancelled?.Invoke();
            ReturnToPrevious();
        }

        private void ReturnToPrevious()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            switch (_source)
            {
                case EditorSource.NotesList:
                    var notesView = new NotesView(_accountName);
                    mainWindow.ShowDetailView(notesView);
                    break;
                case EditorSource.WorkDetail:
                    var workDetail = new WorkDetailView(_sourceWorkId, 0, _accountName);
                    mainWindow.ShowDetailView(workDetail);
                    break;
                case EditorSource.QuickNote:
                    mainWindow.RefreshCurrentView();
                    break;
            }
        }

        public void PreSelectWork(int workId)
        {
            if (!_selectedWorkIds.Contains(workId))
            {
                _selectedWorkIds.Add(workId);
                RefreshWorksPanel();
            }
        }
    }
}