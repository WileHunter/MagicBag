using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfApp3.Services;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfApp3
{
    public partial class NotebookView : UserControl
    {
        #region 数据

        public sealed class NoteItem : INotifyPropertyChanged
        {
            private string _path;
            private DateTime _modified;

            public NoteItem(string path, DateTime modified)
            {
                _path = path;
                _modified = modified;
            }

            public string FilePath
            {
                get => _path;
                set
                {
                    _path = value;
                    OnChanged(nameof(FilePath));
                    OnChanged(nameof(Name));
                    OnChanged(nameof(Initial));
                    OnChanged(nameof(ChipIndex));
                }
            }

            public string Name => System.IO.Path.GetFileNameWithoutExtension(_path);

            // 列表图标：取名称首字，按名称稳定地分配一种底色，方便一眼区分
            public string Initial
            {
                get
                {
                    var name = Name.TrimStart();
                    if (name.Length == 0) return "#";
                    var end = char.IsSurrogatePair(name, 0) ? 2 : 1;
                    return name[..end].ToUpperInvariant();
                }
            }

            public int ChipIndex
            {
                get
                {
                    var hash = 0;
                    foreach (var ch in Name) hash = unchecked(hash * 31 + ch);
                    return (int)((uint)hash % 6);
                }
            }

            public DateTime Modified
            {
                get => _modified;
                set
                {
                    _modified = value;
                    OnChanged(nameof(Modified));
                    OnChanged(nameof(ModifiedText));
                }
            }

            public string ModifiedText
            {
                get
                {
                    var today = DateTime.Today;
                    if (_modified.Date == today) return "今天 " + _modified.ToString("HH:mm");
                    if (_modified.Date == today.AddDays(-1)) return "昨天 " + _modified.ToString("HH:mm");
                    return _modified.Year == today.Year ? _modified.ToString("MM-dd HH:mm") : _modified.ToString("yyyy-MM-dd");
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private sealed record FontEntry(string Name, FontFamily Family);

        private const string BoxUnchecked = "☐";
        private const string BoxChecked = "☑";
        private const double DefaultFontSize = 14;
        private const double MaxImageWidth = 560;

        private static readonly FontFamily DefaultFont = new("Microsoft YaHei UI");
        private static readonly FontFamily CodeFont = new("Consolas, Microsoft YaHei UI");
        private static readonly Brush DefaultForeground = Frozen(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)));
        private static readonly Brush CodeBackground = Frozen(new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF5)));
        private static readonly Brush CodeForeground = Frozen(new SolidColorBrush(Color.FromRgb(0xBE, 0x18, 0x5D)));
        private static readonly Brush QuoteBorder = Frozen(new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9)));
        private static readonly Brush QuoteForeground = Frozen(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
        private static readonly Brush QuoteBackground = Frozen(new SolidColorBrush(Color.FromRgb(0xF0, 0xF7, 0xFC)));
        private static readonly Brush LineBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));
        private static readonly Brush TableHeaderBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)));

        private static readonly string[] FontColors =
        {
            "#1E293B", "#64748B", "#EF4444", "#F97316", "#EAB308", "#22C55E",
            "#14B8A6", "#0EA5E9", "#3B82F6", "#6366F1", "#8B5CF6", "#EC4899",
            "#991B1B", "#9A3412", "#854D0E", "#166534", "#1E40AF", "#6B21A8"
        };

        private static readonly string[] HighlightColors =
        {
            "#FEF08A", "#FDE68A", "#FED7AA", "#FECACA", "#FBCFE8", "#E9D5FF",
            "#C7D2FE", "#BFDBFE", "#A5F3FC", "#BBF7D0", "#D9F99D", "#E2E8F0"
        };

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico" };

        private readonly ObservableCollection<NoteItem> _notes = new();
        private readonly ICollectionView _notesView;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _countTimer;
        private readonly DispatcherTimer _searchTimer;
        private readonly DispatcherTimer _toolbarTimer;

        private NoteItem? _currentNote;
        private bool _dirty;
        private bool _loading;
        private bool _updatingToolbar;
        private bool _initialized;
        private double _zoom = 1.0;
        private TextPointer? _searchAnchor;

        #endregion

        public NotebookView()
        {
            InitializeComponent();

            _notesView = CollectionViewSource.GetDefaultView(_notes);
            _notesView.Filter = FilterNote;
            NotesListBox.ItemsSource = _notesView;

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _autoSaveTimer.Tick += (_, _) =>
            {
                _autoSaveTimer.Stop();
                if (AutoSaveCheck.IsChecked == true) SaveIfDirty();
            };
            _countTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _countTimer.Tick += (_, _) =>
            {
                _countTimer.Stop();
                UpdateWordCount();
            };
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _searchTimer.Tick += (_, _) =>
            {
                _searchTimer.Stop();
                RunSearch(fromAnchor: true);
            };
            _toolbarTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _toolbarTimer.Tick += (_, _) =>
            {
                _toolbarTimer.Stop();
                UpdateToolbarState();
            };

            PreviewKeyDown += View_PreviewKeyDown;
            Loaded += NotebookView_Loaded;
        }

        private static Brush Frozen(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        private static SolidColorBrush BrushFrom(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private void NotebookView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;

            InitFonts();
            InitSwatches();
            LoadNoteList();

            if (_notes.Count > 0)
                NotesListBox.SelectedIndex = 0;
            else
                ShowEmpty();
        }

        #region 初始化

        private void InitFonts()
        {
            var zh = XmlLanguage.GetLanguage("zh-cn");
            var en = XmlLanguage.GetLanguage("en-us");
            var preferred = new[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "FangSong",
                "DengXian", "Consolas", "Cascadia Code", "Segoe UI", "Arial", "Times New Roman"
            };

            string DisplayName(FontFamily f)
            {
                if (f.FamilyNames.TryGetValue(zh, out var n)) return n;
                if (f.FamilyNames.TryGetValue(en, out n)) return n;
                return f.Source;
            }

            var all = Fonts.SystemFontFamilies
                .Select(f => new FontEntry(DisplayName(f), f))
                .GroupBy(f => f.Family.Source, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var list = new List<FontEntry>();
            foreach (var p in preferred)
            {
                var hit = all.FirstOrDefault(f => string.Equals(f.Family.Source, p, StringComparison.OrdinalIgnoreCase));
                if (hit != null)
                {
                    list.Add(hit);
                    all.Remove(hit);
                }
            }
            list.AddRange(all.OrderBy(f => f.Name, StringComparer.CurrentCulture));

            FontFamilyCombo.DisplayMemberPath = nameof(FontEntry.Name);
            FontFamilyCombo.ItemsSource = list;

            FontSizeCombo.ItemsSource = new[] { 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24, 28, 32, 36, 42, 48, 60, 72 }
                .Select(s => s.ToString()).ToList();
        }

        private void InitSwatches()
        {
            var swatchStyle = (Style)FindResource("SwatchButton");

            foreach (var hex in FontColors)
            {
                var brush = BrushFrom(hex);
                var b = new Button { Style = swatchStyle, Background = brush, ToolTip = hex };
                b.Click += (_, _) =>
                {
                    FontColorPopup.IsOpen = false;
                    FontColorBar.Background = brush;
                    ApplyInline(TextElement.ForegroundProperty, brush);
                };
                FontColorPanel.Children.Add(b);
            }

            foreach (var hex in HighlightColors)
            {
                var brush = BrushFrom(hex);
                var b = new Button { Style = swatchStyle, Background = brush, ToolTip = hex };
                b.Click += (_, _) =>
                {
                    HighlightPopup.IsOpen = false;
                    HighlightBar.Background = brush;
                    ApplyInline(TextElement.BackgroundProperty, brush);
                };
                HighlightPanel.Children.Add(b);
            }
        }

        #endregion

        #region 笔记列表

        private static string NotesDir
        {
            get
            {
                Directory.CreateDirectory(AppPaths.NotesDir);
                return AppPaths.NotesDir;
            }
        }

        private void LoadNoteList()
        {
            _notes.Clear();
            try
            {
                foreach (var f in new DirectoryInfo(NotesDir).GetFiles("*.rtf").OrderByDescending(f => f.LastWriteTime))
                    _notes.Add(new NoteItem(f.FullName, f.LastWriteTime));
            }
            catch (Exception ex)
            {
                Toast.Error($"读取笔记目录失败：{ex.Message}");
            }
            UpdateNoteCount();
        }

        private void UpdateNoteCount() => NoteCountText.Text = _notes.Count.ToString();

        private bool FilterNote(object o)
        {
            var key = NoteFilterBox?.Text?.Trim();
            return string.IsNullOrEmpty(key) || ((NoteItem)o).Name.Contains(key, StringComparison.OrdinalIgnoreCase);
        }

        private void NoteFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _notesView.Refresh();
            if (_currentNote != null && _notesView.Contains(_currentNote))
                NotesListBox.SelectedItem = _currentNote;
        }

        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesListBox.SelectedItem is NoteItem item && item != _currentNote)
            {
                SaveIfDirty();
                OpenNote(item);
            }
        }

        private void NotesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(NotesListBox, e.OriginalSource as DependencyObject) is ListBoxItem)
                Rename_Click(sender, e);
        }

        private string? ValidateName(string name, NoteItem? self)
        {
            if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                return "名称不能包含 \\ / : * ? \" < > | 等字符。";
            if (name.EndsWith('.') || name.Length > 120)
                return "名称无效。";
            var path = System.IO.Path.Combine(NotesDir, name + ".rtf");
            var same = self != null && string.Equals(self.FilePath, path, StringComparison.OrdinalIgnoreCase);
            if (!same && File.Exists(path))
                return "已存在同名笔记。";
            return null;
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            var baseName = "新笔记 " + DateTime.Now.ToString("MMdd");
            var suggestion = baseName;
            for (var i = 2; File.Exists(System.IO.Path.Combine(NotesDir, suggestion + ".rtf")); i++)
                suggestion = $"{baseName} ({i})";

            var name = InputBox.Prompt(this, "新建笔记", suggestion, n => ValidateName(n, null), "输入笔记名称");
            if (name == null) return;

            SaveIfDirty();
            var path = System.IO.Path.Combine(NotesDir, name + ".rtf");
            try
            {
                var empty = new FlowDocument(new Paragraph());
                using (var fs = File.Create(path))
                    new TextRange(empty.ContentStart, empty.ContentEnd).Save(fs, DataFormats.Rtf);
            }
            catch (Exception ex)
            {
                Toast.Error($"创建失败：{ex.Message}");
                return;
            }

            var item = new NoteItem(path, DateTime.Now);
            _notes.Insert(0, item);
            UpdateNoteCount();
            NoteFilterBox.Text = string.Empty;
            NotesListBox.SelectedItem = item;
            NotesListBox.ScrollIntoView(item);
            Dispatcher.BeginInvoke(() => NoteRichTextBox.Focus(), DispatcherPriority.Input);
        }

        private NoteItem? TargetNote => NotesListBox.SelectedItem as NoteItem ?? _currentNote;

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            var item = TargetNote;
            if (item == null)
            {
                Toast.Info("请先选择一篇笔记。");
                return;
            }

            var r = MessageBox.Show($"确定删除笔记“{item.Name}”吗？\n文件将移入回收站。", "删除笔记",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            if (item == _currentNote)
            {
                _autoSaveTimer.Stop();
                _dirty = false;
                _currentNote = null;
            }

            try
            {
                if (File.Exists(item.FilePath))
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.FilePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Toast.Error($"删除失败：{ex.Message}");
                return;
            }

            var index = _notes.IndexOf(item);
            _notes.Remove(item);
            UpdateNoteCount();
            Toast.Success($"已删除“{item.Name}”");

            if (_currentNote == null)
            {
                if (_notes.Count > 0)
                    NotesListBox.SelectedItem = _notes[Math.Clamp(index, 0, _notes.Count - 1)];
                if (_currentNote == null)
                    ShowEmpty();
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var item = TargetNote;
            if (item == null) return;

            var name = InputBox.Prompt(this, "重命名笔记", item.Name, n => ValidateName(n, item));
            if (name == null || name == item.Name) return;

            if (item == _currentNote) SaveIfDirty();
            var newPath = System.IO.Path.Combine(NotesDir, name + ".rtf");
            try
            {
                File.Move(item.FilePath, newPath);
                item.FilePath = newPath;
                _notesView.Refresh();
                NotesListBox.SelectedItem = item;
                Toast.Success("已重命名");
            }
            catch (Exception ex)
            {
                Toast.Error($"重命名失败：{ex.Message}");
            }
        }

        private void RevealNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = TargetNote;
                if (item != null && File.Exists(item.FilePath))
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                else
                    Process.Start("explorer.exe", $"\"{NotesDir}\"");
            }
            catch (Exception ex)
            {
                Toast.Error(ex.Message);
            }
        }

        private void ExportText_Click(object sender, RoutedEventArgs e)
        {
            var item = TargetNote;
            if (item == null) return;

            var dlg = new SaveFileDialog
            {
                Title = "导出为纯文本",
                FileName = item.Name + ".txt",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                string text;
                if (item == _currentNote)
                {
                    var doc = NoteRichTextBox.Document;
                    text = new TextRange(doc.ContentStart, doc.ContentEnd).Text;
                }
                else
                {
                    var doc = new FlowDocument();
                    LoadInto(doc, item.FilePath);
                    text = new TextRange(doc.ContentStart, doc.ContentEnd).Text;
                }
                File.WriteAllText(dlg.FileName, text, new UTF8Encoding(true));
                Toast.Success("已导出");
            }
            catch (Exception ex)
            {
                Toast.Error($"导出失败：{ex.Message}");
            }
        }

        #endregion

        #region 打开 / 保存

        private static void LoadInto(FlowDocument doc, string path)
        {
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0)
            {
                doc.Blocks.Clear();
                doc.Blocks.Add(new Paragraph());
                return;
            }

            try
            {
                using var fs = File.OpenRead(path);
                range.Load(fs, DataFormats.Rtf);
            }
            catch
            {
                // 不是合法的 RTF：按纯文本打开，避免内容丢失
                doc.Blocks.Clear();
                doc.Blocks.Add(new Paragraph(new Run(File.ReadAllText(path))));
            }
        }

        private void OpenNote(NoteItem item)
        {
            _loading = true;
            try
            {
                LoadInto(NoteRichTextBox.Document, item.FilePath);
                _currentNote = item;
                _dirty = false;
                _autoSaveTimer.Stop();

                // 清空撤销栈，避免撤销到上一篇笔记的内容
                NoteRichTextBox.IsUndoEnabled = false;
                NoteRichTextBox.IsUndoEnabled = true;

                EmptyOverlay.Visibility = Visibility.Collapsed;
                NoteRichTextBox.IsEnabled = true;
                NoteRichTextBox.CaretPosition = NoteRichTextBox.Document.ContentStart;
                NoteRichTextBox.ScrollToHome();
                SetStatus("已打开 " + item.Name, StatusKind.Ok);
            }
            catch (Exception ex)
            {
                Toast.Error($"打开笔记失败：{ex.Message}");
            }
            finally
            {
                _loading = false;
            }

            _searchAnchor = null;
            if (SearchPanel.Visibility == Visibility.Visible)
                RunSearch(fromAnchor: true);
            UpdateWordCount();
            UpdateToolbarState();
        }

        private void ShowEmpty()
        {
            _loading = true;
            NoteRichTextBox.Document.Blocks.Clear();
            _loading = false;
            _currentNote = null;
            _dirty = false;
            EmptyOverlay.Visibility = Visibility.Visible;
            NoteRichTextBox.IsEnabled = false;
            SearchPanel.Visibility = Visibility.Collapsed;
            WordCountText.Text = string.Empty;
            SetStatus("就绪", StatusKind.Ok);
        }

        /// <summary>有未保存的修改时立即保存（切换笔记、关闭程序时调用）。</summary>
        public bool SaveIfDirty()
        {
            _autoSaveTimer.Stop();
            return !_dirty || SaveCurrent();
        }

        private bool SaveCurrent()
        {
            var note = _currentNote;
            if (note == null) return true;

            try
            {
                var doc = NoteRichTextBox.Document;
                var tmp = note.FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                    new TextRange(doc.ContentStart, doc.ContentEnd).Save(fs, DataFormats.Rtf);
                File.Move(tmp, note.FilePath, true);

                _dirty = false;
                note.Modified = DateTime.Now;
                SetStatus("已保存 " + DateTime.Now.ToString("HH:mm:ss"), StatusKind.Ok);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("保存失败", StatusKind.Error);
                Toast.Error($"保存“{note.Name}”失败：{ex.Message}");
                return false;
            }
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null) return;
            _autoSaveTimer.Stop();
            if (SaveCurrent()) Toast.Success("已保存");
        }

        private enum StatusKind { Ok, Dirty, Error }

        private void SetStatus(string text, StatusKind kind)
        {
            StatusTextBlock.Text = text;
            StatusDot.Fill = kind switch
            {
                StatusKind.Dirty => BrushFrom("#F59E0B"),
                StatusKind.Error => BrushFrom("#EF4444"),
                _ => BrushFrom("#10B981")
            };
        }

        private void NoteRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _currentNote == null) return;

            if (!_dirty)
            {
                _dirty = true;
                SetStatus("未保存", StatusKind.Dirty);
            }

            _autoSaveTimer.Stop();
            if (AutoSaveCheck.IsChecked == true) _autoSaveTimer.Start();
            _countTimer.Stop();
            _countTimer.Start();
        }

        private void UpdateWordCount()
        {
            if (_currentNote == null)
            {
                WordCountText.Text = string.Empty;
                return;
            }

            var doc = NoteRichTextBox.Document;
            var text = new TextRange(doc.ContentStart, doc.ContentEnd).Text;
            int cjk = 0, words = 0, chars = 0;
            var inWord = false;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    inWord = false;
                    continue;
                }
                chars++;
                if (c >= 0x2E80 && c <= 0x9FFF || c >= 0xF900 && c <= 0xFAFF)
                {
                    cjk++;
                    inWord = false;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    if (!inWord) words++;
                    inWord = true;
                }
                else
                {
                    inWord = false;
                }
            }
            WordCountText.Text = $"{cjk + words} 字 · {chars} 字符";
        }

        #endregion

        #region 快捷键

        private void View_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                if (SearchPanel.Visibility == Visibility.Visible)
                {
                    CloseSearch();
                    e.Handled = true;
                }
                return;
            }

            if (!ctrl || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;

            switch (key)
            {
                case Key.S:
                    SaveNote_Click(this, e);
                    e.Handled = true;
                    break;
                case Key.N when !shift:
                    NewNote_Click(this, e);
                    e.Handled = true;
                    break;
                case Key.F:
                    OpenSearch(replace: false);
                    e.Handled = true;
                    break;
                case Key.H:
                    OpenSearch(replace: true);
                    e.Handled = true;
                    break;
            }
        }

        private void NoteRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            var ctrl = mods.HasFlag(ModifierKeys.Control);
            var shift = mods.HasFlag(ModifierKeys.Shift);

            if (ctrl && !mods.HasFlag(ModifierKeys.Alt))
            {
                switch (e.Key)
                {
                    case Key.V when shift:
                        PastePlainText();
                        e.Handled = true;
                        return;
                    case Key.K:
                        InsertLink_Click(this, e);
                        e.Handled = true;
                        return;
                    case Key.D:
                        ToggleStrikethrough();
                        e.Handled = true;
                        return;
                    case Key.D1:
                    case Key.NumPad1:
                        ApplyParagraphStyle("H1");
                        e.Handled = true;
                        return;
                    case Key.D2:
                    case Key.NumPad2:
                        ApplyParagraphStyle("H2");
                        e.Handled = true;
                        return;
                    case Key.D3:
                    case Key.NumPad3:
                        ApplyParagraphStyle("H3");
                        e.Handled = true;
                        return;
                    case Key.D0:
                    case Key.NumPad0:
                        ApplyParagraphStyle("Normal");
                        e.Handled = true;
                        return;
                }
            }

            if (e.Key == Key.Enter && !shift && !ctrl)
                HandleEnterInChecklist(e);
        }

        #endregion

        #region 工具栏状态

        private void NoteRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            _toolbarTimer.Stop();
            _toolbarTimer.Start();
        }

        private void UpdateToolbarState()
        {
            if (_currentNote == null) return;
            _updatingToolbar = true;
            try
            {
                var sel = NoteRichTextBox.Selection;

                BoldButton.IsChecked = sel.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight w && w >= FontWeights.SemiBold;
                ItalicButton.IsChecked = sel.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle s && s == FontStyles.Italic;
                var decorations = sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
                UnderlineButton.IsChecked = HasDecoration(decorations, TextDecorationLocation.Underline);
                StrikeButton.IsChecked = HasDecoration(decorations, TextDecorationLocation.Strikethrough);

                var family = sel.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
                InlineCodeButton.IsChecked = IsCodeFont(family) && !IsCodeParagraph(sel.Start.Paragraph);

                if (family != null && FontFamilyCombo.ItemsSource is List<FontEntry> fonts)
                {
                    var first = family.Source.Split(',')[0].Trim();
                    FontFamilyCombo.SelectedItem = fonts.FirstOrDefault(f =>
                        string.Equals(f.Family.Source, first, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    FontFamilyCombo.SelectedItem = null;
                }

                if (sel.GetPropertyValue(TextElement.FontSizeProperty) is double size)
                {
                    var text = FormatSize(size);
                    FontSizeCombo.SelectedItem = FontSizeCombo.Items.Cast<string>().FirstOrDefault(i => i == text);
                    FontSizeCombo.Text = text;
                }
                else
                {
                    FontSizeCombo.SelectedItem = null;
                    FontSizeCombo.Text = string.Empty;
                }

                var align = sel.GetPropertyValue(Paragraph.TextAlignmentProperty);
                AlignLeftButton.IsChecked = align is TextAlignment.Left;
                AlignCenterButton.IsChecked = align is TextAlignment.Center;
                AlignRightButton.IsChecked = align is TextAlignment.Right;
                AlignJustifyButton.IsChecked = align is TextAlignment.Justify;

                var style = DetectParagraphStyle(sel.Start.Paragraph);
                ParagraphStyleCombo.SelectedItem = ParagraphStyleCombo.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => (string)i.Tag == style);
            }
            finally
            {
                _updatingToolbar = false;
            }
        }

        private static string FormatSize(double size)
        {
            // WPF 以 DIP 存字号，界面上按“磅”显示（与 Word 一致）
            var pt = Math.Round(size * 72.0 / 96.0 * 2) / 2;
            return pt.ToString("0.#");
        }

        private static double PointsToDip(double pt) => pt * 96.0 / 72.0;

        private static bool HasDecoration(TextDecorationCollection? c, TextDecorationLocation loc)
            => c != null && c.Any(d => d.Location == loc);

        private static bool IsCodeFont(FontFamily? f)
            => f != null && f.Source.StartsWith("Consolas", StringComparison.OrdinalIgnoreCase);

        private bool IsCodeParagraph(Paragraph? p)
            => p != null && p.Background != null && IsCodeFont(p.FontFamily);

        private string DetectParagraphStyle(Paragraph? p)
        {
            if (p == null) return "Normal";
            if (p.BorderThickness.Left >= 2 && p.BorderThickness.Top == 0) return "Quote";
            if (IsCodeParagraph(p)) return "Code";

            var range = new TextRange(p.ContentStart, p.ContentEnd);
            var sizeObj = range.IsEmpty ? p.FontSize : range.GetPropertyValue(TextElement.FontSizeProperty);
            var weightObj = range.IsEmpty ? p.FontWeight : range.GetPropertyValue(TextElement.FontWeightProperty);
            if (sizeObj is double size && weightObj is FontWeight w && w >= FontWeights.SemiBold)
            {
                if (size >= 24) return "H1";
                if (size >= 19.5) return "H2";
                if (size >= 16.5) return "H3";
            }
            return "Normal";
        }

        #endregion

        #region 文字格式

        private bool CanEdit => _currentNote != null && NoteRichTextBox.IsEnabled;

        private void FocusEditor() => NoteRichTextBox.Focus();

        private void ApplyInline(DependencyProperty property, object? value)
        {
            if (!CanEdit) return;
            NoteRichTextBox.Selection.ApplyPropertyValue(property, value);
            FocusEditor();
            UpdateToolbarState();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            NoteRichTextBox.Undo();
            FocusEditor();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            NoteRichTextBox.Redo();
            FocusEditor();
        }

        private void ExecuteEditing(RoutedUICommand command)
        {
            if (!CanEdit)
            {
                UpdateToolbarState();
                return;
            }
            command.Execute(null, NoteRichTextBox);
            FocusEditor();
            UpdateToolbarState();
        }

        private void Bold_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.ToggleBold);
        private void Italic_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.ToggleItalic);
        private void Underline_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.ToggleUnderline);
        private void Strike_Click(object sender, RoutedEventArgs e) => ToggleStrikethrough();
        private void Bullets_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.ToggleBullets);
        private void Numbering_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.ToggleNumbering);
        private void Indent_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.IncreaseIndentation);
        private void Outdent_Click(object sender, RoutedEventArgs e) => ExecuteEditing(EditingCommands.DecreaseIndentation);

        private void ToggleStrikethrough()
        {
            if (!CanEdit) return;
            var sel = NoteRichTextBox.Selection;
            var current = sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            var result = new TextDecorationCollection();
            if (current != null)
                foreach (var d in current.Where(d => d.Location != TextDecorationLocation.Strikethrough))
                    result.Add(d);
            if (!HasDecoration(current, TextDecorationLocation.Strikethrough))
                result.Add(TextDecorations.Strikethrough[0]);
            sel.ApplyPropertyValue(Inline.TextDecorationsProperty, result);
            FocusEditor();
            UpdateToolbarState();
        }

        private void InlineCode_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var sel = NoteRichTextBox.Selection;
            var isCode = IsCodeFont(sel.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily);
            NoteRichTextBox.BeginChange();
            if (isCode)
            {
                sel.ApplyPropertyValue(TextElement.FontFamilyProperty, DefaultFont);
                sel.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                sel.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultForeground);
            }
            else
            {
                sel.ApplyPropertyValue(TextElement.FontFamilyProperty, CodeFont);
                sel.ApplyPropertyValue(TextElement.BackgroundProperty, CodeBackground);
                sel.ApplyPropertyValue(TextElement.ForegroundProperty, CodeForeground);
            }
            NoteRichTextBox.EndChange();
            FocusEditor();
            UpdateToolbarState();
        }

        private void FontColorButton_Click(object sender, RoutedEventArgs e) => FontColorPopup.IsOpen = true;
        private void HighlightButton_Click(object sender, RoutedEventArgs e) => HighlightPopup.IsOpen = true;

        private void ResetFontColor_Click(object sender, RoutedEventArgs e)
        {
            FontColorPopup.IsOpen = false;
            ApplyInline(TextElement.ForegroundProperty, DefaultForeground);
        }

        private void ClearHighlight_Click(object sender, RoutedEventArgs e)
        {
            HighlightPopup.IsOpen = false;
            ApplyInline(TextElement.BackgroundProperty, null);
        }

        private void ClearFormat_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            NoteRichTextBox.Selection.ClearAllProperties();
            FocusEditor();
            UpdateToolbarState();
        }

        private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingToolbar || FontFamilyCombo.SelectedItem is not FontEntry font) return;
            ApplyInline(TextElement.FontFamilyProperty, font.Family);
        }

        private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingToolbar || FontSizeCombo.SelectedItem is not string s) return;
            ApplyFontSize(s);
        }

        private void FontSizeCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            ApplyFontSize(FontSizeCombo.Text);
            e.Handled = true;
        }

        private void FontSizeCombo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 未按回车确认的输入不生效，恢复显示当前字号
            if (!FontSizeCombo.IsKeyboardFocusWithin)
                UpdateToolbarState();
        }

        private void ApplyFontSize(string? text)
        {
            if (!double.TryParse(text, out var pt) || pt < 1 || pt > 400)
            {
                UpdateToolbarState();
                return;
            }
            ApplyInline(TextElement.FontSizeProperty, PointsToDip(pt));
        }

        private void Align_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<TextAlignment>(tag, out var align)) return;
            ApplyInline(Paragraph.TextAlignmentProperty, align);
        }

        #endregion

        #region 段落样式

        private void ParagraphStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingToolbar || ParagraphStyleCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
            ApplyParagraphStyle(tag);
        }

        private void ApplyParagraphStyle(string style)
        {
            if (!CanEdit) return;
            var paragraphs = GetSelectedParagraphs();
            if (paragraphs.Count == 0) return;

            NoteRichTextBox.BeginChange();
            try
            {
                foreach (var p in paragraphs)
                    ApplyParagraphStyle(p, style);
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            FocusEditor();
            UpdateToolbarState();
        }

        private static void ApplyParagraphStyle(Paragraph p, string style)
        {
            var range = new TextRange(p.ContentStart, p.ContentEnd);

            // 先恢复为正文
            p.FontFamily = DefaultFont;
            p.FontSize = DefaultFontSize;
            p.FontWeight = FontWeights.Normal;
            p.FontStyle = FontStyles.Normal;
            p.Foreground = DefaultForeground;
            p.Background = null;
            p.BorderBrush = null;
            p.BorderThickness = new Thickness(0);
            p.Padding = new Thickness(0);
            p.Margin = new Thickness(0, 0, 0, 4);

            var wasCode = IsCodeFont(range.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily);
            range.ApplyPropertyValue(TextElement.FontSizeProperty, DefaultFontSize);
            range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            if (wasCode)
            {
                range.ApplyPropertyValue(TextElement.FontFamilyProperty, DefaultFont);
                range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultForeground);
            }

            void Heading(double size, double top, double bottom)
            {
                p.FontSize = size;
                p.FontWeight = FontWeights.Bold;
                p.Margin = new Thickness(0, top, 0, bottom);
                range.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            }

            switch (style)
            {
                case "H1":
                    Heading(26, 12, 6);
                    break;
                case "H2":
                    Heading(21, 10, 4);
                    break;
                case "H3":
                    Heading(17, 8, 4);
                    break;
                case "Quote":
                    p.BorderBrush = QuoteBorder;
                    p.BorderThickness = new Thickness(3, 0, 0, 0);
                    p.Padding = new Thickness(10, 4, 8, 4);
                    p.Background = QuoteBackground;
                    p.Foreground = QuoteForeground;
                    p.Margin = new Thickness(0, 4, 0, 6);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, QuoteForeground);
                    break;
                case "Code":
                    p.FontFamily = CodeFont;
                    p.FontSize = 13;
                    p.Background = CodeBackground;
                    p.Padding = new Thickness(10, 6, 10, 6);
                    p.Margin = new Thickness(0, 4, 0, 6);
                    range.ApplyPropertyValue(TextElement.FontFamilyProperty, CodeFont);
                    range.ApplyPropertyValue(TextElement.FontSizeProperty, 13.0);
                    range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultForeground);
                    break;
                default:
                    if (range.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush b &&
                        b.Color == ((SolidColorBrush)QuoteForeground).Color)
                        range.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultForeground);
                    break;
            }
        }

        private List<Paragraph> GetSelectedParagraphs()
        {
            var sel = NoteRichTextBox.Selection;
            var result = new List<Paragraph>();
            CollectParagraphs(NoteRichTextBox.Document.Blocks, sel.Start, sel.End, result);
            if (result.Count == 0 && sel.Start.Paragraph != null)
                result.Add(sel.Start.Paragraph);
            return result;
        }

        private static void CollectParagraphs(IEnumerable<Block> blocks, TextPointer start, TextPointer end, List<Paragraph> result)
        {
            foreach (var block in blocks)
            {
                if (block.ContentEnd.CompareTo(start) < 0) continue;
                if (block.ContentStart.CompareTo(end) > 0) break;

                switch (block)
                {
                    case Paragraph p:
                        result.Add(p);
                        break;
                    case List list:
                        foreach (var item in list.ListItems)
                            CollectParagraphs(item.Blocks, start, end, result);
                        break;
                    case Section section:
                        CollectParagraphs(section.Blocks, start, end, result);
                        break;
                    case Table table:
                        foreach (var group in table.RowGroups)
                            foreach (var row in group.Rows)
                                foreach (var cell in row.Cells)
                                    CollectParagraphs(cell.Blocks, start, end, result);
                        break;
                }
            }
        }

        #endregion

        #region 待办复选框

        private static TextRange? GetPrefixRange(Paragraph p, int length)
        {
            var start = p.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            var end = start;
            for (var i = 0; i < length && end != null; i++)
                end = end.GetNextInsertionPosition(LogicalDirection.Forward);
            if (end == null || end.CompareTo(p.ContentEnd) > 0) return null;
            return new TextRange(start, end);
        }

        private static bool IsCheckParagraph(Paragraph p)
        {
            var r = GetPrefixRange(p, 1);
            return r != null && (r.Text == BoxUnchecked || r.Text == BoxChecked);
        }

        private void Checkbox_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var paragraphs = GetSelectedParagraphs();
            if (paragraphs.Count == 0) return;

            var allChecklist = paragraphs.All(IsCheckParagraph);
            NoteRichTextBox.BeginChange();
            try
            {
                foreach (var p in paragraphs)
                {
                    if (allChecklist)
                    {
                        var two = GetPrefixRange(p, 2);
                        var prefix = two != null && two.Text.Length == 2 && two.Text[1] == ' ' ? two : GetPrefixRange(p, 1);
                        if (prefix != null) prefix.Text = string.Empty;
                    }
                    else if (!IsCheckParagraph(p))
                    {
                        p.ContentStart.GetInsertionPosition(LogicalDirection.Forward).InsertTextInRun(BoxUnchecked + " ");
                    }
                }
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }

            var last = paragraphs[^1];
            if (!allChecklist && new TextRange(last.ContentStart, last.ContentEnd).Text.Trim() == BoxUnchecked)
                NoteRichTextBox.CaretPosition = last.ContentEnd;
            FocusEditor();
        }

        private void HandleEnterInChecklist(KeyEventArgs e)
        {
            var p = NoteRichTextBox.CaretPosition.Paragraph;
            if (p == null || !NoteRichTextBox.Selection.IsEmpty || !IsCheckParagraph(p)) return;

            var text = new TextRange(p.ContentStart, p.ContentEnd).Text.Trim();
            if (text == BoxUnchecked || text == BoxChecked)
            {
                // 空的待办项上回车：结束待办列表
                new TextRange(p.ContentStart, p.ContentEnd).Text = string.Empty;
                e.Handled = true;
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                var np = NoteRichTextBox.CaretPosition.Paragraph;
                if (np == null || np == p || IsCheckParagraph(np)) return;
                np.ContentStart.GetInsertionPosition(LogicalDirection.Forward).InsertTextInRun(BoxUnchecked + " ");
                var prefix = GetPrefixRange(np, 2);
                if (prefix != null)
                {
                    prefix.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection());
                    prefix.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultForeground);
                    NoteRichTextBox.CaretPosition = prefix.End;
                }
            }, DispatcherPriority.Input);
        }

        private bool TryToggleCheckbox(TextPointer pos)
        {
            foreach (var dir in new[] { LogicalDirection.Forward, LogicalDirection.Backward })
            {
                if (pos.GetPointerContext(dir) != TextPointerContext.Text) continue;
                var run = pos.GetTextInRun(dir);
                if (run.Length == 0) continue;
                var ch = dir == LogicalDirection.Forward ? run[0].ToString() : run[^1].ToString();
                if (ch != BoxUnchecked && ch != BoxChecked) continue;

                var other = pos.GetPositionAtOffset(dir == LogicalDirection.Forward ? 1 : -1);
                if (other == null) continue;
                var range = dir == LogicalDirection.Forward ? new TextRange(pos, other) : new TextRange(other, pos);
                var check = ch == BoxUnchecked;

                NoteRichTextBox.BeginChange();
                range.Text = check ? BoxChecked : BoxUnchecked;

                // 已完成项：其余文字加删除线并变灰
                var p = range.Start.Paragraph;
                if (p != null && IsCheckParagraph(p))
                {
                    var rest = new TextRange(range.End, p.ContentEnd);
                    rest.ApplyPropertyValue(Inline.TextDecorationsProperty,
                        check ? TextDecorations.Strikethrough : new TextDecorationCollection());
                    rest.ApplyPropertyValue(TextElement.ForegroundProperty,
                        check ? BrushFrom("#94A3B8") : DefaultForeground);
                }
                NoteRichTextBox.EndChange();
                return true;
            }
            return false;
        }

        #endregion

        #region 鼠标：链接 / 复选框 / 缩放

        private void NoteRichTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanEdit || e.ClickCount > 1) return;
            var pos = NoteRichTextBox.GetPositionFromPoint(e.GetPosition(NoteRichTextBox), false);
            if (pos == null) return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                for (DependencyObject? el = pos.Parent; el != null; el = (el as FrameworkContentElement)?.Parent)
                {
                    if (el is Hyperlink { NavigateUri: not null } link)
                    {
                        OpenUri(link.NavigateUri);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (Keyboard.Modifiers == ModifierKeys.None && TryToggleCheckbox(pos))
                e.Handled = true;
        }

        private static void OpenUri(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString()) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Toast.Error($"无法打开链接：{ex.Message}");
            }
        }

        private void NoteRichTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
            SetZoom(_zoom + (e.Delta > 0 ? 0.1 : -0.1));
            e.Handled = true;
        }

        private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

        private void SetZoom(double zoom)
        {
            _zoom = Math.Round(Math.Clamp(zoom, 0.5, 2.0), 1);
            NoteRichTextBox.LayoutTransform = Math.Abs(_zoom - 1.0) < 0.01 ? Transform.Identity : new ScaleTransform(_zoom, _zoom);
            ZoomButton.Content = $"{Math.Round(_zoom * 100)}%";
        }

        #endregion

        #region 插入

        private void InsertImage_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var dlg = new OpenFileDialog
            {
                Title = "插入图片",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|所有文件|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            foreach (var f in dlg.FileNames)
                InsertImageFile(f);
        }

        private void InsertImageFile(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                InsertImage(bmp);
            }
            catch (Exception ex)
            {
                Toast.Error($"无法加载图片：{ex.Message}");
            }
        }

        private void InsertImage(BitmapSource source)
        {
            if (!CanEdit) return;
            var width = Math.Min(source.PixelWidth, MaxImageWidth);
            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                Width = width,
                Height = width * source.PixelHeight / Math.Max(1, source.PixelWidth)
            };

            NoteRichTextBox.BeginChange();
            try
            {
                var sel = NoteRichTextBox.Selection;
                if (!sel.IsEmpty) sel.Text = string.Empty;
                var pos = NoteRichTextBox.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
                var container = new InlineUIContainer(image, pos) { BaselineAlignment = BaselineAlignment.Bottom };
                NoteRichTextBox.CaretPosition = container.ElementEnd;
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            FocusEditor();
        }

        private void InsertLink_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var sel = NoteRichTextBox.Selection;

            var initial = "https://";
            if (!sel.IsEmpty && Uri.TryCreate(sel.Text.Trim(), UriKind.Absolute, out var selUri))
                initial = selUri.AbsoluteUri;

            var url = InputBox.Prompt(this, "插入链接", initial, v =>
                Uri.TryCreate(NormalizeUrl(v), UriKind.Absolute, out _) ? null : "请输入有效的网址，例如 https://example.com",
                sel.IsEmpty ? "输入网址" : "为选中文字添加链接");
            if (url == null) return;
            var uri = new Uri(NormalizeUrl(url));

            NoteRichTextBox.BeginChange();
            try
            {
                Hyperlink link;
                if (!sel.IsEmpty && sel.Start.Paragraph != null && sel.Start.Paragraph == sel.End.Paragraph)
                {
                    link = new Hyperlink(sel.Start, sel.End);
                }
                else
                {
                    if (!sel.IsEmpty) sel.Text = string.Empty;
                    var pos = NoteRichTextBox.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
                    link = new Hyperlink(new Run(uri.AbsoluteUri), pos);
                }
                link.NavigateUri = uri;
                link.ToolTip = "Ctrl+单击打开：" + uri.AbsoluteUri;
                NoteRichTextBox.CaretPosition = link.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
            }
            catch (Exception ex)
            {
                Toast.Error($"插入链接失败：{ex.Message}");
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            FocusEditor();
        }

        private static string NormalizeUrl(string v)
        {
            v = v.Trim();
            if (!Regex.IsMatch(v, @"^[a-zA-Z][a-zA-Z0-9+.-]*:") && v.Contains('.'))
                v = "https://" + v;
            return v;
        }

        /// <summary>获取光标所在的顶层块（直接属于 FlowDocument 的块）。</summary>
        private Block? GetTopLevelBlock()
        {
            DependencyObject? el = NoteRichTextBox.CaretPosition.Paragraph ?? NoteRichTextBox.CaretPosition.Parent;
            Block? lastBlock = null;
            while (el is FrameworkContentElement fce)
            {
                if (fce is Block b) lastBlock = b;
                if (fce.Parent is FlowDocument) return lastBlock;
                el = fce.Parent;
            }
            return null;
        }

        private void InsertBlocks(params Block[] blocks)
        {
            var doc = NoteRichTextBox.Document;
            var anchor = GetTopLevelBlock();
            // 光标位于空段落时，用插入内容替换该空段落
            var emptyAnchor = anchor is Paragraph p && !p.Inlines.Any(i => i is InlineUIContainer)
                              && string.IsNullOrWhiteSpace(new TextRange(p.ContentStart, p.ContentEnd).Text)
                ? p
                : null;

            var after = anchor;
            foreach (var block in blocks)
            {
                if (after == null)
                    doc.Blocks.Add(block);
                else
                    doc.Blocks.InsertAfter(after, block);
                after = block;
            }

            if (emptyAnchor != null)
                doc.Blocks.Remove(emptyAnchor);
        }

        private void InsertTable_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var regex = new Regex(@"^\s*(\d{1,2})\s*[xX×*,，\s]\s*(\d{1,2})\s*$");
            var input = InputBox.Prompt(this, "插入表格", "3x3", v =>
            {
                var m = regex.Match(v);
                if (!m.Success) return "格式：行数x列数，例如 3x4";
                var r = int.Parse(m.Groups[1].Value);
                var c = int.Parse(m.Groups[2].Value);
                return r is < 1 or > 50 || c is < 1 or > 12 ? "行数 1~50，列数 1~12" : null;
            }, "行数 x 列数（第一行为表头）");
            if (input == null) return;

            var match = regex.Match(input);
            int rows = int.Parse(match.Groups[1].Value), cols = int.Parse(match.Groups[2].Value);

            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(0, 6, 0, 6)
            };
            for (var c = 0; c < cols; c++)
                table.Columns.Add(new TableColumn());

            var group = new TableRowGroup();
            for (var r = 0; r < rows; r++)
            {
                var row = new TableRow();
                if (r == 0)
                {
                    row.Background = TableHeaderBrush;
                    row.FontWeight = FontWeights.Bold;
                }
                for (var c = 0; c < cols; c++)
                {
                    row.Cells.Add(new TableCell(new Paragraph { Margin = new Thickness(0) })
                    {
                        BorderBrush = LineBrush,
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(6, 3, 6, 3)
                    });
                }
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);

            NoteRichTextBox.BeginChange();
            try
            {
                InsertBlocks(table, new Paragraph());
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            NoteRichTextBox.CaretPosition = group.Rows[0].Cells[0].ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            FocusEditor();
        }

        private void InsertDivider_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var divider = new Paragraph
            {
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 4, 0, 10),
                FontSize = 2,
                LineHeight = 2
            };
            var next = new Paragraph();

            NoteRichTextBox.BeginChange();
            try
            {
                InsertBlocks(divider, next);
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            NoteRichTextBox.CaretPosition = next.ContentStart;
            FocusEditor();
        }

        private void InsertDate_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var sel = NoteRichTextBox.Selection;
            sel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            sel.Select(sel.End, sel.End);
            FocusEditor();
        }

        #endregion

        #region 粘贴

        private void RichTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            var data = e.DataObject;
            var hasText = data.GetDataPresent(DataFormats.Rtf) || data.GetDataPresent(DataFormats.UnicodeText) ||
                          data.GetDataPresent(DataFormats.Text);

            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
            {
                var images = files.Where(f => ImageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())).ToList();
                if (images.Count > 0)
                {
                    e.CancelCommand();
                    Dispatcher.BeginInvoke(() => images.ForEach(InsertImageFile));
                    return;
                }
            }

            if (!hasText && data.GetDataPresent(DataFormats.Bitmap))
            {
                e.CancelCommand();
                var image = GetClipboardImage(data);
                if (image != null)
                    Dispatcher.BeginInvoke(() => InsertImage(image));
            }
        }

        private static BitmapSource? GetClipboardImage(IDataObject data)
        {
            try
            {
                // 优先使用 PNG 格式（保留透明度），否则退回到位图
                if (data.GetDataPresent("PNG") && data.GetData("PNG") is MemoryStream png)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = png;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }

                if (data.GetData(DataFormats.Bitmap) is BitmapSource src)
                {
                    // 重新编码一次，脱离剪贴板句柄，保存为 RTF 时更稳定
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(src));
                    var ms = new MemoryStream();
                    encoder.Save(ms);
                    ms.Position = 0;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                // 剪贴板内容异常时忽略
            }
            return null;
        }

        private void PastePlainText()
        {
            if (!CanEdit || !Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Replace("\r\n", "\n");
            var sel = NoteRichTextBox.Selection;
            NoteRichTextBox.BeginChange();
            sel.Text = text;
            sel.Select(sel.End, sel.End);
            NoteRichTextBox.EndChange();
        }

        #endregion

        #region 查找 / 替换

        private void ToggleFind_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                CloseSearch();
            else
                OpenSearch(replace: false);
        }

        private void OpenSearch(bool replace)
        {
            if (_currentNote == null) return;
            SearchPanel.Visibility = Visibility.Visible;
            if (replace)
            {
                ReplaceToggle.IsChecked = true;
                ReplaceRow.Visibility = Visibility.Visible;
            }

            var sel = NoteRichTextBox.Selection;
            _searchAnchor = sel.Start;
            if (!sel.IsEmpty && !sel.Text.Contains('\n') && sel.Text.Length <= 100)
                SearchTextBox.Text = sel.Text;

            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            RunSearch(fromAnchor: true);
        }

        private void CloseSearch()
        {
            _searchTimer.Stop();
            SearchPanel.Visibility = Visibility.Collapsed;
            if (_currentNote != null) FocusEditor();
        }

        private void CloseSearch_Click(object sender, RoutedEventArgs e) => CloseSearch();

        private void ReplaceToggle_Click(object sender, RoutedEventArgs e)
        {
            ReplaceRow.Visibility = ReplaceToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ReplaceToggle.Content = ReplaceToggle.IsChecked == true ? "\uE70D" : "\uE76C";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void MatchCase_Click(object sender, RoutedEventArgs e) => RunSearch(fromAnchor: true);

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _searchTimer.Stop();
            MoveMatch(forward: !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }

        private void ReplaceTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                ReplaceAll_Click(sender, e);
            else
                ReplaceOne_Click(sender, e);
            e.Handled = true;
        }

        private void PreviousMatch_Click(object sender, RoutedEventArgs e) => MoveMatch(forward: false);
        private void NextMatch_Click(object sender, RoutedEventArgs e) => MoveMatch(forward: true);

        /// <summary>在整篇文档中查找（可跨越不同格式的 Run），返回所有匹配范围。</summary>
        private List<TextRange> FindAll(string query)
        {
            var results = new List<TextRange>();
            if (string.IsNullOrEmpty(query)) return results;

            var segments = new List<(TextPointer Start, int Offset, int Length)>();
            var sb = new StringBuilder();
            var doc = NoteRichTextBox.Document;

            for (var nav = doc.ContentStart; nav != null && nav.CompareTo(doc.ContentEnd) < 0;)
            {
                var ctx = nav.GetPointerContext(LogicalDirection.Forward);
                if (ctx == TextPointerContext.Text)
                {
                    var t = nav.GetTextInRun(LogicalDirection.Forward);
                    segments.Add((nav, sb.Length, t.Length));
                    sb.Append(t);
                    nav = nav.GetPositionAtOffset(t.Length, LogicalDirection.Forward);
                }
                else
                {
                    if (ctx is TextPointerContext.ElementStart or TextPointerContext.EmbeddedElement)
                    {
                        var el = nav.GetAdjacentElement(LogicalDirection.Forward);
                        if (el is Block or LineBreak or InlineUIContainer or TableCell || ctx == TextPointerContext.EmbeddedElement)
                            sb.Append('\n');
                    }
                    nav = nav.GetNextContextPosition(LogicalDirection.Forward);
                }
            }

            var text = sb.ToString();
            var comparison = MatchCaseToggle.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var seg = 0;
            for (var idx = text.IndexOf(query, comparison); idx >= 0; idx = text.IndexOf(query, idx + query.Length, comparison))
            {
                while (seg < segments.Count && segments[seg].Offset + segments[seg].Length <= idx) seg++;
                if (seg >= segments.Count) break;
                var start = segments[seg].Start.GetPositionAtOffset(idx - segments[seg].Offset);

                var endIdx = idx + query.Length;
                var e = seg;
                while (e < segments.Count && segments[e].Offset + segments[e].Length < endIdx) e++;
                if (e >= segments.Count || start == null) break;
                var end = segments[e].Start.GetPositionAtOffset(endIdx - segments[e].Offset);
                if (end != null) results.Add(new TextRange(start, end));
            }
            return results;
        }

        private void RunSearch(bool fromAnchor)
        {
            if (_currentNote == null || SearchPanel.Visibility != Visibility.Visible) return;
            var matches = FindAll(SearchTextBox.Text);
            if (matches.Count == 0)
            {
                MatchCountTextBlock.Text = "0/0";
                MatchCountTextBlock.Foreground = string.IsNullOrEmpty(SearchTextBox.Text)
                    ? (Brush)FindResource("SecondaryTextBrush")
                    : BrushFrom("#F87171");
                return;
            }

            var anchor = fromAnchor && _searchAnchor != null ? _searchAnchor : NoteRichTextBox.Selection.Start;
            var index = matches.FindIndex(m => m.Start.CompareTo(anchor) >= 0);
            SelectMatch(matches, index < 0 ? 0 : index);
        }

        private void MoveMatch(bool forward)
        {
            if (_currentNote == null) return;
            var matches = FindAll(SearchTextBox.Text);
            if (matches.Count == 0)
            {
                RunSearch(fromAnchor: false);
                return;
            }

            var cur = NoteRichTextBox.Selection.Start;
            int index;
            if (forward)
            {
                index = matches.FindIndex(m => m.Start.CompareTo(cur) > 0);
                if (index < 0) index = 0;
            }
            else
            {
                index = matches.FindLastIndex(m => m.Start.CompareTo(cur) < 0);
                if (index < 0) index = matches.Count - 1;
            }
            SelectMatch(matches, index);
        }

        private void SelectMatch(List<TextRange> matches, int index)
        {
            var m = matches[index];
            NoteRichTextBox.Selection.Select(m.Start, m.End);
            MatchCountTextBlock.Text = $"{index + 1}/{matches.Count}";
            MatchCountTextBlock.Foreground = (Brush)FindResource("SecondaryTextBrush");
            ScrollIntoView(m.Start);
        }

        private void ScrollIntoView(TextPointer pos)
        {
            NoteRichTextBox.UpdateLayout();
            var rect = pos.GetCharacterRect(LogicalDirection.Forward);
            if (rect.IsEmpty) return;
            var viewport = NoteRichTextBox.ViewportHeight;
            if (rect.Top < 40 || rect.Bottom > viewport - 20)
                NoteRichTextBox.ScrollToVerticalOffset(Math.Max(0, NoteRichTextBox.VerticalOffset + rect.Top - viewport / 3));
        }

        private bool SelectionMatchesQuery()
        {
            var q = SearchTextBox.Text;
            var comparison = MatchCaseToggle.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return !string.IsNullOrEmpty(q) && string.Equals(NoteRichTextBox.Selection.Text, q, comparison);
        }

        private void ReplaceOne_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit || string.IsNullOrEmpty(SearchTextBox.Text)) return;
            if (SelectionMatchesQuery())
            {
                var sel = NoteRichTextBox.Selection;
                sel.Text = ReplaceTextBox.Text;
                sel.Select(sel.End, sel.End);
            }
            MoveMatch(forward: true);
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit || string.IsNullOrEmpty(SearchTextBox.Text)) return;
            var matches = FindAll(SearchTextBox.Text);
            if (matches.Count == 0)
            {
                Toast.Info("没有找到匹配项");
                return;
            }

            NoteRichTextBox.BeginChange();
            try
            {
                for (var i = matches.Count - 1; i >= 0; i--)
                    matches[i].Text = ReplaceTextBox.Text;
            }
            finally
            {
                NoteRichTextBox.EndChange();
            }
            MatchCountTextBlock.Text = "0/0";
            Toast.Success($"已替换 {matches.Count} 处");
        }

        #endregion

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            var show = SidebarToggle.IsChecked == true;
            SidebarColumn.Width = new GridLength(show ? 186 : 0);
            SidebarGapColumn.Width = new GridLength(show ? 10 : 0);
            Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
