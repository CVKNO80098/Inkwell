using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Inkwell.App;

/// <summary>One document editor: RichTextBox + live Markdown styling + undo history.</summary>
public sealed class EditorView : Grid
{
    public RichTextBox Rtb { get; private set; }
    public MarkdownStyler Styler { get; private set; }
    public DocumentHistory History { get; private set; }

    Theme _theme;
    AppSettings _settings;
    DispatcherTimer _styleTimer;
    bool _suppress;
    ScrollViewer _scroller;
    bool _caretDirty;
    bool _composing;

    const int GCS_COMPSTR = 0x0008;
    [DllImport("imm32.dll")] static extern IntPtr ImmGetContext(IntPtr hWnd);
    [DllImport("imm32.dll")] static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);
    [DllImport("imm32.dll")] static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    /// <summary>True while an IME composition (e.g. pinyin candidates) is active.</summary>
    bool IsImeComposing()
    {
        var win = Window.GetWindow(Rtb);
        if (win == null) return false;
        IntPtr hwnd = new WindowInteropHelper(win).Handle;
        if (hwnd == IntPtr.Zero) return false;
        IntPtr himc = ImmGetContext(hwnd);
        if (himc == IntPtr.Zero) return false;
        try { return ImmGetCompositionStringW(himc, GCS_COMPSTR, null, 0) > 0; }
        finally { ImmReleaseContext(hwnd, himc); }
    }
    string _cachedText;
    int _revision;

    public event EventHandler TextEdited;
    public event EventHandler CaretMoved;
    public event EventHandler Scrolled;
    public event Action<string[]> FilesDropped;
    public Func<bool> PasteImageHandler;
    public Func<string, bool> OpenLinkHandler;

    public EditorView(Theme theme, AppSettings settings)
    {
        _theme = theme;
        _settings = settings;
        History = new DocumentHistory();
        Styler = new MarkdownStyler();

        Rtb = new RichTextBox
        {
            AcceptsTab = true,
            AcceptsReturn = true,
            IsReadOnly = false,
            IsUndoEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            AllowDrop = true,
            FocusVisualStyle = null,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            
        };
        MarkdownStyler.SetDocumentDefaults(Rtb, theme, settings.EditorFont, settings.EditorFontSize, settings.EditorWidth);
        Styler.Attach(Rtb);
        Styler.Configure(theme, settings.EditorFont, settings.EditorFontSize);
        Children.Add(Rtb);

        Rtb.TextChanged += OnTextChanged;
        Rtb.SelectionChanged += OnSelectionChanged;
        Rtb.PreviewKeyDown += OnPreviewKeyDown;
        Rtb.AddHandler(TextCompositionManager.PreviewTextInputStartEvent, new TextCompositionEventHandler(OnCompositionStart));
        Rtb.AddHandler(TextCompositionManager.PreviewTextInputEvent, new TextCompositionEventHandler(OnCompositionEnd));
        Rtb.PreviewMouseWheel += OnEditorWheel;
        MouseWheel += (s, e) => { if (!e.Handled) OnEditorWheel(s, e); };
        Rtb.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        Rtb.Loaded += (s, e) => { HookScroller(); };

        _styleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _styleTimer.Tick += (s, e) =>
        {
            _styleTimer.Stop();
            if (_composing || IsImeComposing()) { _styleTimer.Start(); return; }
            StyleNow(false);
        };

        Background = Brushes.Transparent;
        Rtb.DragOver += OnDragOver;
        Rtb.Drop += OnDrop;
    }

    public string Text
    {
        get
        {
            if (_cachedText == null) _cachedText = MarkdownStyler.GetText(Rtb);
            return _cachedText;
        }
        set { SetText(value, null); }
    }

    /// <summary>Increments on every document edit; used for cheap dirty checks.</summary>
    public int Revision => _revision;

    public bool SinglePage
    {
        get => Styler.SinglePage;
        set
        {
            if (Styler.SinglePage == value) return;
            Styler.SinglePage = value;
            Styler.ActiveLine = GetLineCol(Rtb.CaretPosition).line;
            Styler.Invalidate();
            StyleNow(true);
        }
    }

    public bool IsComposing => _composing || IsImeComposing();

    void InvalidateText()
    {
        _cachedText = null;
        _revision++;
    }

    public bool IsSuppressing => _suppress;

    void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        InvalidateText();
        // Text snapshot for undo is taken when typing pauses (StyleNow), not per keystroke.
        _caretDirty = false;
        TextEdited?.Invoke(this, EventArgs.Empty);
        _styleTimer.Stop();
        if (_composing) return;
        _styleTimer.Start();
        if (sw.ElapsedMilliseconds >= 8) App.Log("perf textchanged " + sw.ElapsedMilliseconds + "ms rev=" + _revision);
    }

    void OnCompositionStart(object sender, TextCompositionEventArgs e)
    {
        _composing = true;
        _styleTimer.Stop();
    }

    void OnCompositionEnd(object sender, TextCompositionEventArgs e)
    {
        _composing = false;
        ScheduleStyle();
    }

    void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (_composing || IsImeComposing()) { CaretMoved?.Invoke(this, EventArgs.Empty); return; }
        if (Styler.SinglePage)
        {
            int line = GetLineCol(Rtb.CaretPosition).line;
            if (line != Styler.ActiveLine)
            {
                Styler.ActiveLine = line;
                StyleNow(false);
            }
        }
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_suppress) return;
        if (e.VerticalChange != 0 || e.ExtentHeightChange != 0)
            Scrolled?.Invoke(this, EventArgs.Empty);
    }

    void OnEditorWheel(object sender, MouseWheelEventArgs e)
    {
        var sc = Scroller;
        if (sc == null || sc.ScrollableHeight <= 0) return;
        if (e.Delta == 0) return;
        int lines = SystemParameters.WheelScrollLines;
        if (lines < 0)
        {
            if (e.Delta < 0) Rtb.PageDown(); else Rtb.PageUp();
        }
        else
        {
            lines = Math.Max(1, lines);
            if (e.Delta < 0)
            {
                for (int i = 0; i < lines; i++) Rtb.LineDown();
            }
            else
            {
                for (int i = 0; i < lines; i++) Rtb.LineUp();
            }
        }
        e.Handled = true;
    }

    /// <summary>Test hook: raises wheel events through the real handler chain.</summary>
    public void DemoWheel(int notches)
    {
        for (int i = 0; i < notches; i++)
        {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent
            };
            Rtb.RaiseEvent(args);
        }
    }

    public void ScrollBy(double delta)
    {
        var sc = Scroller;
        if (sc == null) return;
        double target = sc.VerticalOffset + delta;
        if (target < 0) target = 0;
        if (target > sc.ScrollableHeight) target = sc.ScrollableHeight;
        Rtb.ScrollToVerticalOffset(target);
    }


    public void ScheduleStyle()
    {
        _styleTimer.Stop();
        _styleTimer.Start();
    }

    public void EnsureStyled()
    {
        if (_caretDirty) StyleNow(false);
    }

    public void StyleNow(bool force)
    {
        if (!IsVisible && !force) { _caretDirty = true; return; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var caret = GetCaretState();
        Styler.ActiveLine = caret.Line;
        _suppress = true;
        try
        {
            Styler.Style(force);
        }
        finally
        {
            _suppress = false;
        }
        RestoreCaret(caret);
        _caretDirty = false;
        // Commit once per typing burst; cheap because it only happens when the style timer fires.
        History.Commit(Text, caret);
        if (sw.ElapsedMilliseconds >= 80) App.Log("perf style " + sw.ElapsedMilliseconds + "ms paras=" + Styler.LastStyledParagraphs + " rebuilt=" + (Styler.LastPassRebuilt ? 1 : 0));
    }

    public void SetText(string text, CaretState? caret, bool resetHistory = true)
    {
        string normalized = TextUtil.Normalize(text ?? "");
        _styleTimer.Stop();
        _suppress = true;
        try
        {
            if (Rtb.Document.Blocks.Count == 0) Rtb.Document.Blocks.Add(new Paragraph());
            new TextRange(Rtb.Document.ContentStart, Rtb.Document.ContentEnd).Text = normalized.Replace("\n", "\r\n");
            Styler.Invalidate();
            Styler.Style(true);
        }
        finally
        {
            _suppress = false;
        }

        InvalidateText();
        if (resetHistory) History.Reset(Text, caret ?? CaretState.At(0, 0));
        if (caret.HasValue) RestoreCaret(caret.Value);
        TextEdited?.Invoke(this, EventArgs.Empty);
        CaretMoved?.Invoke(this, EventArgs.Empty);
        ScrollCaretIntoView();
    }

    /// <summary>Replace text as a discrete undoable step.</summary>
    public void ReplaceAll(string newText, CaretState caret)
    {
        string normalized = TextUtil.Normalize(newText ?? "");
        History.Commit(Text, GetCaretState());
        SetText(normalized, caret, resetHistory: false);
        History.Adopt(normalized, caret);
    }

    public bool Undo()
    {
        if (!History.TryUndo(Text, GetCaretState(), out var snap)) return false;
        SetText(snap.Text, snap.Caret, resetHistory: false);
        return true;
    }

    public bool Redo()
    {
        if (!History.TryRedo(Text, GetCaretState(), out var snap)) return false;
        SetText(snap.Text, snap.Caret, resetHistory: false);
        return true;
    }

    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        MarkdownStyler.SetDocumentDefaults(Rtb, theme, _settings.EditorFont, _settings.EditorFontSize, _settings.EditorWidth);
        Styler.Configure(theme, _settings.EditorFont, _settings.EditorFontSize);
        Styler.Invalidate();
        StyleNow(true);
    }

    public void ApplyLayout()
    {
        Rtb.MaxWidth = _settings.EditorWidth;
        MarkdownStyler.SetDocumentDefaults(Rtb, _theme, _settings.EditorFont, _settings.EditorFontSize, _settings.EditorWidth);
        Styler.Configure(_theme, _settings.EditorFont, _settings.EditorFontSize);
        Styler.Invalidate();
        StyleNow(true);
    }

    // ---------- caret helpers ----------

    public List<Paragraph> Paragraphs()
    {
        return Rtb.Document.Blocks.OfType<Paragraph>().ToList();
    }

    public CaretState GetCaretState()
    {
        var sel = Rtb.Selection;
        var a = GetLineCol(sel.Start);
        var b = GetLineCol(sel.End);
        return new CaretState { Line = a.line, Col = a.col, SelLine = b.line, SelCol = b.col };
    }

    public (int line, int col) GetLineCol(TextPointer pos)
    {
        int line = 0;
        foreach (var b in Rtb.Document.Blocks)
        {
            if (pos.CompareTo(b.ContentEnd) <= 0) break;
            line++;
        }
        int count = Rtb.Document.Blocks.Count;
        if (count == 0) return (0, 0);
        if (line >= count) line = count - 1;
        var para = Rtb.Document.Blocks.ElementAt(line) as Paragraph;
        if (para == null) return (line, 0);
        return (line, GetCol(para, pos));
    }

    public static int GetCol(Paragraph p, TextPointer pos)
    {
        int col = 0;
        foreach (var inline in p.Inlines)
        {
            var run = inline as Run;
            if (run == null)
            {
                continue;
            }
            if (pos.CompareTo(run.ContentStart) < 0) break;
            if (pos.CompareTo(run.ContentEnd) <= 0)
            {
                int off = run.ContentStart.GetOffsetToPosition(pos);
                if (off < 0) off = 0;
                if (off > run.Text.Length) off = run.Text.Length;
                return col + off;
            }
            col += run.Text.Length;
        }
        return col;
    }

    public TextPointer PointerAt(int line, int col)
    {
        var blocks = Rtb.Document.Blocks.ToList();
        if (blocks.Count == 0) return Rtb.Document.ContentStart;
        if (line < 0) line = 0;
        if (line >= blocks.Count) line = blocks.Count - 1;
        var para = blocks[line] as Paragraph;
        if (para == null) return blocks[line].ContentStart;
        if (col < 0) col = 0;
        int acc = 0;
        TextPointer last = null;
        foreach (var inline in para.Inlines)
        {
            var run = inline as Run;
            if (run == null) { last = inline.ContentEnd; continue; }
            int len = run.Text.Length;
            if (col <= acc + len)
            {
                int off = col - acc;
                var pnt = run.ContentStart.GetPositionAtOffset(off);
                return pnt ?? run.ContentStart;
            }
            acc += len;
            last = run.ContentEnd;
        }
        return last ?? para.ContentStart;
    }

    public void RestoreCaret(CaretState s)
    {
        try
        {
            var start = PointerAt(s.Line, s.Col);
            var end = PointerAt(s.SelLine, s.SelCol);
            _suppress = true;
            if (start != null && end != null && start.CompareTo(end) != 0)
                Rtb.Selection.Select(start, end);
            else
                Rtb.CaretPosition = start;
            _suppress = false;
        }
        catch
        {
            _suppress = false;
        }
    }

    public void SelectRange(CaretState start, CaretState end)
    {
        var a = PointerAt(start.Line, start.Col);
        var b = PointerAt(end.Line, end.Col);
        if (a == null || b == null) return;
        _suppress = true;
        Rtb.Selection.Select(a, b);
        _suppress = false;
        Rtb.Focus();
        ScrollCaretIntoView();
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollCaretIntoView()
    {
        
        // RichTextBox has no direct caret scroll API; use internal scroll viewer.
        var sc = Scroller;
        if (sc == null) return;
        var rect = Rtb.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) return;
        double top = sc.VerticalOffset;
        double h = sc.ViewportHeight;
        double y = rect.Top;
        if (y < top + 8) Rtb.ScrollToVerticalOffset(Math.Max(0, y - 24));
        else if (y + rect.Height > top + h - 8) Rtb.ScrollToVerticalOffset(y + rect.Height - h + 40);
    }

    public ScrollViewer Scroller
    {
        get
        {
            if (_scroller == null) HookScroller();
            return _scroller;
        }
    }

    void HookScroller()
    {
        _scroller = FindChild<ScrollViewer>(Rtb);
        if (_scroller != null) _scroller.CanContentScroll = false;
    }

    static T FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int n = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var hit = child as T;
            if (hit != null) return hit;
            var deeper = FindChild<T>(child);
            if (deeper != null) return deeper;
        }
        return null;
    }

    public double VerticalOffset => Scroller != null ? Scroller.VerticalOffset : 0;
    public double ScrollableHeight => Scroller != null ? Scroller.ScrollableHeight : 0;
    public double ViewportHeight => Scroller != null ? Scroller.ViewportHeight : 0;

    public void ScrollToVerticalOffset(double offset)
    {
        if (Scroller != null) Rtb.ScrollToVerticalOffset(offset);
    }

    public string CurrentLineText()
    {
        var st = GetCaretState();
        string text = Text;
        var lines = text.Split('\n');
        if (st.Line >= 0 && st.Line < lines.Length) return lines[st.Line];
        return "";
    }

    public void FocusEditor()
    {
        Rtb.Focus();
    }

    // ---------- editing interactions ----------

    static readonly Regex ListMarkerRegex = new Regex(@"^(\s*)([-*+]|\d{1,9}[.)])([ \t]+)(\[[ xX]\][ \t]+)?", RegexOptions.Compiled);

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            if (shift) OutdentSelection();
            else IndentSelection();
            return;
        }

        if (e.Key == Key.Enter && !ctrl && !shift)
        {
            if (TrySmartEnter()) { e.Handled = true; }
            return;
        }

        if (ctrl && !shift && e.Key == Key.X)
        {
            CutSelection();
            e.Handled = true;
            return;
        }

        if (ctrl && !shift && e.Key == Key.C)
        {
            CopySelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && ctrl && !shift)
        {
            if (HandlePaste()) { e.Handled = true; }
            return;
        }
    }

    public void InsertTextAtCaret(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Rtb.Selection.Text = text;
        Rtb.CaretPosition = Rtb.Selection.End;
    }

    void IndentSelection()
    {
        var sel = Rtb.Selection;
        var a = GetLineCol(sel.Start);
        var b = GetLineCol(sel.End);
        string text = Text;
        var lines = text.Split('\n');
        int first = a.line;
        int last = b.line;
        if (last > first && b.col == 0) last--;
        bool multi = sel.Start.CompareTo(sel.End) != 0 && last > first;
        if (!multi && sel.Start.CompareTo(sel.End) == 0)
        {
            InsertTextAtCaret("  ");
            return;
        }
        for (int i = first; i <= last && i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            lines[i] = "  " + lines[i];
        }
        var caret = new CaretState { Line = first, Col = a.col + 2, SelLine = last, SelCol = b.col + 2 };
        ReplaceAll(string.Join("\n", lines), caret);
    }

    void OutdentSelection()
    {
        var sel = Rtb.Selection;
        var a = GetLineCol(sel.Start);
        var b = GetLineCol(sel.End);
        var lines = Text.Split('\n');
        int first = a.line;
        int last = b.line;
        if (last > first && b.col == 0) last--;
        for (int i = first; i <= last && i < lines.Length; i++)
        {
            if (lines[i].StartsWith("  ", StringComparison.Ordinal)) lines[i] = lines[i].Substring(2);
            else if (lines[i].StartsWith(" ", StringComparison.Ordinal)) lines[i] = lines[i].Substring(1);
        }
        var caret = new CaretState
        {
            Line = first,
            Col = Math.Max(0, a.col - 2),
            SelLine = last,
            SelCol = Math.Max(0, b.col - 2)
        };
        ReplaceAll(string.Join("\n", lines), caret);
    }

    bool TrySmartEnter()
    {
        var sel = Rtb.Selection;
        if (sel.Start.CompareTo(sel.End) != 0) return false;
        string line = CurrentLineText();
        var m = ListMarkerRegex.Match(line);
        if (!m.Success) return false;
        string indent = m.Groups[1].Value;
        string marker = m.Groups[2].Value;
        string gap = m.Groups[3].Value;
        string task = m.Groups[4].Success ? m.Groups[4].Value : "";
        int markerEnd = m.Length;
        string rest = line.Substring(markerEnd);
        if (rest.Trim().Length == 0)
        {
            // exit the list: clear this line
            var st = GetCaretState();
            var lines = Text.Split('\n');
            if (st.Line < lines.Length)
            {
                lines[st.Line] = "";
                ReplaceAll(string.Join("\n", lines), CaretState.At(st.Line, 0));
            }
            return true;
        }

        string newMarker = marker;
        if (marker.Length > 0 && char.IsDigit(marker[0]))
        {
            int dot = marker.IndexOfAny(new[] { '.', ')' });
            if (dot > 0 && int.TryParse(marker.Substring(0, dot), out int num))
                newMarker = (num + 1) + marker.Substring(dot);
        }
        string taskPrefix = task.Length > 0 ? "[ ] " : "";
        string insert = "\n" + indent + newMarker + gap + taskPrefix;
        var caretPos = Rtb.CaretPosition;
        Rtb.Selection.Text = insert;
        Rtb.CaretPosition = Rtb.Selection.End;
        return true;
    }

    /// <summary>
    /// Clipboard writes are done on a throwaway STA thread: OLE clipboard calls can block
    /// for seconds when a clipboard monitor (WeChat, QQ, …) holds the clipboard, and we
    /// must never freeze the UI thread or lose a cut because of that.
    /// </summary>
    static void CopyTextAsync(string text)
    {
        var thread = new System.Threading.Thread(() =>
        {
            try { Clipboard.SetDataObject(text, true); }
            catch { }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    void CopySelection()
    {
        if (Rtb.Selection.IsEmpty) return;
        string text = Rtb.Selection.Text;
        if (!string.IsNullOrEmpty(text)) CopyTextAsync(text);
    }

    void CutSelection()
    {
        if (Rtb.Selection.IsEmpty) return;
        string text = Rtb.Selection.Text;
        if (string.IsNullOrEmpty(text)) return;
        CopyTextAsync(text);
        // Remove immediately; the optional undo step keeps the text recoverable.
        Rtb.Selection.Text = "";
        Rtb.CaretPosition = Rtb.Selection.End;
    }

    bool HandlePaste()
    {
        try
        {
            if (Clipboard.ContainsImage() && PasteImageHandler != null)
            {
                if (PasteImageHandler()) return true;
            }
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                var list = new List<string>();
                foreach (string f in files) if (f != null) list.Add(f);
                if (list.Count > 0)
                {
                    var image = list.FirstOrDefault(TextUtil.IsImageFile);
                    if (image != null && PasteImageHandler != null)
                    {
                        if (PasteImageHandler()) return true;
                    }
                    var text = string.Join("\n", list);
                    Rtb.Selection.Text = text;
                    Rtb.CaretPosition = Rtb.Selection.End;
                    return true;
                }
            }
            if (Clipboard.ContainsText())
            {
                Rtb.Selection.Text = TextUtil.Normalize(Clipboard.GetText()).Replace("\n", "\r\n");
                Rtb.CaretPosition = Rtb.Selection.End;
                return true;
            }
        }
        catch { }
        return false;
    }

    void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;
        FilesDropped?.Invoke(files);
        e.Handled = true;
    }
}

