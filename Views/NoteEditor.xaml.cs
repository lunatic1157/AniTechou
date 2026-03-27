using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AniTechou.Services;
using AniTechou.Windows;
using Microsoft.Win32;

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
        private sealed class PaletteColor
        {
            public PaletteColor(string name, string hex)
            {
                Name = name;
                Hex = hex;
            }

            public string Name { get; }
            public string Hex { get; }
        }

        private static readonly PaletteColor[] TextPaletteColors =
        {
            new PaletteColor("墨黑", "#1F2937"),
            new PaletteColor("深灰", "#4B5563"),
            new PaletteColor("石墨", "#6B7280"),
            new PaletteColor("海军蓝", "#1D4ED8"),
            new PaletteColor("靛蓝", "#4F46E5"),
            new PaletteColor("紫罗兰", "#7C3AED"),
            new PaletteColor("莓果粉", "#DB2777"),
            new PaletteColor("珊瑚红", "#DC2626"),
            new PaletteColor("橙棕", "#EA580C"),
            new PaletteColor("金橙", "#D97706"),
            new PaletteColor("橄榄绿", "#65A30D"),
            new PaletteColor("祖母绿", "#059669"),
            new PaletteColor("青绿", "#0F766E"),
            new PaletteColor("天青", "#0284C7")
        };

        private static readonly PaletteColor[] HighlightPaletteColors =
        {
            new PaletteColor("奶油黄", "#FEF3C7"),
            new PaletteColor("柠檬黄", "#FDE68A"),
            new PaletteColor("暖橙", "#FED7AA"),
            new PaletteColor("蜜桃粉", "#FBCFE8"),
            new PaletteColor("浅玫瑰", "#FDA4AF"),
            new PaletteColor("薰衣草", "#DDD6FE"),
            new PaletteColor("冰蓝", "#BFDBFE"),
            new PaletteColor("湖水蓝", "#BAE6FD"),
            new PaletteColor("薄荷青", "#A7F3D0"),
            new PaletteColor("嫩青柠", "#D9F99D"),
            new PaletteColor("雾灰", "#E5E7EB"),
            new PaletteColor("淡沙色", "#FDE68A"),
            new PaletteColor("浅米色", "#F5E1B8"),
            new PaletteColor("浅紫灰", "#E9D5FF")
        };

        private string _accountName;
        private WorkService.NoteInfo _note;
        private List<WorkService.WorkListItem> _allWorks;
        private List<int> _selectedWorkIds = new List<int>();
        private List<string> _tags = new List<string>();
        private EditorSource _source;
        private int _sourceWorkId;
        public event Action NoteSaved;
        public event Action NoteCancelled;

        public static RoutedUICommand ToggleBoldCommand { get; } = new RoutedUICommand("Bold", "Bold", typeof(NoteEditor));
        public static RoutedUICommand ToggleItalicCommand { get; } = new RoutedUICommand("Italic", "Italic", typeof(NoteEditor));
        public static RoutedUICommand ToggleUnderlineCommand { get; } = new RoutedUICommand("Underline", "Underline", typeof(NoteEditor));
        public static RoutedUICommand Heading1Command { get; } = new RoutedUICommand("H1", "H1", typeof(NoteEditor));
        public static RoutedUICommand Heading2Command { get; } = new RoutedUICommand("H2", "H2", typeof(NoteEditor));
        public static RoutedUICommand Heading3Command { get; } = new RoutedUICommand("H3", "H3", typeof(NoteEditor));
        public static RoutedUICommand BulletedListCommand { get; } = new RoutedUICommand("BulletedList", "BulletedList", typeof(NoteEditor));
        public static RoutedUICommand NumberedListCommand { get; } = new RoutedUICommand("NumberedList", "NumberedList", typeof(NoteEditor));
        public static RoutedUICommand InsertLinkCommand { get; } = new RoutedUICommand("InsertLink", "InsertLink", typeof(NoteEditor));
        public static RoutedUICommand InsertImageCommand { get; } = new RoutedUICommand("InsertImage", "InsertImage", typeof(NoteEditor));
        public static RoutedUICommand AlignLeftCommand { get; } = new RoutedUICommand("AlignLeft", "AlignLeft", typeof(NoteEditor));
        public static RoutedUICommand AlignCenterCommand { get; } = new RoutedUICommand("AlignCenter", "AlignCenter", typeof(NoteEditor));
        public static RoutedUICommand AlignRightCommand { get; } = new RoutedUICommand("AlignRight", "AlignRight", typeof(NoteEditor));

        private DispatcherTimer _autoSaveTimer;
        private bool _isDirty;
        private bool _isSaving;
        private bool _suppressTextEvents;
        private List<string> _allNoteTags = new List<string>();
        private TextPointer _selectionStartSnapshot;
        private TextPointer _selectionEndSnapshot;

        public NoteEditor(string accountName, WorkService.NoteInfo note = null, EditorSource source = EditorSource.NotesList, int workId = 0)
        {
            _suppressTextEvents = true;
            InitializeComponent();
            _accountName = accountName;
            _note = note ?? new WorkService.NoteInfo();
            _source = source;
            _sourceWorkId = workId;
            RichEditor.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(Hyperlink_Click));

            LoadWorks();
            LoadData();
            LoadTagSuggestions();
            InitializeColorPalettes();
            InitializeAutoSave();
            UpdateTitlePlaceholder();
            SaveSelectionSnapshot();
        }

        private void LoadWorks()
        {
            var workService = new WorkService(_accountName);
            _allWorks = workService.GetWorksForSelection();
        }

        private void LoadData()
        {
            _suppressTextEvents = true;
            if (_note.Id > 0)
            {
                TitleBox.Text = _note.Title;
                LoadRichTextContent(_note.Content);
                _selectedWorkIds = new List<int>(_note.WorkIds);
                _tags = new List<string>(_note.Tags);
            }
            if (_source == EditorSource.WorkDetail && _sourceWorkId > 0)
            {
                if (!_selectedWorkIds.Contains(_sourceWorkId))
                {
                    _selectedWorkIds.Add(_sourceWorkId);
                }
            }
            RefreshWorksPanel();
            RefreshTagsPanel();
            _suppressTextEvents = false;
            MarkSaved();
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
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 5, 5)
                    };
                    tag.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    var stack = new StackPanel { Orientation = Orientation.Horizontal };
                    var workText = new TextBlock { Text = work.Title, FontSize = 12 };
                    workText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    stack.Children.Add(workText);
                    var removeBtn = new Button
                    {
                        Content = "✕",
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(6, 0, 0, 0),
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    removeBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                    removeBtn.Click += (s, e) => { _selectedWorkIds.Remove(workId); RefreshWorksPanel(); MarkDirtyAndScheduleSave(); };
                    stack.Children.Add(removeBtn);
                    tag.Child = stack;
                    WorksPanel.Children.Add(tag);
                }
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(hyperlink.NavigateUri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                }
                catch
                {
                }
            }
        }

        private string StripMarkup(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";

            try
            {
                return System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", string.Empty).Trim();
            }
            catch
            {
                return content;
            }
        }

        private void RefreshTagsPanel()
        {
            TagsPanel.Children.Clear();
            foreach (string tag in _tags)
            {
                var tagBorder = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5)
                };
                tagBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                var tagText = new TextBlock { Text = $"#{tag}", FontSize = 12 };
                tagText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                stack.Children.Add(tagText);
                var removeBtn = new Button
                {
                    Content = "✕",
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                removeBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                removeBtn.Click += (s, e) => { _tags.Remove(tag); RefreshTagsPanel(); MarkDirtyAndScheduleSave(); };
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
                MarkDirtyAndScheduleSave();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
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
                MarkDirtyAndScheduleSave();
            }
        }

        private void InitializeAutoSave()
        {
            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                if (AutoSaveToggle.IsChecked == true)
                {
                    SaveNoteInternal(false);
                }
            };
        }

        private void LoadTagSuggestions()
        {
            var workService = new WorkService(_accountName);
            _allNoteTags = workService.GetAllNoteTags() ?? new List<string>();
        }

        private void MarkDirtyAndScheduleSave()
        {
            _isDirty = true;
            if (SaveStatusText == null) return;
            SaveStatusText.Text = "保存中";
            if (_autoSaveTimer != null && AutoSaveToggle?.IsChecked == true)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private void MarkSaved()
        {
            _isDirty = false;
            if (SaveStatusText == null) return;
            SaveStatusText.Text = "已保存";
        }

        private void MarkSaveFailed(string reason)
        {
            if (SaveStatusText == null) return;
            SaveStatusText.Text = string.IsNullOrWhiteSpace(reason) ? "保存失败" : $"保存失败（{reason}）";
        }

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            UpdateTitlePlaceholder();
            MarkDirtyAndScheduleSave();
        }

        private void RichEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            MarkDirtyAndScheduleSave();
        }

        private void RichEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            SaveSelectionSnapshot();
        }

        private void UpdateTitlePlaceholder()
        {
            TitlePlaceholder.Visibility = string.IsNullOrWhiteSpace(TitleBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadRichTextContent(string content)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0) };
            RichEditor.Document = document;
            if (string.IsNullOrWhiteSpace(content))
            {
                document.Blocks.Add(new Paragraph());
                return;
            }

            try
            {
                var range = new TextRange(document.ContentStart, document.ContentEnd);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                range.Load(stream, DataFormats.Xaml);
            }
            catch
            {
                document.Blocks.Clear();
                document.Blocks.Add(new Paragraph(new Run(StripMarkup(content))));
            }
        }

        private string GetRichTextContent()
        {
            var range = new TextRange(RichEditor.Document.ContentStart, RichEditor.Document.ContentEnd);
            if (string.IsNullOrWhiteSpace(range.Text)) return "";
            try
            {
                using var stream = new MemoryStream();
                range.Save(stream, DataFormats.Xaml);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
            catch
            {
                return range.Text;
            }
        }

        private bool SaveNoteInternal(bool manual)
        {
            if (_isSaving) return false;
            if (!_isDirty && !manual) return true;

            string content = GetRichTextContent();
            if (string.IsNullOrEmpty(content))
            {
                if (manual)
                {
                    Windows.AppMessageDialog.Show(Application.Current.MainWindow, "提示", "请输入笔记内容");
                }
                return false;
            }

            try
            {
                _isSaving = true;
                _autoSaveTimer.Stop();
                SaveStatusText.Text = "保存中";

                _note.Title = (TitleBox.Text ?? "").Trim();
                _note.Content = content;
                _note.WorkIds = _selectedWorkIds.ToList();
                _note.Tags = _tags.ToList();

                var workService = new WorkService(_accountName);
                int noteId = workService.SaveNote(_note);
                if (noteId > 0)
                {
                    _note.Id = noteId;
                    MarkSaved();
                    NoteSaved?.Invoke();
                    return true;
                }
                MarkSaveFailed("");
                return false;
            }
            catch (Exception ex)
            {
                MarkSaveFailed(ex.Message);
                return false;
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveNoteInternal(true);

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RichEditor.CanUndo) RichEditor.Undo();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RichEditor.CanRedo) RichEditor.Redo();
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => SaveNoteInternal(true);

        private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (RichEditor.CanUndo) RichEditor.Undo();
        }

        private void RedoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (RichEditor.CanRedo) RichEditor.Redo();
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBold);
        private void ItalicButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleItalic);
        private void UnderlineButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleUnderline);
        private void H1Button_Click(object sender, RoutedEventArgs e) => ExecuteHeading(26);
        private void H2Button_Click(object sender, RoutedEventArgs e) => ExecuteHeading(22);
        private void H3Button_Click(object sender, RoutedEventArgs e) => ExecuteHeading(18);
        private void BulletedListButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBullets);
        private void NumberedListButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleNumbering);
        private void AlignLeftButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignLeft);
        private void AlignCenterButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignCenter);
        private void AlignRightButton_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignRight);
        private void TextColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(TextColorPopup, HighlightPopup);
        private void HighlightButton_Click(object sender, RoutedEventArgs e) => TogglePopup(HighlightPopup, TextColorPopup);
        private void LinkButton_Click(object sender, RoutedEventArgs e) => InsertLink();
        private void ImageButton_Click(object sender, RoutedEventArgs e) => InsertImage();

        private void ToggleBoldCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBold);
        private void ToggleItalicCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleItalic);
        private void ToggleUnderlineCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleUnderline);
        private void Heading1Command_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteHeading(26);
        private void Heading2Command_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteHeading(22);
        private void Heading3Command_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteHeading(18);
        private void BulletedListCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBullets);
        private void NumberedListCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleNumbering);
        private void InsertLinkCommand_Executed(object sender, ExecutedRoutedEventArgs e) => InsertLink();
        private void InsertImageCommand_Executed(object sender, ExecutedRoutedEventArgs e) => InsertImage();
        private void AlignLeftCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignLeft);
        private void AlignCenterCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignCenter);
        private void AlignRightCommand_Executed(object sender, ExecutedRoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignRight);

        private void ExecuteEditingCommand(RoutedUICommand command)
        {
            RichEditor.Focus();
            RestoreSelectionSnapshot();
            if (command.CanExecute(null, RichEditor))
            {
                command.Execute(null, RichEditor);
                MarkDirtyAndScheduleSave();
                SaveSelectionSnapshot();
            }
        }

        private void ExecuteColor(Brush brush, bool isBackground)
        {
            RichEditor.Focus();
            RestoreSelectionSnapshot();
            if (RichEditor.Selection != null)
            {
                RichEditor.Selection.ApplyPropertyValue(isBackground ? TextElement.BackgroundProperty : TextElement.ForegroundProperty, brush);
                MarkDirtyAndScheduleSave();
                SaveSelectionSnapshot();
            }
        }

        private void ExecuteHeading(double fontSize)
        {
            RichEditor.Focus();
            RestoreSelectionSnapshot();
            if (RichEditor.Selection != null)
            {
                RichEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
                RichEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                MarkDirtyAndScheduleSave();
                SaveSelectionSnapshot();
            }
        }

        private void InsertLink()
        {
            var dialog = new LinkInsertDialog();
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                string text = dialog.LinkTextValue?.Trim();
                string url = dialog.LinkUrlValue?.Trim();
                if (string.IsNullOrWhiteSpace(url)) return;
                if (string.IsNullOrWhiteSpace(text)) text = url;

                RichEditor.Focus();
                RestoreSelectionSnapshot();
                if (RichEditor.Selection != null)
                {
                    try
                    {
                        if (RichEditor.Selection.IsEmpty)
                        {
                            RichEditor.Selection.Text = text;
                            var end = RichEditor.CaretPosition;
                            var start = end.GetPositionAtOffset(-text.Length, LogicalDirection.Backward) ?? RichEditor.Document.ContentStart;
                            RichEditor.Selection.Select(start, end);
                        }

                        var hyperlink = new Hyperlink(RichEditor.Selection.Start, RichEditor.Selection.End)
                        {
                            NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null
                        };
                        hyperlink.Foreground = System.Windows.Media.Brushes.SteelBlue;
                        hyperlink.TextDecorations = TextDecorations.Underline;
                        MarkDirtyAndScheduleSave();
                        SaveSelectionSnapshot();
                    }
                    catch { }
                }
            }
        }

        private void InsertImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.webp|所有文件|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniTechou", "Images", "Notes");
                    Directory.CreateDirectory(baseDir);
                    string ext = Path.GetExtension(dlg.FileName);
                    string fileName = Guid.NewGuid().ToString("N") + ext;
                    string dest = Path.Combine(baseDir, fileName);
                    File.Copy(dlg.FileName, dest, true);

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(dest);
                    bitmap.EndInit();

                    var img = new Image
                    {
                        Source = bitmap,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        MaxHeight = 240,
                        Margin = new Thickness(0, 6, 0, 6)
                    };

                    RichEditor.Focus();
                    RestoreSelectionSnapshot();
                    if (RichEditor.Selection != null)
                    {
                        RichEditor.Selection.Text = "";
                        new InlineUIContainer(img, RichEditor.Selection.Start);
                        MarkDirtyAndScheduleSave();
                        SaveSelectionSnapshot();
                    }
                }
                catch { }
            }
        }

        private void InitializeColorPalettes()
        {
            BuildPalette(TextColorPalettePanel, TextPaletteColors, false);
            BuildPalette(HighlightPalettePanel, HighlightPaletteColors, true);
        }

        private void BuildPalette(Panel host, IEnumerable<PaletteColor> colors, bool isBackground)
        {
            host.Children.Clear();
            foreach (var color in colors)
            {
                var brush = CreateBrush(color.Hex);
                var button = new Button
                {
                    Background = brush,
                    ToolTip = color.Name,
                    Style = (Style)FindResource("PaletteSwatchButtonStyle"),
                    Tag = brush
                };
                button.Click += (s, e) =>
                {
                    ExecuteColor((Brush)((Button)s).Tag, isBackground);
                    TextColorPopup.IsOpen = false;
                    HighlightPopup.IsOpen = false;
                };
                host.Children.Add(button);
            }
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private void TogglePopup(System.Windows.Controls.Primitives.Popup targetPopup, System.Windows.Controls.Primitives.Popup otherPopup)
        {
            otherPopup.IsOpen = false;
            targetPopup.IsOpen = !targetPopup.IsOpen;
        }

        private void SaveSelectionSnapshot()
        {
            if (RichEditor?.Selection == null) return;
            _selectionStartSnapshot = RichEditor.Selection.Start;
            _selectionEndSnapshot = RichEditor.Selection.End;
        }

        private void RestoreSelectionSnapshot()
        {
            if (_selectionStartSnapshot == null || _selectionEndSnapshot == null) return;
            RichEditor.Selection.Select(_selectionStartSnapshot, _selectionEndSnapshot);
        }

        private void NewTagBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = (NewTagBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(q))
            {
                TagSuggestPopup.IsOpen = false;
                return;
            }
            var items = _allNoteTags
                .Where(t => !_tags.Contains(t))
                .Where(t => t.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(8)
                .ToList();
            if (items.Count == 0)
            {
                TagSuggestPopup.IsOpen = false;
                return;
            }
            TagSuggestList.ItemsSource = items;
            TagSuggestPopup.IsOpen = true;
        }

        private void NewTagBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && TagSuggestPopup.IsOpen)
            {
                TagSuggestList.Focus();
                if (TagSuggestList.Items.Count > 0) TagSuggestList.SelectedIndex = 0;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                AddTagFromInput();
                e.Handled = true;
            }
        }

        private void TagSuggestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagSuggestList.SelectedItem is string tag)
            {
                NewTagBox.Text = tag;
                NewTagBox.CaretIndex = NewTagBox.Text.Length;
                TagSuggestPopup.IsOpen = false;
                AddTagFromInput();
            }
        }

        private void NewTagBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!TagSuggestList.IsKeyboardFocusWithin)
            {
                TagSuggestPopup.IsOpen = false;
            }
        }

        private void AddTagFromInput()
        {
            string newTag = (NewTagBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(newTag)) return;
            if (_tags.Contains(newTag))
            {
                NewTagBox.Text = "";
                return;
            }
            _tags.Add(newTag);
            RefreshTagsPanel();
            NewTagBox.Text = "";
            TagSuggestPopup.IsOpen = false;
            MarkDirtyAndScheduleSave();
        }
    }
}
