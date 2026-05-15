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
        NotesList,
        WorkDetail,
        QuickNote
    }

    public partial class NoteEditor : UserControl
    {
        private const string ResizableImagePathPrefix = "ani-image:";

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
        private int _selectionStart;
        private int _selectionLength;
        private int _changeVersion;

        public NoteEditor(string accountName, WorkService.NoteInfo note = null, EditorSource source = EditorSource.NotesList, int workId = 0)
        {
            _suppressTextEvents = true;
            InitializeComponent();
            _accountName = accountName;
            _note = note ?? new WorkService.NoteInfo();
            _source = source;
            _sourceWorkId = workId;
            DataObject.AddPastingHandler(MarkdownEditBox, MarkdownEditBox_Pasting);

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
                string content = _note.Content ?? "";

                if (_note.ContentType != "Markdown" && !string.IsNullOrWhiteSpace(content))
                {
                    // Auto-migrate old XAML note to Markdown on first open
                    try
                    {
                        content = Utilities.MarkdownConverter.XamlToMarkdown(content);
                    }
                    catch
                    {
                        // keep original content if conversion fails
                    }
                    _note.Content = content;
                    _note.ContentType = "Markdown";
                }

                MarkdownEditBox.Text = content;
                RefreshMarkdownPreview();

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
            _changeVersion = 0;
            MarkSaved();

            // Trigger save to persist migration (if any)
            if (_note.Id > 0 && _isDirty)
                MarkDirtyAndScheduleSave();
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
                        Cursor = Cursors.Hand
                    };
                    removeBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                    removeBtn.Click += (s, e) => { _selectedWorkIds.Remove(workId); RefreshWorksPanel(); MarkDirtyAndScheduleSave(); };
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
                    Cursor = Cursors.Hand
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

        private string GetMarkdownContent()
        {
            return MarkdownEditBox.Text ?? "";
        }

        private void MarkdownEditBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_suppressTextEvents)
            {
                RefreshMarkdownPreview();
                MarkDirtyAndScheduleSave();
            }
        }

        private void MarkdownEditBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            SaveSelectionSnapshot();
        }

        private void RefreshMarkdownPreview()
        {
            try
            {
                string md = MarkdownEditBox.Text ?? "";
                var flowDoc = Utilities.MarkdownConverter.MarkdownToFlowDocument(md);
                flowDoc.Foreground = MarkdownPreviewViewer.Foreground;
                flowDoc.FontFamily = MarkdownPreviewViewer.FontFamily;
                MarkdownPreviewViewer.Document = flowDoc;
                System.Diagnostics.Debug.WriteLine($"[NoteEditor] Markdown预览已刷新, Block数={flowDoc.Blocks.Count}, 字符数={md.Length}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoteEditor] Markdown预览刷新失败: {ex.Message}");
            }
        }

        private void UpdateTitlePlaceholder()
        {
            TitlePlaceholder.Visibility = string.IsNullOrWhiteSpace(TitleBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task<bool> SaveNoteInternalAsync(bool manual)
        {
            if (_isSaving) return false;
            if (!_isDirty && !manual) return true;

            int versionAtSaveStart = _changeVersion;
            string title = (TitleBox.Text ?? "").Trim();
            string content = GetMarkdownContent();
            if (string.IsNullOrEmpty(content))
            {
                if (manual)
                {
                    AppMessageDialog.Show(Application.Current.MainWindow, "提示", "请输入笔记内容");
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
                    ContentType = "Markdown",
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
                    _note.ContentType = "Markdown";
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
            if (MarkdownEditBox.CanUndo) MarkdownEditBox.Undo();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MarkdownEditBox.CanRedo) MarkdownEditBox.Redo();
        }

        private async void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => await SaveNoteInternalAsync(true);

        private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (MarkdownEditBox.CanUndo) MarkdownEditBox.Undo();
        }

        private void RedoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (MarkdownEditBox.CanRedo) MarkdownEditBox.Redo();
        }

        // =========================================================================
        // MD text manipulation helpers
        // =========================================================================

        private void WrapSelection(string prefix, string suffix)
        {
            MarkdownEditBox.Focus();
            if (MarkdownEditBox.SelectionLength > 0)
            {
                string selected = MarkdownEditBox.SelectedText;
                MarkdownEditBox.SelectedText = prefix + selected + suffix;
                MarkdownEditBox.SelectionStart = MarkdownEditBox.SelectionStart - suffix.Length - selected.Length;
                MarkdownEditBox.SelectionLength = selected.Length;
            }
            else
            {
                InsertTextAtCaret(prefix + suffix);
                MarkdownEditBox.CaretIndex -= suffix.Length;
            }
            MarkDirtyAndScheduleSave();
        }

        private void PrefixLine(string prefix)
        {
            MarkdownEditBox.Focus();
            int pos = Math.Max(0, MarkdownEditBox.CaretIndex);
            int lineStart = MarkdownEditBox.Text.LastIndexOf('\n', Math.Max(0, pos - 1)) + 1;
            int selectionLen = MarkdownEditBox.SelectionLength;
            MarkdownEditBox.Text = MarkdownEditBox.Text.Insert(lineStart, prefix);
            int shift = prefix.Length;
            MarkdownEditBox.CaretIndex = lineStart + shift + selectionLen;
            MarkdownEditBox.SelectionLength = 0;
            MarkDirtyAndScheduleSave();
        }

        private void InsertTextAtCaret(string text)
        {
            int pos = MarkdownEditBox.CaretIndex;
            MarkdownEditBox.Text = MarkdownEditBox.Text.Insert(pos, text);
            MarkdownEditBox.CaretIndex = pos + text.Length;
        }

        private void InsertMdImageAtCaret(string imagePath)
        {
            MarkdownEditBox.Focus();
            int pos = MarkdownEditBox.CaretIndex;
            string mdImage = $"![]({imagePath})";
            MarkdownEditBox.Text = MarkdownEditBox.Text.Insert(pos, mdImage);
            MarkdownEditBox.CaretIndex = pos + mdImage.Length;
            MarkDirtyAndScheduleSave();
        }

        private void SaveSelectionSnapshot()
        {
            if (MarkdownEditBox == null) return;
            _selectionStart = MarkdownEditBox.SelectionStart;
            _selectionLength = MarkdownEditBox.SelectionLength;
        }

        private void RestoreSelectionSnapshot()
        {
            MarkdownEditBox.Focus();
            MarkdownEditBox.SelectionStart = _selectionStart;
            MarkdownEditBox.SelectionLength = _selectionLength;
        }

        // =========================================================================
        // Toolbar button handlers — insert Markdown syntax
        // =========================================================================

        private void BoldButton_Click(object sender, RoutedEventArgs e) => WrapSelection("**", "**");
        private void ItalicButton_Click(object sender, RoutedEventArgs e) => WrapSelection("*", "*");
        private void UnderlineButton_Click(object sender, RoutedEventArgs e) => WrapSelection("<u>", "</u>");
        private void H1Button_Click(object sender, RoutedEventArgs e) => PrefixLine("# ");
        private void H2Button_Click(object sender, RoutedEventArgs e) => PrefixLine("## ");
        private void H3Button_Click(object sender, RoutedEventArgs e) => PrefixLine("### ");
        private void BulletedListButton_Click(object sender, RoutedEventArgs e) => PrefixLine("- ");
        private void NumberedListButton_Click(object sender, RoutedEventArgs e) => PrefixLine("1. ");
        private void AlignLeftButton_Click(object sender, RoutedEventArgs e) => WrapSelection("<p align=\"left\">\n\n", "\n\n</p>");
        private void AlignCenterButton_Click(object sender, RoutedEventArgs e) => WrapSelection("<p align=\"center\">\n\n", "\n\n</p>");
        private void AlignRightButton_Click(object sender, RoutedEventArgs e) => WrapSelection("<p align=\"right\">\n\n", "\n\n</p>");

        private void TextColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(TextColorPopup, HighlightPopup);
        private void HighlightButton_Click(object sender, RoutedEventArgs e) => TogglePopup(HighlightPopup, TextColorPopup);
        private void LinkButton_Click(object sender, RoutedEventArgs e) => InsertLink();
        private void ImageButton_Click(object sender, RoutedEventArgs e) => InsertImage();

        // =========================================================================
        // Command Executed — keyboard shortcuts
        // =========================================================================

        private void ToggleBoldCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("**", "**");
        private void ToggleItalicCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("*", "*");
        private void ToggleUnderlineCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("<u>", "</u>");
        private void Heading1Command_Executed(object sender, ExecutedRoutedEventArgs e) => PrefixLine("# ");
        private void Heading2Command_Executed(object sender, ExecutedRoutedEventArgs e) => PrefixLine("## ");
        private void Heading3Command_Executed(object sender, ExecutedRoutedEventArgs e) => PrefixLine("### ");
        private void BulletedListCommand_Executed(object sender, ExecutedRoutedEventArgs e) => PrefixLine("- ");
        private void NumberedListCommand_Executed(object sender, ExecutedRoutedEventArgs e) => PrefixLine("1. ");
        private void InsertLinkCommand_Executed(object sender, ExecutedRoutedEventArgs e) => InsertLink();
        private void InsertImageCommand_Executed(object sender, ExecutedRoutedEventArgs e) => InsertImage();
        private void AlignLeftCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("<p align=\"left\">\n\n", "\n\n</p>");
        private void AlignCenterCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("<p align=\"center\">\n\n", "\n\n</p>");
        private void AlignRightCommand_Executed(object sender, ExecutedRoutedEventArgs e) => WrapSelection("<p align=\"right\">\n\n", "\n\n</p>");

        // =========================================================================
        // Link insertion
        // =========================================================================

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

                MarkdownEditBox.Focus();
                if (MarkdownEditBox.SelectionLength > 0)
                {
                    string selected = MarkdownEditBox.SelectedText;
                    MarkdownEditBox.SelectedText = $"[{selected}]({url})";
                }
                else
                {
                    InsertTextAtCaret($"[{text}]({url})");
                }
                MarkDirtyAndScheduleSave();
            }
        }

        // =========================================================================
        // Image insertion
        // =========================================================================

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
                InsertMdImageAtCaret(dest);
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
                InsertMdImageAtCaret(dest);
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

        // =========================================================================
        // Drag-drop and paste handlers for images
        // =========================================================================

        private void MarkdownEditBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (ContainsImageData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void MarkdownEditBox_Drop(object sender, DragEventArgs e)
        {
            if (TryInsertImagesFromDataObject(e.Data))
            {
                e.Handled = true;
            }
        }

        private void MarkdownEditBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (TryInsertImagesFromDataObject(e.DataObject))
            {
                e.CancelCommand();
            }
        }

        private bool ContainsImageData(IDataObject dataObject)
        {
            if (dataObject == null) return false;
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

        // =========================================================================
        // Color palettes
        // =========================================================================

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
                    var b = (SolidColorBrush)((Button)s).Tag;
                    string hex = b.Color.ToString();
                    string tag = isBackground
                        ? $"span style=\"background-color:{hex}\""
                        : $"span style=\"color:{hex}\"";
                    WrapSelection($"<{tag}>", "</span>");
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

        private void TogglePopup(Popup targetPopup, Popup otherPopup)
        {
            otherPopup.IsOpen = false;
            targetPopup.IsOpen = !targetPopup.IsOpen;
        }

        // =========================================================================
        // Tag input
        // =========================================================================

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
