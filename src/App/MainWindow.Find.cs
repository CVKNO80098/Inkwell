using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Inkwell.App;

public sealed partial class MainWindow
{
    struct FindMatch
    {
        public int Start;
        public int Length;
        public FindMatch(int start, int length) { Start = start; Length = length; }
    }

    Border _findBar;
    TextBox _findBox;
    TextBox _replaceBox;
    TextBlock _findStatus;
    StackPanel _replaceRow;
    Button _findCaseButton;
    bool _matchCase;
    List<FindMatch> _matches;
    int _matchIndex = -1;

    Border BuildFindBar()
    {
        var bar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 14, 30, 0),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Effect = Ui.Shadow(_theme, 18, 4, 0.22)
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        bar.SetResourceReference(Border.BorderBrushProperty, "Ink.Border");

        var stack = new StackPanel();

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _findBox = new TextBox
        {
            Width = 200,
            Height = 28,
            FontSize = 12.5,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _findBox.TextChanged += (s, e) => UpdateFindMatches(false);
        _findBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                FindNext(shift ? -1 : 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideFind();
                e.Handled = true;
            }
        };
        row.Children.Add(_findBox);

        _findStatus = new TextBlock
        {
            Width = 62,
            FontSize = 11,
            Margin = new Thickness(8, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _findStatus.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
        row.Children.Add(_findStatus);

        var prev = Ui.IconButton("↑", "上一个 (Shift+Enter)", () => FindNext(-1), 26, 12);
        prev.Height = 26;
        row.Children.Add(prev);
        var next = Ui.IconButton("↓", "下一个 (Enter)", () => FindNext(1), 26, 12);
        next.Height = 26;
        row.Children.Add(next);

        _findCaseButton = new Button
        {
            Content = new TextBlock { Text = "Aa", FontSize = 11.5 },
            Style = (Style)Application.Current.Resources["FlatButton"],
            Width = 30,
            Height = 26,
            Padding = new Thickness(0),
            ToolTip = "区分大小写"
        };
        _findCaseButton.Click += (s, e) => ToggleFindCase();
        row.Children.Add(_findCaseButton);

        var close = Ui.IconButton("✕", "关闭 (Esc)", () => HideFind(), 26, 11);
        close.Height = 26;
        close.Margin = new Thickness(4, 0, 0, 0);
        row.Children.Add(close);
        stack.Children.Add(row);

        _replaceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        _replaceBox = new TextBox
        {
            Width = 200,
            Height = 28,
            FontSize = 12.5,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _replaceBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { ReplaceCurrent(); e.Handled = true; }
        };
        _replaceRow.Children.Add(_replaceBox);
        var replaceOne = Ui.FlatButton("替换", "替换当前匹配", ReplaceCurrent, 12);
        replaceOne.Height = 26;
        replaceOne.Margin = new Thickness(8, 0, 0, 0);
        _replaceRow.Children.Add(replaceOne);
        var replaceAll = Ui.FlatButton("全部", "替换全部匹配", ReplaceAllMatches, 12);
        replaceAll.Height = 26;
        replaceAll.Margin = new Thickness(4, 0, 0, 0);
        _replaceRow.Children.Add(replaceAll);
        stack.Children.Add(_replaceRow);

        bar.Child = stack;
        return bar;
    }

    public void DemoFind() { ShowFind(false); }
    void ShowFind(bool replace)
    {
        var tab = ActiveTab;
        if (tab == null) return;
        var st = tab.Editor.GetCaretState();
        if (st.Line != st.SelLine || st.Col != st.SelCol)
        {
            string text = tab.Editor.Text;
            int a = OffsetFrom(text, st.Line, st.Col);
            int b = OffsetFrom(text, st.SelLine, st.SelCol);
            int start = Math.Min(a, b), end = Math.Max(a, b);
            string sel = text.Substring(start, end - start);
            if (sel.Length > 0 && !sel.Contains("\n")) _findBox.Text = sel;
        }
        if (_findBox.Text.Length == 0)
        {
            st = tab.Editor.GetCaretState();
        }
        _replaceRow.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        _findBar.Visibility = Visibility.Visible;
        _findBox.Focus();
        _findBox.SelectAll();
        UpdateFindMatches(true);
        if (replace) _replaceBox.Focus();
    }

    void HideFind()
    {
        _findBar.Visibility = Visibility.Collapsed;
        ActiveEditor?.FocusEditor();
    }

    static List<FindMatch> FindAll(string text, string query, bool matchCase)
    {
        var list = new List<FindMatch>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return list;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int index = 0;
        while (index <= text.Length - query.Length)
        {
            int found = text.IndexOf(query, index, comparison);
            if (found < 0) break;
            list.Add(new FindMatch(found, query.Length));
            index = found + Math.Max(1, query.Length);
            if (list.Count > 5000) break;
        }
        return list;
    }

    void UpdateFindMatches(bool selectFirst)
    {
        _matches = null;
        _matchIndex = -1;
        var tab = ActiveTab;
        if (tab == null) return;
        string query = _findBox.Text;
        if (query.Length == 0)
        {
            _findStatus.Text = "";
            return;
        }
        _matches = FindAll(tab.Editor.Text, query, _matchCase);
        if (_matches.Count == 0)
        {
            _findStatus.Text = "无结果";
            _findStatus.Foreground = _theme.AccentBrush;
            return;
        }
        _findStatus.Foreground = _theme.MutedBrush;
        _findStatus.Text = _matches.Count + " 处";
        if (selectFirst) SelectMatch(0);
    }

    void SelectMatch(int index)
    {
        var tab = ActiveTab;
        if (tab == null || _matches == null || index < 0 || index >= _matches.Count) return;
        string text = tab.Editor.Text;
        var m = _matches[index];
        _matchIndex = index;
        var s = StateFrom(text, m.Start);
        var e = StateFrom(text, m.Start + m.Length);
        tab.Editor.SelectRange(s, e);
        tab.Editor.ScrollCaretIntoView();
        _findStatus.Text = (index + 1) + "/" + _matches.Count;
    }

    void FindNext(int direction)
    {
        var tab = ActiveTab;
        if (tab == null) return;
        if (_findBox.Text.Length == 0) return;
        if (_matches == null || _matches.Count == 0)
        {
            _matches = FindAll(tab.Editor.Text, _findBox.Text, _matchCase);
            if (_matches.Count == 0) { _findStatus.Text = "无结果"; return; }
        }
        string text = tab.Editor.Text;
        var st = tab.Editor.GetCaretState();
        bool hasSel = st.Line != st.SelLine || st.Col != st.SelCol;
        int caret = OffsetFrom(text, hasSel ? st.SelLine : st.Line, hasSel ? st.SelCol : st.Col);

        int index = -1;
        if (direction > 0)
        {
            for (int i = 0; i < _matches.Count; i++)
            {
                if (_matches[i].Start >= caret) { index = i; break; }
            }
            if (index < 0) index = 0;
        }
        else
        {
            for (int i = _matches.Count - 1; i >= 0; i--)
            {
                if (_matches[i].Start < caret) { index = i; break; }
            }
            if (index < 0) index = _matches.Count - 1;
        }
        SelectMatch(index);
    }

    void ReplaceCurrent()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        string query = _findBox.Text;
        if (query.Length == 0) return;
        string text = tab.Editor.Text;
        var st = tab.Editor.GetCaretState();
        int a = OffsetFrom(text, st.Line, st.Col);
        int b = OffsetFrom(text, st.SelLine, st.SelCol);
        int start = Math.Min(a, b), end = Math.Max(a, b);
        var comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        bool selected = end - start == query.Length && string.Compare(text, start, query, 0, query.Length, comparison) == 0;
        if (!selected)
        {
            FindNext(1);
            return;
        }
        string replacement = _replaceBox.Text ?? "";
        string newText = text.Substring(0, start) + replacement + text.Substring(end);
        tab.Editor.ReplaceAll(newText, StateFrom(newText, start + replacement.Length));
        _matches = null;
        UpdateFindMatches(false);
        FindNext(1);
    }

    void ReplaceAllMatches()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        string query = _findBox.Text;
        if (query.Length == 0) return;
        string text = tab.Editor.Text;
        string replacement = _replaceBox.Text ?? "";
        var comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = FindAll(text, query, _matchCase);
        if (matches.Count == 0) { _findStatus.Text = "无结果"; return; }
        var sb = new StringBuilder();
        int last = 0;
        foreach (var m in matches)
        {
            sb.Append(text, last, m.Start - last);
            sb.Append(replacement);
            last = m.Start + m.Length;
        }
        sb.Append(text, last, text.Length - last);
        tab.Editor.ReplaceAll(sb.ToString(), CaretState.At(0, 0));
        _matches = null;
        UpdateFindMatches(false);
        _findStatus.Text = "已替换 " + matches.Count + " 处";
    }
}
