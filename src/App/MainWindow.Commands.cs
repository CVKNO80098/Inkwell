using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Inkwell.App;

public sealed partial class MainWindow
{
    // ------------------------------------------------------------------ menus

    MenuEntry[] BuildMenus()
    {
        var recent = new List<MenuEntry>();
        foreach (var path in _settings.Recent ?? new List<string>())
        {
            if (!System.IO.File.Exists(path)) continue;
            string captured = path;
            recent.Add(MenuEntry.Item(System.IO.Path.GetFileName(path), "", () => OpenFile(captured)));
            if (recent.Count >= 10) break;
        }
        if (recent.Count == 0) recent.Add(new MenuEntry { Text = "（暂无最近文件）", Enabled = false });

        var format = new List<MenuEntry>
        {
            MenuEntry.Item("加粗", "Ctrl+B", () => WrapInline("**", "**")),
            MenuEntry.Item("斜体", "Ctrl+I", () => WrapInline("*", "*")),
            MenuEntry.Item("删除线", "Ctrl+Shift+X", () => WrapInline("~~", "~~")),
            MenuEntry.Item("行内代码", "Ctrl+E", () => WrapInline("`", "`")),
            MenuEntry.Item("高亮", "", () => WrapInline("==", "==")),
            MenuEntry.Sep(),
            MenuEntry.Sub("标题",
                MenuEntry.Item("一级标题", "Ctrl+1", () => ApplyHeading(1)),
                MenuEntry.Item("二级标题", "Ctrl+2", () => ApplyHeading(2)),
                MenuEntry.Item("三级标题", "Ctrl+3", () => ApplyHeading(3)),
                MenuEntry.Item("四级标题", "Ctrl+4", () => ApplyHeading(4)),
                MenuEntry.Item("五级标题", "Ctrl+5", () => ApplyHeading(5)),
                MenuEntry.Item("六级标题", "Ctrl+6", () => ApplyHeading(6)),
                MenuEntry.Sep(),
                MenuEntry.Item("正文", "Ctrl+0", () => ApplyHeading(0))),
            MenuEntry.Item("引用", "Ctrl+Shift+Q", () => TogglePrefix(new[] { "> " })),
            MenuEntry.Item("无序列表", "Ctrl+Shift+U", () => TogglePrefix(new[] { "- ", "* ", "+ " })),
            MenuEntry.Item("有序列表", "Ctrl+Shift+O", () => TogglePrefix(new[] { "1. " })),
            MenuEntry.Item("任务列表", "Ctrl+Shift+T", () => TogglePrefix(new[] { "- [ ] ", "- [x] " })),
            MenuEntry.Sep(),
            MenuEntry.Item("代码块", "Ctrl+Shift+K", InsertCodeBlock),
            MenuEntry.Item("表格", "Ctrl+Shift+M", InsertTable),
            MenuEntry.Item("分隔线", "", InsertHr),
            MenuEntry.Item("链接", "Ctrl+K", InsertLink),
            MenuEntry.Item("图片", "", InsertImage)
        };

        var view = new List<MenuEntry>
        {
            MenuEntry.Check("分栏视图", _settings.ViewMode == "Split", () => SetViewMode("Split"), "Ctrl+Alt+2"),
            MenuEntry.Check("仅编辑", _settings.ViewMode == "Editor", () => SetViewMode("Editor"), "Ctrl+Alt+1"),
            MenuEntry.Check("仅预览", _settings.ViewMode == "Preview", () => SetViewMode("Preview"), "Ctrl+Alt+3"),
            MenuEntry.Check("单页模式", _settings.ViewMode == "Single", () => SetViewMode("Single"), "Ctrl+Alt+4"),
            MenuEntry.Sep(),
            MenuEntry.Check("侧边栏", _sidebarCol.Width.Value > 20, () => ToggleSidebar(), "Ctrl+Shift+B"),
            MenuEntry.Check("工具栏", _settings.ShowToolbar, ToggleToolbar),
            MenuEntry.Sep(),
            MenuEntry.Check("专注模式", _focusMode, ToggleFocusMode, "F11"),
            MenuEntry.Check("打字机模式", _settings.Typewriter, ToggleTypewriter, "Ctrl+Shift+Y"),
            MenuEntry.Sep(),
            MenuEntry.Sub("主题",
                Theme.Order.Select(id => MenuEntry.Check(Theme.ById(id).Title, _theme.Id == id, () => SetTheme(id), "")).ToArray()),
            MenuEntry.Sep(),
            MenuEntry.Item("放大字号", "Ctrl+=", () => ZoomFont(1)),
            MenuEntry.Item("缩小字号", "Ctrl+-", () => ZoomFont(-1)),
            MenuEntry.Item("重置字号", "Ctrl+Shift+0", ResetFont)
        };

        var edit = new List<MenuEntry>
        {
            MenuEntry.Item("撤销", "Ctrl+Z", () => { ActiveEditor?.Undo(); }),
            MenuEntry.Item("重做", "Ctrl+Y", () => { ActiveEditor?.Redo(); }),
            MenuEntry.Sep(),
            MenuEntry.Item("剪切", "Ctrl+X", () => { ActiveEditor?.Rtb.Cut(); }),
            MenuEntry.Item("复制", "Ctrl+C", () => { ActiveEditor?.Rtb.Copy(); }),
            MenuEntry.Item("粘贴", "Ctrl+V", () => { ActiveEditor?.Rtb.Paste(); }),
            MenuEntry.Sep(),
            MenuEntry.Item("全选", "Ctrl+A", () => { ActiveEditor?.Rtb.SelectAll(); }),
            MenuEntry.Sep(),
            MenuEntry.Item("查找", "Ctrl+F", () => ShowFind(false)),
            MenuEntry.Item("替换", "Ctrl+H", () => ShowFind(true))
        };

        var fileOps = new List<MenuEntry>
        {
            MenuEntry.Sub("最近打开", recent.ToArray()),
            MenuEntry.Sep(),
            MenuEntry.Item("导出为 HTML…", "Ctrl+Shift+E", ExportHtml),
            MenuEntry.Item("导出 PDF / 打印…", "Ctrl+P", PrintDocument),
            MenuEntry.Sep(),
            MenuEntry.Item("关闭当前标签", "Ctrl+W", CloseActiveTab)
        };

        return new[]
        {
            MenuEntry.Item("新建文档", "Ctrl+N", NewDocument),
            MenuEntry.Item("打开文件…", "Ctrl+O", OpenFileDialog),
            MenuEntry.Item("打开文件夹…", "Ctrl+Alt+O", PickFolder),
            MenuEntry.Sep(),
            MenuEntry.Item("保存", "Ctrl+S", () => SaveActive(false)),
            MenuEntry.Item("另存为…", "Ctrl+Shift+S", () => SaveActive(true)),
            MenuEntry.Sep(),
            MenuEntry.Sub("编辑", edit.ToArray()),
            MenuEntry.Sub("格式", format.ToArray()),
            MenuEntry.Sub("视图", view.ToArray()),
            MenuEntry.Sub("文件", fileOps.ToArray()),
            MenuEntry.Sep(),
            MenuEntry.Item("快捷键", "", () => Dialogs.ShowShortcuts(this, _theme)),
            MenuEntry.Item("关于 Inkwell", "", () => Dialogs.ShowAbout(this, _theme))
        };
    }

    // ------------------------------------------------------------------ keyboard

    void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        if (Keyboard.FocusedElement is TextBox && ctrl &&
            (e.Key == Key.Z || e.Key == Key.Y || e.Key == Key.A || e.Key == Key.C || e.Key == Key.V || e.Key == Key.X))
            return;

        if (e.Key == Key.Escape)
        {
            if (_findBar.Visibility == Visibility.Visible) { HideFind(); e.Handled = true; return; }
            if (_focusMode) { ToggleFocusMode(); e.Handled = true; return; }
            if (Keyboard.FocusedElement is FrameworkElement fe && fe.Name == "FindBox") { HideFind(); }
            return;
        }

        if (e.Key == Key.F11 && !ctrl && !alt)
        {
            ToggleFocusMode();
            e.Handled = true;
            return;
        }

        if (ctrl && alt)
        {
            if (e.Key == Key.D1) { SetViewMode("Editor"); e.Handled = true; }
            else if (e.Key == Key.D2) { SetViewMode("Split"); e.Handled = true; }
            else if (e.Key == Key.D3) { SetViewMode("Preview"); e.Handled = true; }
            else if (e.Key == Key.D4) { SetViewMode("Single"); e.Handled = true; }
            else if (e.Key == Key.O) { PickFolder(); e.Handled = true; }
            return;
        }

        if (ctrl && shift)
        {
            switch (e.Key)
            {
                case Key.S: SaveActive(true); e.Handled = true; break;
                case Key.E: ExportHtml(); e.Handled = true; break;
                case Key.B: ToggleSidebar(); e.Handled = true; break;
                case Key.Q: TogglePrefix(new[] { "> " }); e.Handled = true; break;
                case Key.U: TogglePrefix(new[] { "- ", "* ", "+ " }); e.Handled = true; break;
                case Key.O: TogglePrefix(new[] { "1. " }); e.Handled = true; break;
                case Key.T: TogglePrefix(new[] { "- [ ] ", "- [x] " }); e.Handled = true; break;
                case Key.K: InsertCodeBlock(); e.Handled = true; break;
                case Key.M: InsertTable(); e.Handled = true; break;
                case Key.X: WrapInline("~~", "~~"); e.Handled = true; break;
                case Key.Y: ToggleTypewriter(); e.Handled = true; break;
                case Key.D0: ResetFont(); e.Handled = true; break;
                case Key.Tab: NextTab(-1); e.Handled = true; break;
            }
            return;
        }

        if (ctrl && !alt)
        {
            switch (e.Key)
            {
                case Key.N: NewDocument(); e.Handled = true; break;
                case Key.O: OpenFileDialog(); e.Handled = true; break;
                case Key.S: SaveActive(false); e.Handled = true; break;
                case Key.W: CloseActiveTab(); e.Handled = true; break;
                case Key.Z: if (ActiveEditor != null) { ActiveEditor.Undo(); e.Handled = true; } break;
                case Key.Y: if (ActiveEditor != null) { ActiveEditor.Redo(); e.Handled = true; } break;
                case Key.F: ShowFind(false); e.Handled = true; break;
                case Key.H: ShowFind(true); e.Handled = true; break;
                case Key.B: WrapInline("**", "**"); e.Handled = true; break;
                case Key.I: WrapInline("*", "*"); e.Handled = true; break;
                case Key.E: WrapInline("`", "`"); e.Handled = true; break;
                case Key.K: InsertLink(); e.Handled = true; break;
                case Key.P: PrintDocument(); e.Handled = true; break;
                case Key.Tab: NextTab(1); e.Handled = true; break;
                case Key.D0: ApplyHeading(0); e.Handled = true; break;
                case Key.D1: ApplyHeading(1); e.Handled = true; break;
                case Key.D2: ApplyHeading(2); e.Handled = true; break;
                case Key.D3: ApplyHeading(3); e.Handled = true; break;
                case Key.D4: ApplyHeading(4); e.Handled = true; break;
                case Key.D5: ApplyHeading(5); e.Handled = true; break;
                case Key.D6: ApplyHeading(6); e.Handled = true; break;
                case Key.OemPlus:
                case Key.Add: ZoomFont(1); e.Handled = true; break;
                case Key.OemMinus:
                case Key.Subtract: ZoomFont(-1); e.Handled = true; break;
            }
        }
    }

    bool _focusMode;
    bool _focusWasSidebar;
    bool _focusWasToolbar;

    void ToggleFocusMode()
    {
        _focusMode = !_focusMode;
        if (_focusMode)
        {
            _focusWasSidebar = _sidebarCol.Width.Value > 20;
            _focusWasToolbar = _settings.ShowToolbar;
            ApplySidebarVisibility(false);
            _mainGrid.RowDefinitions[0].Height = new GridLength(0);
            _root.RowDefinitions[0].Height = new GridLength(0);
            _root.RowDefinitions[1].Height = new GridLength(0);
            _root.RowDefinitions[3].Height = new GridLength(0);
        }
        else
        {
            ApplySidebarVisibility(_focusWasSidebar);
            _mainGrid.RowDefinitions[0].Height = new GridLength(_settings.ShowToolbar ? 38 : 0);
            _root.RowDefinitions[0].Height = new GridLength(42);
            _root.RowDefinitions[1].Height = new GridLength(36);
            _root.RowDefinitions[3].Height = new GridLength(26);
        }
        ActiveEditor?.FocusEditor();
    }

    void ToggleTypewriter()
    {
        _settings.Typewriter = !_settings.Typewriter;
        ScheduleSettingsSave();
    }

    void ZoomFont(int delta)
    {
        double size = Math.Max(12, Math.Min(30, _settings.EditorFontSize + delta));
        _settings.EditorFontSize = size;
        _settings.PreviewFontSize = size;
        foreach (var tab in _tabs) tab.Editor.ApplyLayout();
        if (_active >= 0) RenderPreview();
        ScheduleSettingsSave();
    }

    void ResetFont()
    {
        _settings.EditorFontSize = 16.5;
        _settings.PreviewFontSize = 16.5;
        foreach (var tab in _tabs) tab.Editor.ApplyLayout();
        if (_active >= 0) RenderPreview();
        ScheduleSettingsSave();
    }

    // ------------------------------------------------------------------ markdown commands

    static int OffsetFrom(string text, int line, int col)
    {
        int offset = 0;
        for (int l = 0; l < line; l++)
        {
            int nl = text.IndexOf('\n', offset);
            if (nl < 0) return text.Length;
            offset = nl + 1;
        }
        return Math.Min(text.Length, offset + Math.Max(0, col));
    }

    static CaretState StateFrom(string text, int offset)
    {
        offset = Math.Max(0, Math.Min(text.Length, offset));
        int line = 0, lineStart = 0;
        for (int i = 0; i < offset; i++)
        {
            if (text[i] == '\n') { line++; lineStart = i + 1; }
        }
        return CaretState.At(line, offset - lineStart);
    }

    static CaretState SelectionState(string text, int start, int end)
    {
        var a = StateFrom(text, start);
        var b = StateFrom(text, end);
        a.SelLine = b.Line;
        a.SelCol = b.Col;
        return a;
    }

    void ReplaceEditorText(string newText, CaretState caret)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        ed.ReplaceAll(newText, caret);
        ed.FocusEditor();
    }

    void WrapInline(string prefix, string suffix)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int a = OffsetFrom(text, st.Line, st.Col);
        int b = OffsetFrom(text, st.SelLine, st.SelCol);
        int start = Math.Min(a, b), end = Math.Max(a, b);

        bool unwrap = start >= prefix.Length && end + suffix.Length <= text.Length
            && string.CompareOrdinal(text, start - prefix.Length, prefix, 0, prefix.Length) == 0
            && string.CompareOrdinal(text, end, suffix, 0, suffix.Length) == 0;

        if (unwrap)
        {
            string inner = text.Substring(start, end - start);
            string newText = text.Remove(end, suffix.Length).Remove(start - prefix.Length, prefix.Length);
            ReplaceEditorText(newText, CaretState.At(0, 0));
            var s2 = StateFrom(newText, start - prefix.Length);
            var e2 = StateFrom(newText, start - prefix.Length + inner.Length);
            ed.SelectRange(s2, e2);
            return;
        }

        if (end == start)
        {
            string newText = text.Insert(start, prefix + suffix);
            ReplaceEditorText(newText, StateFrom(newText, start + prefix.Length));
        }
        else
        {
            string sel = text.Substring(start, end - start);
            string newText = text.Substring(0, start) + prefix + sel + suffix + text.Substring(end);
            ReplaceEditorText(newText, CaretState.At(0, 0));
            var s2 = StateFrom(newText, start + prefix.Length);
            var e2 = StateFrom(newText, start + prefix.Length + sel.Length);
            ed.SelectRange(s2, e2);
        }
    }

    void ApplyHeading(int level)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int first = Math.Min(st.Line, st.SelLine);
        int last = Math.Max(st.Line, st.SelLine);
        if (last > first && st.Col == 0 && st.SelCol == 0) last--;
        string[] lines = TextUtil.SplitLines(text);
        if (first >= lines.Length) return;
        last = Math.Min(last, lines.Length - 1);
        string newText;
        if (level == 0)
        {
            for (int i = first; i <= last; i++)
            {
                string line = lines[i];
                int h = 0;
                while (h < line.Length && line[h] == '#') h++;
                if (h >= 1 && h <= 6 && h < line.Length && line[h] == ' ') lines[i] = line.Substring(h + 1);
            }
            newText = string.Join("\n", lines);
        }
        else
        {
            newText = TextUtil.ToggleHeading(text, first, last, level);
        }
        ReplaceEditorText(newText, CaretState.At(first, 0));
    }

    void TogglePrefix(string[] prefixes)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int first = Math.Min(st.Line, st.SelLine);
        int last = Math.Max(st.Line, st.SelLine);
        if (last > first && st.SelCol == 0) last--;
        string newText = TextUtil.ToggleLinePrefix(text, first, last, prefixes);
        ReplaceEditorText(newText, CaretState.At(first, 0));
    }

    void InsertCodeBlock()
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int a = OffsetFrom(text, st.Line, st.Col);
        int b = OffsetFrom(text, st.SelLine, st.SelCol);
        int start = Math.Min(a, b), end = Math.Max(a, b);
        string sel = text.Substring(start, end - start);
        string header = (start == 0 || text[start - 1] == '\n') ? "" : "\n\n";
        string body = sel.Length > 0 ? sel + (sel.EndsWith("\n") ? "" : "\n") : "";
        string block = header + "```\n" + body + "```";
        string newText = text.Substring(0, start) + block + text.Substring(end);
        int caret = start + header.Length + 4 + body.Length;
        ReplaceEditorText(newText, StateFrom(newText, caret));
    }

    void InsertTable()
    {
        string template = "| 列 1 | 列 2 | 列 3 |\n| --- | --- | --- |\n|  |  |  |\n|  |  |  |\n";
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int pos = OffsetFrom(text, st.Line, st.Col);
        string prefix = (pos == 0 || text[pos - 1] == '\n') ? "" : "\n\n";
        string insert = prefix + template;
        string newText = text.Insert(pos, insert);
        ReplaceEditorText(newText, StateFrom(newText, pos + insert.Length));
    }

    void InsertHr()
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int pos = OffsetFrom(text, st.Line, st.Col);
        string insert = (pos == 0 || text[pos - 1] == '\n') ? "---\n" : "\n\n---\n\n";
        string newText = text.Insert(pos, insert);
        ReplaceEditorText(newText, StateFrom(newText, pos + insert.Length));
    }

    void InsertLink()
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int a = OffsetFrom(text, st.Line, st.Col);
        int b = OffsetFrom(text, st.SelLine, st.SelCol);
        int start = Math.Min(a, b), end = Math.Max(a, b);
        string sel = text.Substring(start, end - start);
        string insert = "[" + sel + "](url)";
        string newText = text.Substring(0, start) + insert + text.Substring(end);
        int urlStart = start + 1 + sel.Length + 2;
        ReplaceEditorText(newText, CaretState.At(0, 0));
        var s2 = StateFrom(newText, urlStart);
        var e2 = StateFrom(newText, urlStart + 3);
        ed.SelectRange(s2, e2);
        ed.FocusEditor();
    }

    void InsertImage()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        var dlg = new OpenFileDialog
        {
            Title = "插入图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tif;*.tiff|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) == true) InsertImageFromFile(tab, dlg.FileName);
    }

    void ToggleFindCase()
    {
        _matchCase = !_matchCase;
        if (_findCaseButton != null)
        {
            _findCaseButton.Foreground = _matchCase ? _theme.AccentBrush : _theme.MutedBrush;
        }
        UpdateFindMatches(false);
    }

    // find bar is declared/built in MainWindow.Find.cs
}
