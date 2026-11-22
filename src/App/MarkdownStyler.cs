using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Inkwell.App;

/// <summary>Applies Markdown syntax decoration and block layout to the RichTextBox document.</summary>
public sealed class MarkdownStyler
{
    RichTextBox _rtb;
    Theme _theme = Theme.Paper();
    string _fontName = "Segoe UI";
    double _fontSize = 16.5;
    double _editorWidth = 760;

    /// <summary>Single-page (Typora-like) mode: hide the Markdown punctuation except on the caret line.</summary>
    public bool SinglePage;
    public int ActiveLine = -1;

    readonly List<string> _cacheText = new List<string>();
    readonly List<string> _cacheSig = new List<string>();

    public int LastStyledParagraphs { get; private set; }
    public bool LastPassRebuilt { get; private set; }

    public void Attach(RichTextBox rtb) { _rtb = rtb; }

    public void Configure(Theme theme, string fontName, double fontSize)
    {
        _theme = theme;
        _fontName = string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName;
        _fontSize = fontSize;
        Invalidate();
    }

    public void ConfigureWidth(double width)
    {
        if (Math.Abs(width - _editorWidth) < 0.5) return;
        _editorWidth = width;
        Invalidate();
    }

    public void Invalidate()
    {
        _cacheText.Clear();
        _cacheSig.Clear();
    }

    public static string GetText(RichTextBox rtb)
    {
        string t = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text;
        t = TextUtil.Normalize(t);
        if (t.EndsWith("\n", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
        return t;
    }

    void EnsureParagraphs()
    {
        var doc = _rtb.Document;
        var blocks = doc.Blocks.ToList();
        bool rebuild = false;
        foreach (var b in blocks)
        {
            if (!(b is Paragraph)) { rebuild = true; break; }
        }
        if (!rebuild) return;
        string text = GetText(_rtb);
        doc.Blocks.Clear();
        new TextRange(doc.ContentStart, doc.ContentEnd).Text = text.Replace("\n", "\r\n");
    }

    public void Style(bool force)
    {
        if (_rtb == null) return;
        EnsureParagraphs();
        string text = GetText(_rtb);
        string[] lines = text.Length == 0 ? new[] { "" } : text.Split('\n');

        var doc = _rtb.Document;
        var paragraphs = doc.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count != lines.Length)
        {
            // Keep model and view in sync no matter what.
            string joined = string.Join("\n", lines);
            doc.Blocks.Clear();
            new TextRange(doc.ContentStart, doc.ContentEnd).Text = joined.Replace("\n", "\r\n");
            paragraphs = doc.Blocks.OfType<Paragraph>().ToList();
            if (paragraphs.Count == 0)
            {
                var p0 = new Paragraph();
                doc.Blocks.Add(p0);
                paragraphs.Add(p0);
            }
            force = true;
        }

        if (force) { _cacheText.Clear(); _cacheSig.Clear(); }

        var infos = MdLineAnalyzer.Analyze(lines);
        int rebuilt = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string line = i < lines.Length ? lines[i] : "";
            var info = i < infos.Length ? infos[i] : new MdLineInfo { ContentEnd = line.Length };
            string sig = info.Sig() + "|" + _theme.Id + "|" + _fontName + "|" + _fontSize.ToString("0.##") + "|" + _editorWidth.ToString("0.##")
                + "|" + (SinglePage ? (i == ActiveLine ? "a" : "h") : "n");
            bool hit = false;
            if (!force && i < _cacheText.Count && i < _cacheSig.Count)
            {
                if (string.Equals(_cacheText[i], line, StringComparison.Ordinal) && _cacheSig[i] == sig) hit = true;
            }
            if (!hit)
            {
                StyleParagraph(paragraphs[i], line, info, SinglePage && i != ActiveLine);
                if (i < _cacheText.Count) { _cacheText[i] = line; _cacheSig[i] = sig; }
                else { _cacheText.Add(line); _cacheSig.Add(sig); }
                rebuilt++;
            }
        }

        if (_cacheText.Count > paragraphs.Count) _cacheText.RemoveRange(paragraphs.Count, _cacheText.Count - paragraphs.Count);
        if (_cacheSig.Count > paragraphs.Count) _cacheSig.RemoveRange(paragraphs.Count, _cacheSig.Count - paragraphs.Count);

        LastStyledParagraphs = paragraphs.Count;
        LastPassRebuilt = rebuilt > 0;
    }

    double HeadingSize(int level)
    {
        double f = _fontSize;
        switch (level)
        {
            case 1: return Math.Round(f * 1.9);
            case 2: return Math.Round(f * 1.55);
            case 3: return Math.Round(f * 1.3);
            case 4: return Math.Round(f * 1.16);
            case 5: return Math.Round(f * 1.05);
            default: return f;
        }
    }

    public static bool IsCodeKind(MdLineKind kind)
    {
        return kind == MdLineKind.Code || kind == MdLineKind.CodeFence || kind == MdLineKind.FrontMatter;
    }

    void StyleParagraph(Paragraph p, string line, MdLineInfo info, bool hideMarkers)
    {
        var theme = _theme;
        var font = Fonts.Resolve(_fontName);
        var mono = Fonts.Mono;
        double baseSize = _fontSize;

        p.FontFamily = font;
        p.FontSize = baseSize;
        p.FontWeight = FontWeights.Normal;
        p.FontStyle = FontStyles.Normal;
        p.TextDecorations = null;
        p.Margin = new Thickness(0);
        p.Padding = new Thickness(0);
        p.TextIndent = 0;
        p.BorderThickness = new Thickness(0);
        p.Background = Brushes.Transparent;
        p.Foreground = theme.FgBrush;
        p.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        p.LineHeight = Math.Round(baseSize * 1.62);
        p.TextAlignment = TextAlignment.Left;

        double size = baseSize;
        switch (info.Kind)
        {
            case MdLineKind.Heading:
                size = HeadingSize(info.HeadingLevel);
                p.FontSize = size;
                p.FontWeight = FontWeights.Bold;
                p.LineHeight = Math.Round(size * 1.34);
                p.Margin = new Thickness(0, info.HeadingLevel <= 2 ? 24 : 16, 0, info.HeadingLevel <= 2 ? 10 : 6);
                break;
            case MdLineKind.Code:
            case MdLineKind.CodeFence:
            case MdLineKind.FrontMatter:
                p.FontFamily = mono;
                p.FontSize = Math.Round(baseSize * 0.93);
                p.Background = theme.CodeBgBrush;
                p.Padding = new Thickness(14, info.CodeStart ? 10 : 1, 12, info.CodeEnd ? 10 : 1);
                p.BorderThickness = new Thickness(3, 0, 0, 0);
                p.BorderBrush = theme.Alpha(theme.AccentBrush, 0.45);
                p.LineHeight = Math.Round(baseSize * 1.5);
                break;
            case MdLineKind.Quote:
                p.BorderThickness = new Thickness(3, 0, 0, 0);
                p.BorderBrush = theme.QuoteBarBrush;
                p.Padding = new Thickness(12, 1, 0, 1);
                p.Foreground = theme.MutedBrush;
                break;
            case MdLineKind.List:
                p.Margin = new Thickness(20 + info.ListIndent * 22, 0, 0, 0);
                p.TextIndent = -20;
                break;
            case MdLineKind.Hr:
                p.BorderThickness = new Thickness(0, 0, 0, 1);
                p.BorderBrush = theme.BorderBrush;
                p.Margin = new Thickness(0, 10, 0, 10);
                p.Foreground = theme.MutedBrush;
                p.FontSize = Math.Round(baseSize * 0.8);
                p.LineHeight = Math.Round(baseSize * 1.2);
                break;
            case MdLineKind.Table:
                p.Padding = new Thickness(10, 3, 10, 3);
                p.LineHeight = Math.Round(baseSize * 1.5);
                p.FontSize = Math.Round(baseSize * 0.95);
                if (info.TableHeader) { p.Background = theme.TableHeaderBrush; p.FontWeight = FontWeights.Bold; }
                break;
            case MdLineKind.SetextUnderline:
                p.Foreground = theme.MutedBrush;
                p.FontSize = Math.Round(baseSize * 0.75);
                p.LineHeight = Math.Round(baseSize * 0.9);
                break;
            default:
                break;
        }

        if (hideMarkers && SinglePage)
        {
            switch (info.Kind)
            {
                case MdLineKind.CodeFence:
                    // Fences exist only in the source; in 单页 they collapse to
                    // nothing so only the formatted code block remains visible.
                    p.FontSize = 0.1;
                    p.LineHeight = 0.2;
                    p.Padding = new Thickness(0);
                    p.Margin = new Thickness(0);
                    p.BorderThickness = new Thickness(0);
                    p.Background = Brushes.Transparent;
                    break;
                case MdLineKind.FrontMatter:
                    p.FontSize = 0.1;
                    p.LineHeight = 0.2;
                    p.Padding = new Thickness(0);
                    p.Background = Brushes.Transparent;
                    p.BorderThickness = new Thickness(0);
                    break;
                case MdLineKind.Hr:
                    p.FontSize = 0.1;
                    p.LineHeight = 2;
                    p.Margin = new Thickness(0, 8, 0, 8);
                    break;
                case MdLineKind.SetextUnderline:
                    p.FontSize = 0.1;
                    p.LineHeight = 0.2;
                    break;
            }
        }

        var flags = new MdStyleFlags[line.Length];
        if (IsCodeKind(info.Kind))
        {
            for (int k = 0; k < flags.Length; k++) flags[k] |= MdStyleFlags.Code;
        }
        foreach (var d in info.Decos)
        {
            int a = Math.Max(0, d.Start);
            int b = Math.Min(flags.Length, d.Start + d.Length);
            for (int k = a; k < b; k++) flags[k] |= d.Flags;
        }
        if (!IsCodeKind(info.Kind))
        {
            if (info.Kind == MdLineKind.Table && info.TableHeader)
            {
                for (int k = 0; k < flags.Length; k++) flags[k] |= MdStyleFlags.Bold;
            }
            int cs = Math.Max(0, Math.Min(info.ContentStart, line.Length));
            int ce = Math.Max(cs, Math.Min(info.ContentEnd, line.Length));
            InlineHighlighter.Scan(line, cs, ce, flags);
        }

        p.Inlines.Clear();
        if (line.Length == 0)
        {
            p.Inlines.Add(new Run(""));
        }
        else if (info.Kind == MdLineKind.Code && !string.IsNullOrEmpty(info.FenceLang))
        {
            foreach (var r in CodeHighlighter.Highlight(line, info.FenceLang, theme)) p.Inlines.Add(r);
        }
        else
        {
            int i = 0;
            while (i < line.Length)
            {
                MdStyleFlags f = flags[i];
                int j = i + 1;
                while (j < line.Length && flags[j] == f) j++;
                var run = new Run(line.Substring(i, j - i));
                StyleRun(run, f, info, theme, font, mono, baseSize, hideMarkers && HideSegment(f, info, i));
                p.Inlines.Add(run);
                i = j;
            }
        }
        // keep paragraph level font settings authoritative
        p.FontSize = size;
        if (info.Kind == MdLineKind.Heading) p.FontWeight = FontWeights.Bold;
    }

    bool HideSegment(MdStyleFlags f, MdLineInfo info, int start)
    {
        if (info.Kind == MdLineKind.Quote)
            return (f & (MdStyleFlags.Dim | MdStyleFlags.Accent)) != 0;
        if ((f & MdStyleFlags.Dim) == 0) return false;
        // Tables stay as styled source in single-page mode for now.
        if (info.Kind == MdLineKind.Table) return false;
        // Keep list bullets/numbering; hide only markers inside the item text.
        if (info.Kind == MdLineKind.List) return start >= info.ContentStart;
        return true;
    }

    void StyleRun(Run run, MdStyleFlags f, MdLineInfo info, Theme theme, FontFamily font, FontFamily mono, double baseSize, bool hidden)
    {
        bool codeBlock = IsCodeKind(info.Kind);
        bool inlineCode = (f & MdStyleFlags.Code) != 0;

        if (inlineCode && !codeBlock)
        {
            run.FontFamily = mono;
            run.FontSize = Math.Round(baseSize * 0.9);
            run.Background = theme.CodeBgBrush;
        }
        else if (codeBlock)
        {
            run.FontFamily = mono;
        }

        if ((f & MdStyleFlags.Bold) != 0) run.FontWeight = FontWeights.Bold;
        if ((f & MdStyleFlags.Italic) != 0) run.FontStyle = FontStyles.Italic;

        var decorations = new TextDecorationCollection();
        if ((f & MdStyleFlags.Strike) != 0) decorations.Add(TextDecorations.Strikethrough[0]);
        if ((f & MdStyleFlags.Link) != 0 || (f & MdStyleFlags.Underline) != 0) decorations.Add(TextDecorations.Underline[0]);
        if (decorations.Count > 0)
        {
            decorations.Freeze();
            run.TextDecorations = decorations;
        }

        if ((f & MdStyleFlags.Mark) != 0) run.Background = theme.AccentSoftBrush;

        Brush fg = theme.FgBrush;
        if ((f & MdStyleFlags.Link) != 0) fg = theme.LinkBrush;
        else if ((f & MdStyleFlags.Accent) != 0) fg = theme.AccentBrush;
        else if ((f & MdStyleFlags.Dim) != 0 || (f & MdStyleFlags.Url) != 0) fg = theme.MutedBrush;
        else if (info.Kind == MdLineKind.Quote && !codeBlock) fg = theme.MutedBrush;
        run.Foreground = fg;

        if (codeBlock && (f & MdStyleFlags.Dim) != 0) run.Foreground = theme.MutedBrush;

        if (hidden)
        {
            // Keep the characters in the document (offsets, undo, find, caret all rely on them)
            // but draw them at an invisible size on lines the caret is not on.
            run.FontSize = 0.1;
            run.Foreground = Brushes.Transparent;
            run.Background = Brushes.Transparent;
            run.TextDecorations = null;
        }
    }

    public static void SetDocumentDefaults(RichTextBox rtb, Theme theme, string fontName, double fontSize, double editorWidth)
    {
        var doc = rtb.Document;
        doc.PagePadding = new Thickness(28, 20, 28, 96);
        doc.ColumnWidth = double.PositiveInfinity;
        doc.Background = Brushes.Transparent;
        doc.FontFamily = Fonts.Resolve(fontName);
        doc.FontSize = fontSize;
        doc.Foreground = theme.FgBrush;
        rtb.Background = Brushes.Transparent;
        rtb.Foreground = theme.FgBrush;
        rtb.CaretBrush = theme.CaretBrush;
        rtb.SelectionBrush = theme.SelectionBrush;
        rtb.SelectionOpacity = 0.55;
        rtb.MaxWidth = editorWidth;
    }
}
