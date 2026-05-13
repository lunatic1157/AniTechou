using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private const string ResizableImageHostTag = "ResizableNoteImageHost";
        private const string ResizableImagePathPrefix = "ani-image:";
        private const string NoteImageMoveDataFormat = "AniTechou.NoteImageMoveV2";
        private const double MinImageWidth = 120;
        private const double MinImageHeight = 90;
        private const double MaxImageWidth = 900;
        private const double MaxImageHeight = 900;
        private const double ResizeHitThickness = 10;

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
        private int _changeVersion;
        private Grid _activeResizeHost;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private string _resizeMode;
        private Grid _pendingMoveHost;
        private Point _pendingMoveStartPoint;
        private Grid _movingSourceHost;

        public NoteEditor(string accountName, WorkService.NoteInfo note = null, EditorSource source = EditorSource.NotesList, int workId = 0)
        {
            _suppressTextEvents = true;
            InitializeComponent();
            _accountName = accountName;
            _note = note ?? new WorkService.NoteInfo();
            _source = source;
            _sourceWorkId = workId;
            RichEditor.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(Hyperlink_Click));
            DataObject.AddPastingHandler(RichEditor, RichEditor_Pasting);

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
            AttachResizableImagesToDocument();
            _suppressTextEvents = false;
            _changeVersion = 0;
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
            _autoSaveTimer.Tick += async (s, e) =>
            {
                _autoSaveTimer.Stop();
                if (AutoSaveToggle.IsChecked == true)
                {
                    await SaveNoteInternalAsync(false);
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
            _changeVersion++;
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
            if (string.IsNullOrWhiteSpace(content))
            {
                RichEditor.Document = CreateEmptyDocument();
                return;
            }

            try
            {
                if (content.TrimStart().StartsWith("<FlowDocument", StringComparison.OrdinalIgnoreCase))
                {
                    if (XamlReader.Parse(content) is FlowDocument parsedDocument)
                    {
                        RichEditor.Document = parsedDocument;
                        AttachResizableImagesToDocument();
                        return;
                    }
                }
            }
            catch
            {
            }

            var fallbackDocument = CreateEmptyDocument();
            try
            {
                var range = new TextRange(fallbackDocument.ContentStart, fallbackDocument.ContentEnd);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                range.Load(stream, DataFormats.Xaml);
                RichEditor.Document = fallbackDocument;
                AttachResizableImagesToDocument();
            }
            catch
            {
                fallbackDocument.Blocks.Clear();
                fallbackDocument.Blocks.Add(new Paragraph(new Run(StripMarkup(content))));
                RichEditor.Document = fallbackDocument;
            }
        }

        private string GetRichTextContent()
        {
            if (IsDocumentEffectivelyEmpty()) return "";
            try
            {
                return XamlWriter.Save(RichEditor.Document);
            }
            catch
            {
                var range = new TextRange(RichEditor.Document.ContentStart, RichEditor.Document.ContentEnd);
                return range.Text;
            }
        }

        private FlowDocument CreateEmptyDocument()
        {
            var document = new FlowDocument { PagePadding = new Thickness(0) };
            document.Blocks.Add(new Paragraph());
            return document;
        }

        private bool IsDocumentEffectivelyEmpty()
        {
            var range = new TextRange(RichEditor.Document.ContentStart, RichEditor.Document.ContentEnd);
            if (!string.IsNullOrWhiteSpace(range.Text))
            {
                return false;
            }

            return !EnumerateResizableImageHosts(RichEditor.Document).Any();
        }

        private async System.Threading.Tasks.Task<bool> SaveNoteInternalAsync(bool manual)
        {
            if (_isSaving) return false;
            if (!_isDirty && !manual) return true;

            int versionAtSaveStart = _changeVersion;
            string title = (TitleBox.Text ?? "").Trim();
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
                _autoSaveTimer?.Stop();
                SaveStatusText.Text = "保存中";

                var noteSnapshot = new WorkService.NoteInfo
                {
                    Id = _note.Id,
                    Title = title,
                    Content = content,
                    CreatedTime = _note.CreatedTime,
                    ModifiedTime = _note.ModifiedTime,
                    WorkIds = _selectedWorkIds.ToList(),
                    Tags = _tags.ToList()
                };

                int noteId = await System.Threading.Tasks.Task.Run(() =>
                {
                    var workService = new WorkService(_accountName);
                    return workService.SaveNote(noteSnapshot);
                });

                if (noteId > 0)
                {
                    _note.Id = noteId;
                    _note.Title = noteSnapshot.Title;
                    _note.Content = noteSnapshot.Content;
                    _note.WorkIds = noteSnapshot.WorkIds;
                    _note.Tags = noteSnapshot.Tags;

                    if (_changeVersion == versionAtSaveStart)
                    {
                        MarkSaved();
                    }
                    else
                    {
                        _isDirty = true;
                        SaveStatusText.Text = "已保存（有新修改）";
                    }

                    if (manual)
                    {
                        NoteSaved?.Invoke();
                    }
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
                if (_isDirty && _autoSaveTimer != null && AutoSaveToggle?.IsChecked == true)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer.Start();
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e) => await SaveNoteInternalAsync(true);

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RichEditor.CanUndo) RichEditor.Undo();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RichEditor.CanRedo) RichEditor.Redo();
        }

        private async void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => await SaveNoteInternalAsync(true);

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
                TryInsertImageFromFile(dlg.FileName);
            }
        }

        private bool TryInsertImageFromFile(string filePath)
        {
            try
            {
                string dest = CopyImageToNotesStorage(filePath);
                InsertImageAtSelection(dest);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryInsertImageFromBitmap(BitmapSource bitmapSource)
        {
            try
            {
                string dest = SaveBitmapToNotesStorage(bitmapSource);
                InsertImageAtSelection(dest);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string CopyImageToNotesStorage(string sourceFilePath)
        {
            string ext = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            string dest = Path.Combine(GetNotesImageDirectory(), Guid.NewGuid().ToString("N") + ext);
            File.Copy(sourceFilePath, dest, true);
            return dest;
        }

        private string SaveBitmapToNotesStorage(BitmapSource bitmapSource)
        {
            string dest = Path.Combine(GetNotesImageDirectory(), Guid.NewGuid().ToString("N") + ".png");
            using var stream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            return dest;
        }

        private string GetNotesImageDirectory()
        {
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniTechou", "Images", "Notes");
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        private void InsertImageAtSelection(string imagePath, double? width = null, double? height = null)
        {
            var imageHost = CreateResizableImageHost(imagePath, width, height);

            RichEditor.Focus();
            RestoreSelectionSnapshot();
            if (RichEditor.Selection != null)
            {
                RichEditor.Selection.Text = "";
                new InlineUIContainer(imageHost, RichEditor.Selection.Start);
                MarkDirtyAndScheduleSave();
                SaveSelectionSnapshot();
            }
        }

        private void InsertImageAtPosition(TextPointer position, string imagePath, double? width, double? height)
        {
            if (position == null) return;

            var imageHost = CreateResizableImageHost(imagePath, width, height);
            new InlineUIContainer(imageHost, position);
            MarkDirtyAndScheduleSave();
        }

        private Grid CreateResizableImageHost(string imagePath, double? width = null, double? height = null)
        {
            var source = CreateBitmapFromPath(imagePath);
            (double defaultWidth, double defaultHeight) = GetInitialImageSize(source);
            double imageWidth = width ?? defaultWidth;
            double imageHeight = height ?? defaultHeight;

            var host = new Grid
            {
                Width = imageWidth,
                Height = imageHeight,
                Margin = new Thickness(0, 6, 0, 6),
                Background = Brushes.Transparent,
                Tag = ResizableImageHostTag,
                Cursor = Cursors.Arrow
            };

            var border = new Border
            {
                Background = Brushes.Transparent
            };

            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                Uid = $"{ResizableImagePathPrefix}{imagePath}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            border.Child = image;
            host.Children.Add(border);

            AttachResizableImageHandlers(host);
            return host;
        }

        private BitmapImage CreateBitmapFromPath(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static (double width, double height) GetInitialImageSize(BitmapSource bitmapSource)
        {
            double width = 320;
            double height = 220;

            if (bitmapSource.PixelWidth > 0 && bitmapSource.PixelHeight > 0)
            {
                width = bitmapSource.PixelWidth;
                height = bitmapSource.PixelHeight;
                double scale = Math.Min(1.0, 360.0 / Math.Max(width, height));
                width = Math.Max(MinImageWidth, width * scale);
                height = Math.Max(MinImageHeight, height * scale);
            }

            return (width, height);
        }

        private void AttachResizableImagesToDocument()
        {
            foreach (var host in EnumerateResizableImageHosts(RichEditor.Document))
            {
                if (host.Children.OfType<Border>().FirstOrDefault() is Border border)
                {
                    if (border.Child is Image image && image.Source == null)
                    {
                        string imagePath = GetImagePathFromElement(image);
                        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                        {
                            image.Source = CreateBitmapFromPath(imagePath);
                        }
                    }

                    AttachResizableImageHandlers(host);
                }
            }
        }

        private IEnumerable<Grid> EnumerateResizableImageHosts(FlowDocument document)
        {
            if (document == null) yield break;

            foreach (var block in document.Blocks)
            {
                foreach (var host in EnumerateResizableImageHosts(block))
                {
                    yield return host;
                }
            }
        }

        private IEnumerable<Grid> EnumerateResizableImageHosts(Block block)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    foreach (var inline in paragraph.Inlines)
                    {
                        foreach (var host in EnumerateResizableImageHosts(inline))
                        {
                            yield return host;
                        }
                    }
                    break;
                case Section section:
                    foreach (var child in section.Blocks)
                    {
                        foreach (var host in EnumerateResizableImageHosts(child))
                        {
                            yield return host;
                        }
                    }
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                    {
                        foreach (var child in item.Blocks)
                        {
                            foreach (var host in EnumerateResizableImageHosts(child))
                            {
                                yield return host;
                            }
                        }
                    }
                    break;
                case BlockUIContainer blockUi when blockUi.Child is Grid grid && Equals(grid.Tag, ResizableImageHostTag):
                    yield return grid;
                    break;
            }
        }

        private IEnumerable<Grid> EnumerateResizableImageHosts(Inline inline)
        {
            switch (inline)
            {
                case Span span:
                    foreach (var child in span.Inlines)
                    {
                        foreach (var host in EnumerateResizableImageHosts(child))
                        {
                            yield return host;
                        }
                    }
                    break;
                case InlineUIContainer inlineUi when inlineUi.Child is Grid grid && Equals(grid.Tag, ResizableImageHostTag):
                    yield return grid;
                    break;
            }
        }

        private void AttachResizableImageHandlers(Grid host)
        {
            host.PreviewMouseMove -= ResizableImageHost_PreviewMouseMove;
            host.PreviewMouseMove += ResizableImageHost_PreviewMouseMove;
            host.PreviewMouseLeftButtonDown -= ResizableImageHost_PreviewMouseLeftButtonDown;
            host.PreviewMouseLeftButtonDown += ResizableImageHost_PreviewMouseLeftButtonDown;
            host.PreviewMouseLeftButtonUp -= ResizableImageHost_PreviewMouseLeftButtonUp;
            host.PreviewMouseLeftButtonUp += ResizableImageHost_PreviewMouseLeftButtonUp;
            host.MouseLeave -= ResizableImageHost_MouseLeave;
            host.MouseLeave += ResizableImageHost_MouseLeave;
        }

        private void ResizableImageHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Grid host) return;

            string mode = GetResizeMode(host, e.GetPosition(host));
            if (!string.IsNullOrEmpty(mode))
            {
                _activeResizeHost = host;
                _resizeMode = mode;
                _resizeStartPoint = e.GetPosition(this);
                _resizeStartWidth = host.Width;
                _resizeStartHeight = host.Height;
                host.CaptureMouse();
                host.Cursor = GetResizeCursor(mode);
                e.Handled = true;
                return;
            }

            _pendingMoveHost = host;
            _pendingMoveStartPoint = e.GetPosition(this);
            e.Handled = true;
        }

        private void ResizableImageHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Grid host) return;
            host.ReleaseMouseCapture();
            _activeResizeHost = null;
            _resizeMode = null;
            _pendingMoveHost = null;
            UpdateResizeCursor(host, e.GetPosition(host));
            e.Handled = true;
        }

        private void ResizableImageHost_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_activeResizeHost == null && sender is Grid host)
            {
                host.Cursor = Cursors.Arrow;
            }
        }

        private void ResizableImageHost_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not Grid host) return;

            if (_activeResizeHost == host && e.LeftButton == MouseButtonState.Pressed && !string.IsNullOrWhiteSpace(_resizeMode))
            {
                ResizeImageHost(host, e.GetPosition(this));
                host.Cursor = GetResizeCursor(_resizeMode);
                e.Handled = true;
                return;
            }

            if (_pendingMoveHost == host && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                if (Math.Abs(currentPoint.X - _pendingMoveStartPoint.X) >= 6 ||
                    Math.Abs(currentPoint.Y - _pendingMoveStartPoint.Y) >= 6)
                {
                    _pendingMoveHost = null;
                    StartMoveImageDrag(host);
                    e.Handled = true;
                    return;
                }
            }

            UpdateResizeCursor(host, e.GetPosition(host));
        }

        private void ResizeImageHost(Grid host, Point currentPoint)
        {
            if (host.Width <= 0 || host.Height <= 0) return;

            double aspectRatio = _resizeStartWidth / _resizeStartHeight;
            if (aspectRatio <= 0) aspectRatio = 1;

            double dx = currentPoint.X - _resizeStartPoint.X;
            double dy = currentPoint.Y - _resizeStartPoint.Y;
            double newWidth = _resizeStartWidth;
            double newHeight = _resizeStartHeight;

            if (_resizeMode == "right" || _resizeMode == "left")
            {
                double signedDelta = _resizeMode == "right" ? dx : -dx;
                newWidth = _resizeStartWidth + signedDelta;
                newWidth = Math.Clamp(newWidth, MinImageWidth, MaxImageWidth);
                newHeight = newWidth / aspectRatio;
            }
            else if (_resizeMode == "bottom" || _resizeMode == "top")
            {
                double signedDelta = _resizeMode == "bottom" ? dy : -dy;
                newHeight = _resizeStartHeight + signedDelta;
                newHeight = Math.Clamp(newHeight, MinImageHeight, MaxImageHeight);
                newWidth = newHeight * aspectRatio;
            }
            else
            {
                double widthDelta = _resizeMode.Contains("left") ? -dx : dx;
                double heightDelta = _resizeMode.Contains("top") ? -dy : dy;
                double widthCandidate = Math.Clamp(_resizeStartWidth + widthDelta, MinImageWidth, MaxImageWidth);
                double heightCandidate = Math.Clamp(_resizeStartHeight + heightDelta, MinImageHeight, MaxImageHeight);

                if (Math.Abs(widthDelta) >= Math.Abs(heightDelta))
                {
                    newWidth = widthCandidate;
                    newHeight = newWidth / aspectRatio;
                }
                else
                {
                    newHeight = heightCandidate;
                    newWidth = newHeight * aspectRatio;
                }
            }

            host.Width = Math.Clamp(newWidth, MinImageWidth, MaxImageWidth);
            host.Height = Math.Clamp(newHeight, MinImageHeight, MaxImageHeight);
            MarkDirtyAndScheduleSave();
        }

        private void UpdateResizeCursor(FrameworkElement element, Point point)
        {
            element.Cursor = GetResizeCursor(GetResizeMode(element, point));
        }

        private string GetResizeMode(FrameworkElement element, Point point)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return "";

            bool left = point.X <= ResizeHitThickness;
            bool right = point.X >= element.ActualWidth - ResizeHitThickness;
            bool top = point.Y <= ResizeHitThickness;
            bool bottom = point.Y >= element.ActualHeight - ResizeHitThickness;

            if (left && top) return "top-left";
            if (right && top) return "top-right";
            if (left && bottom) return "bottom-left";
            if (right && bottom) return "bottom-right";
            if (left) return "left";
            if (right) return "right";
            if (top) return "top";
            if (bottom) return "bottom";
            return "";
        }

        private static Cursor GetResizeCursor(string resizeMode)
        {
            return resizeMode switch
            {
                "left" or "right" => Cursors.SizeWE,
                "top" or "bottom" => Cursors.SizeNS,
                "top-left" or "bottom-right" => Cursors.SizeNWSE,
                "top-right" or "bottom-left" => Cursors.SizeNESW,
                _ => Cursors.Arrow
            };
        }

        private string GetImagePathFromElement(Image image)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.Uid)) return "";
            return image.Uid.StartsWith(ResizableImagePathPrefix, StringComparison.OrdinalIgnoreCase)
                ? image.Uid.Substring(ResizableImagePathPrefix.Length)
                : "";
        }

        private void RichEditor_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(NoteImageMoveDataFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (ContainsImageData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void RichEditor_Drop(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(NoteImageMoveDataFormat) &&
                e.Data.GetData(NoteImageMoveDataFormat) is string payload &&
                TryParseImageMovePayload(payload, out string path, out double width, out double height))
            {
                var pos = RichEditor.GetPositionFromPoint(e.GetPosition(RichEditor), true) ?? RichEditor.Document.ContentEnd;
                InsertImageAtPosition(pos, path, width, height);
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (TryInsertImagesFromDataObject(e.Data))
            {
                e.Handled = true;
            }
        }

        private void RichEditor_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (TryInsertImagesFromDataObject(e.DataObject))
            {
                e.CancelCommand();
            }
        }

        private bool ContainsImageData(IDataObject dataObject)
        {
            if (dataObject == null) return false;
            if (dataObject.GetDataPresent(NoteImageMoveDataFormat)) return true;
            if (dataObject.GetDataPresent(DataFormats.Bitmap)) return true;
            if (!dataObject.GetDataPresent(DataFormats.FileDrop)) return false;

            if (dataObject.GetData(DataFormats.FileDrop) is not string[] files) return false;
            return files.Any(IsSupportedImageFile);
        }

        private bool TryInsertImagesFromDataObject(IDataObject dataObject)
        {
            if (dataObject == null) return false;

            bool inserted = false;

            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (string file in files.Where(IsSupportedImageFile))
                {
                    inserted |= TryInsertImageFromFile(file);
                }
            }
            else if (dataObject.GetDataPresent(DataFormats.Bitmap) &&
                     dataObject.GetData(DataFormats.Bitmap) is BitmapSource bitmapSource)
            {
                inserted = TryInsertImageFromBitmap(bitmapSource);
            }

            return inserted;
        }

        private static bool IsSupportedImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
        }

        private void StartMoveImageDrag(Grid host)
        {
            if (host == null) return;

            string imagePath = "";
            if (host.Children.OfType<Border>().FirstOrDefault()?.Child is Image image)
            {
                imagePath = GetImagePathFromElement(image);
            }
            if (string.IsNullOrWhiteSpace(imagePath)) return;

            _movingSourceHost = host;
            string payload = BuildImageMovePayload(imagePath, host.Width, host.Height);
            var data = new DataObject();
            data.SetData(NoteImageMoveDataFormat, payload);

            var effect = DragDrop.DoDragDrop(host, data, DragDropEffects.Move);
            if (effect == DragDropEffects.Move)
            {
                RemoveImageHostFromDocument(_movingSourceHost);
            }
            _movingSourceHost = null;
        }

        private void RemoveImageHostFromDocument(Grid host)
        {
            if (host == null) return;

            var container = FindInlineContainerForHostInDocument(RichEditor.Document, host);
            if (container == null) return;

            if (container.Parent is Paragraph paragraph)
            {
                paragraph.Inlines.Remove(container);
                MarkDirtyAndScheduleSave();
            }
        }

        private InlineUIContainer FindInlineContainerForHostInDocument(FlowDocument document, Grid host)
        {
            if (document == null || host == null) return null;

            foreach (var block in document.Blocks)
            {
                var found = FindInlineContainerForHostInBlock(block, host);
                if (found != null) return found;
            }
            return null;
        }

        private InlineUIContainer FindInlineContainerForHostInBlock(Block block, Grid host)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    foreach (var inline in paragraph.Inlines)
                    {
                        var found = FindInlineContainerForHostInInline(inline, host);
                        if (found != null) return found;
                    }
                    break;
                case Section section:
                    foreach (var child in section.Blocks)
                    {
                        var found = FindInlineContainerForHostInBlock(child, host);
                        if (found != null) return found;
                    }
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                    {
                        foreach (var child in item.Blocks)
                        {
                            var found = FindInlineContainerForHostInBlock(child, host);
                            if (found != null) return found;
                        }
                    }
                    break;
            }
            return null;
        }

        private InlineUIContainer FindInlineContainerForHostInInline(Inline inline, Grid host)
        {
            switch (inline)
            {
                case InlineUIContainer ui when ui.Child == host:
                    return ui;
                case Span span:
                    foreach (var child in span.Inlines)
                    {
                        var found = FindInlineContainerForHostInInline(child, host);
                        if (found != null) return found;
                    }
                    break;
            }
            return null;
        }

        private static string BuildImageMovePayload(string imagePath, double width, double height)
        {
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(imagePath));
            return $"{b64}|{width}|{height}";
        }

        private static bool TryParseImageMovePayload(string payload, out string imagePath, out double width, out double height)
        {
            imagePath = "";
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(payload)) return false;
            var parts = payload.Split('|');
            if (parts.Length != 3) return false;

            try
            {
                imagePath = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                if (!double.TryParse(parts[1], out width)) return false;
                if (!double.TryParse(parts[2], out height)) return false;
                return true;
            }
            catch
            {
                return false;
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
