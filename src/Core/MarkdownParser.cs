using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Inkwell.Core;

/// <summary>Line-oriented CommonMark/GFM subset parser (headings, lists, quotes,
/// fenced code, tables, task lists, rules, setext, link references).</summary>
public static class MarkdownParser
{
    public static MdDocument Parse(string text)
    {
        if (text == null) text = "";
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
        var doc = new MdDocument();
        var lines = SplitLines(text);

        // Front matter -> fenced yaml block
        int bodyStart = 0;
        if (lines.Count > 0 && lines[0].TrimEnd() == "---")
        {
            for (int j = 1; j < Math.Min(lines.Count, 60); j++)
            {
                if (lines[j].TrimEnd() == "---" || lines[j].TrimEnd() == "...")
                {
                    var yaml = new StringBuilder();
                    for (int k = 1; k < j; k++) yaml.Append(lines[k]).Append('\n');
                    var fm = new CodeBlock
                    {
                        Line = 1, EndLine = j + 1, Lang = "yaml", Info = "frontmatter",
                        Text = yaml.ToString().TrimEnd('\n')
                    };
                    doc.Blocks.Add(fm);
                    bodyStart = j + 1;
                    break;
                }
            }
        }

        var body = new List<string>();
        for (int i = bodyStart; i < lines.Count; i++) body.Add(lines[i]);

        CollectReferences(body, doc.Refs);
        var blocks = ParseBlocks(body, 0, body.Count, doc.Refs);
        foreach (var b in blocks)
        {
            b.Line += bodyStart;
            b.EndLine += bodyStart;
        }
        doc.Blocks.AddRange(blocks);
        return doc;
    }

    public static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                int end = i;
                if (end > start && text[end - 1] == '\r') end--;
                list.Add(text.Substring(start, end - start));
                start = i + 1;
            }
            else if (c == '\r')
            {
                list.Add(text.Substring(start, i - start));
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                start = i + 1;
            }
        }
        list.Add(text.Substring(start));
        return list;
    }

    static readonly Regex RefDef = new Regex(@"^ {0,3}\[([^\]]+)\]:\s*(<[^>]*>|\S+)(?:\s+(?:""([^""]*)""|'([^']*)'|\(([^)]*)\)))?\s*$", RegexOptions.Compiled);

    static void CollectReferences(List<string> lines, Dictionary<string, LinkRef> refs)
    {
        bool inFence = false; char fenceChar = '\0'; int fenceLen = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            var fence = MatchFence(line);
            if (fence != null)
            {
                if (!inFence) { inFence = true; fenceChar = fence.Value.Char; fenceLen = fence.Value.Len; }
                else if (fence.Value.Char == fenceChar && fence.Value.Len >= fenceLen && string.IsNullOrWhiteSpace(line.TrimStart().Substring(Math.Min(fence.Value.Len, line.TrimStart().Length)))) inFence = false;
                continue;
            }
            if (inFence) continue;
            var m = RefDef.Match(line);
            if (m.Success)
            {
                string label = NormalizeLabel(m.Groups[1].Value);
                string url = m.Groups[2].Value;
                if (url.StartsWith("<") && url.EndsWith(">") && url.Length >= 2) url = url.Substring(1, url.Length - 2);
                string title = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Success ? m.Groups[4].Value : m.Groups[5].Success ? m.Groups[5].Value : null;
                if (!refs.ContainsKey(label)) refs[label] = new LinkRef { Url = url, Title = title };
                lines[i] = "";
            }
        }
    }

    public static string NormalizeLabel(string s)
    {
        var sb = new StringBuilder();
        bool space = false;
        foreach (char c in s.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = true; continue; }
            if (space && sb.Length > 0) sb.Append(' ');
            space = false;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- blocks

    public static List<MdBlock> ParseBlocks(List<string> lines, int start, int end, Dictionary<string, LinkRef> refs)
    {
        var result = new List<MdBlock>();
        int i = start;
        while (i < end)
        {
            string line = lines[i];
            if (IsBlank(line)) { i++; continue; }

            var fence = MatchFence(line);
            if (fence != null)
            {
                int fenceStart = i;
                var sb = new StringBuilder();
                i++;
                while (i < end)
                {
                    var close = MatchFence(lines[i]);
                    if (close != null && close.Value.Char == fence.Value.Char && close.Value.Len >= fence.Value.Len &&
                        string.IsNullOrWhiteSpace(close.Value.Info))
                    { i++; break; }
                    if (i > fenceStart + 1) sb.Append('\n');
                    sb.Append(StripIndent(lines[i], fence.Value.Indent));
                    i++;
                }
                string info = Unescape(fence.Value.Info.Trim());
                string lang = info;
                int sp = lang.IndexOfAny(new[] { ' ', '\t', '{' });
                if (sp > 0) lang = lang.Substring(0, sp);
                result.Add(new CodeBlock
                {
                    Line = fenceStart + 1, EndLine = i, Text = sb.ToString(),
                    Info = info, Lang = lang.Trim('.').ToLowerInvariant()
                });
                continue;
            }

            if (TryAtxHeading(line, out int level, out string headingText))
            {
                result.Add(new HeadingBlock
                {
                    Level = level, Line = i + 1, EndLine = i + 1,
                    Inlines = InlineParser.Parse(headingText, refs)
                });
                i++;
                continue;
            }

            if (IsThematicBreak(line))
            {
                result.Add(new ThematicBreakBlock { Line = i + 1, EndLine = i + 1 });
                i++;
                continue;
            }

            if (IsQuoteLine(line))
            {
                int qStart = i;
                var inner = new List<string>();
                while (i < end)
                {
                    if (IsQuoteLine(lines[i]))
                    {
                        inner.Add(StripQuotePrefix(lines[i]));
                        i++;
                    }
                    else if (!IsBlank(lines[i]) && !StartsNewBlock(lines[i]) && inner.Count > 0 && !IsBlank(inner[inner.Count - 1]))
                    {
                        inner.Add(lines[i]);
                        i++;
                    }
                    else break;
                }
                result.Add(new QuoteBlock { Line = qStart + 1, EndLine = i, Blocks = ParseBlocks(inner, 0, inner.Count, refs) });
                continue;
            }

            if (TryMatchListItem(line, out var lm) && !IsThematicBreak(line))
            {
                result.Add(ParseList(lines, ref i, end, refs, lm));
                continue;
            }

            if (i + 1 < end && line.IndexOf('|') >= 0 && IsTableDelimiter(lines[i + 1]))
            {
                result.Add(ParseTable(lines, ref i, end, refs));
                continue;
            }

            if (LeadingSpaces(line) >= 4)
            {
                int cStart = i;
                var code = new StringBuilder();
                while (i < end)
                {
                    if (IsBlank(lines[i])) { code.Append('\n'); i++; continue; }
                    if (LeadingSpaces(lines[i]) < 4) break;
                    if (code.Length > 0 && code[code.Length - 1] != '\n') code.Append('\n');
                    code.Append(StripIndent(lines[i], 4));
                    i++;
                }
                result.Add(new CodeBlock { Line = cStart + 1, EndLine = i, Text = code.ToString().TrimEnd('\n'), Lang = "", Info = "" });
                continue;
            }

            if (line.TrimStart().StartsWith("<") && LooksLikeHtmlBlock(line))
            {
                int hStart = i;
                var sb = new StringBuilder();
                while (i < end && !IsBlank(lines[i]))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }
                result.Add(new HtmlBlock { Line = hStart + 1, EndLine = i, Text = sb.ToString() });
                continue;
            }

            // paragraph (possibly setext heading)
            {
                int pStart = i;
                var sb = new StringBuilder();
                sb.Append(line.Trim());
                if (line.Length - line.TrimEnd(' ').Length >= 2) sb.Append("  ");
                i++;
                bool setext = false;
                int setextLevel = 0;
                if (i < end && TrySetext(lines[i], out int sl))
                {
                    setext = true; setextLevel = sl; i++;
                }
                else
                {
                    while (i < end && !IsBlank(lines[i]))
                    {
                        if (StartsNewBlock(lines[i])) break;
                        if (i + 1 <= end && TrySetext(lines[i], out _)) break;
                        string raw = lines[i];
                        string trimmed = raw.Trim();
                        bool hard = raw.Length - raw.TrimEnd(' ').Length >= 2;
                        sb.Append('\n').Append(trimmed);
                        if (hard) sb.Append("  ");
                        i++;
                    }
                }
                string paraText = sb.ToString();
                if (setext)
                {
                    result.Add(new HeadingBlock
                    {
                        Level = setextLevel, Line = pStart + 1, EndLine = i,
                        Inlines = InlineParser.Parse(paraText, refs)
                    });
                }
                else
                {
                    result.Add(new ParagraphBlock
                    {
                        Line = pStart + 1, EndLine = i,
                        Inlines = InlineParser.Parse(paraText, refs)
                    });
                }
                continue;
            }
        }
        return result;
    }

    struct FenceInfo { public char Char; public int Len; public int Indent; public string Info; }

    static FenceInfo? MatchFence(string line)
    {
        int indent = LeadingSpaces(line);
        if (indent > 3) return null;
        string t = line.Substring(indent);
        if (t.Length < 3) return null;
        char c = t[0];
        if (c != '`' && c != '~') return null;
        int n = 0;
        while (n < t.Length && t[n] == c) n++;
        if (n < 3) return null;
        if (c == '`' && t.IndexOf('`', n) >= 0) return null;
        return new FenceInfo { Char = c, Len = n, Indent = indent, Info = t.Substring(n) };
    }

    static bool TryAtxHeading(string line, out int level, out string text)
    {
        level = 0; text = null;
        int indent = LeadingSpaces(line);
        if (indent > 3) return false;
        string t = line.Substring(indent);
        int n = 0;
        while (n < t.Length && t[n] == '#') n++;
        if (n == 0 || n > 6) return false;
        if (n < t.Length && t[n] != ' ' && t[n] != '\t') return false;
        string rest = t.Substring(n).Trim();
        // strip closing sequence
        int e = rest.Length;
        while (e > 0 && rest[e - 1] == '#') e--;
        if (e < rest.Length && (e == 0 || rest[e - 1] == ' ')) rest = rest.Substring(0, e).TrimEnd();
        level = n; text = rest;
        return true;
    }

    static bool TrySetext(string line, out int level)
    {
        level = 0;
        string t = line.Trim();
        if (t.Length == 0) return false;
        char c = t[0];
        if (c != '=' && c != '-') return false;
        for (int i = 1; i < t.Length; i++) if (t[i] != c) return false;
        if (t.Length < 1) return false;
        level = c == '=' ? 1 : 2;
        return true;
    }

    public static bool IsThematicBreak(string line)
    {
        string t = line.Trim();
        if (t.Length < 3) return false;
        char c = t[0];
        if (c != '-' && c != '*' && c != '_') return false;
        int count = 0;
        foreach (char ch in t)
        {
            if (ch == c) count++;
            else if (ch != ' ' && ch != '\t') return false;
        }
        return count >= 3;
    }

    static bool IsQuoteLine(string line)
    {
        int indent = LeadingSpaces(line);
        return indent <= 3 && indent < line.Length && line[indent] == '>';
    }

    static string StripQuotePrefix(string line)
    {
        int indent = LeadingSpaces(line);
        string t = line.Substring(indent + 1);
        if (t.StartsWith(" ")) t = t.Substring(1);
        return t;
    }

    struct ListMarker
    {
        public int Indent;
        public int MarkerLen;
        public int SpaceLen;
        public bool Ordered;
        public int Number;
        public string Content;
        public int ContentIndent => Indent + MarkerLen + SpaceLen;
    }

    static bool TryMatchListItem(string line, out ListMarker m)
    {
        m = default;
        if (IsBlank(line)) return false;
        int indent = LeadingSpaces(line);
        if (indent > 6) return false;
        string t = line.Substring(indent);
        if (t.Length == 0) return false;
        if (t[0] == '-' || t[0] == '+' || t[0] == '*')
        {
            if (t.Length < 2 || (t[1] != ' ' && t[1] != '\t')) return false;
            int sp = 0;
            while (1 + sp < t.Length && (t[1 + sp] == ' ' || t[1 + sp] == '\t')) sp++;
            m = new ListMarker { Indent = indent, MarkerLen = 1, SpaceLen = Math.Min(sp, 4), Ordered = false, Number = 1, Content = t.Substring(1 + sp) };
            return true;
        }
        int digits = 0;
        while (digits < t.Length && char.IsDigit(t[digits])) digits++;
        if (digits == 0 || digits > 9 || digits >= t.Length) return false;
        if (t[digits] != '.' && t[digits] != ')') return false;
        if (digits + 1 >= t.Length || (t[digits + 1] != ' ' && t[digits + 1] != '\t')) return false;
        int sp2 = 0;
        while (digits + 1 + sp2 < t.Length && (t[digits + 1 + sp2] == ' ' || t[digits + 1 + sp2] == '\t')) sp2++;
        int num = 1;
        int.TryParse(t.Substring(0, digits), out num);
        m = new ListMarker { Indent = indent, MarkerLen = digits + 1, SpaceLen = Math.Min(sp2, 4), Ordered = true, Number = num, Content = t.Substring(digits + 1 + sp2) };
        return true;
    }

    static ListBlock ParseList(List<string> lines, ref int i, int end, Dictionary<string, LinkRef> refs, ListMarker first)
    {
        var list = new ListBlock { Ordered = first.Ordered, Start = first.Number, Line = i + 1, Tight = true };
        int listIndent = first.Indent;
        bool seenBlankBetweenItems = false;

        while (i < end)
        {
            if (!TryMatchListItem(lines[i], out var m)) break;
            if (m.Indent != listIndent || m.Ordered != first.Ordered) break;
            if (IsThematicBreak(lines[i])) break;

            var item = new ListItemBlock { Line = i + 1 };
            var content = new List<string> { m.Content };
            int contentIndent = m.ContentIndent;
            i++;
            int blanks = 0;
            while (i < end)
            {
                string raw = lines[i];
                if (IsBlank(raw)) { content.Add(""); blanks++; i++; continue; }
                if (TryMatchListItem(raw, out var nm) && nm.Indent == listIndent)
                {
                    if (blanks > 0) seenBlankBetweenItems = true;
                    break;
                }
                int ind = LeadingSpaces(raw);
                if (ind >= contentIndent)
                {
                    if (blanks > 0) list.Tight = false;
                    blanks = 0;
                    content.Add(StripIndent(raw, contentIndent));
                    i++;
                    continue;
                }
                if (nm.Indent > listIndent && nm.Indent < contentIndent)
                {
                    blanks = 0;
                    content.Add(StripIndent(raw, nm.Indent));
                    i++;
                    continue;
                }
                if (!StartsNewBlock(raw))
                {
                    blanks = 0;
                    content.Add(raw.TrimStart());
                    i++;
                    continue;
                }
                break;
            }
            while (content.Count > 0 && content[content.Count - 1].Length == 0) content.RemoveAt(content.Count - 1);
            item.EndLine = i;
            item.Blocks = ParseBlocks(content, 0, content.Count, refs);
            DetectTask(item);
            list.Items.Add(item);
        }
        if (seenBlankBetweenItems) list.Tight = false;
        list.EndLine = i;
        return list;
    }

    static void DetectTask(ListItemBlock item)
    {
        if (item.Blocks.Count == 0) return;
        var para = item.Blocks[0] as ParagraphBlock;
        if (para == null || para.Inlines.Count == 0) return;
        var t = para.Inlines[0] as TextInline;
        if (t == null || t.Text.Length < 3) return;
        string s = t.Text;
        if (s[0] != '[' || s[2] != ']') return;
        char c = char.ToLowerInvariant(s[1]);
        if (c != ' ' && c != 'x') return;
        if (s.Length > 3 && s[3] != ' ' && s[3] != '\t') return;
        item.Task = c == 'x';
        string rest = s.Length > 3 ? s.Substring(4) : "";
        if (rest.Length > 0) t.Text = rest;
        else para.Inlines.RemoveAt(0);
    }

    static bool IsTableDelimiter(string line)
    {
        string t = line.Trim();
        if (t.Length == 0 || t.IndexOf('-') < 0) return false;
        if (t.Contains("|") == false) return false;
        foreach (char c in t)
        {
            if (c != '|' && c != '-' && c != ':' && c != ' ' && c != '\t') return false;
        }
        return true;
    }

    static TableBlock ParseTable(List<string> lines, ref int i, int end, Dictionary<string, LinkRef> refs)
    {
        var table = new TableBlock { Line = i + 1 };
        var header = SplitRow(lines[i]);
        var delim = SplitRow(lines[i + 1]);
        int cols = header.Count;
        for (int c = 0; c < cols; c++)
        {
            string d = c < delim.Count ? delim[c] : "---";
            bool left = d.StartsWith(":");
            bool right = d.EndsWith(":");
            table.Aligns.Add(left && right ? TableAlign.Center : right ? TableAlign.Right : left ? TableAlign.Left : TableAlign.None);
            table.Header.Add(InlineParser.Parse(header[c], refs));
        }
        i += 2;
        while (i < end && !IsBlank(lines[i]) && lines[i].IndexOf('|') >= 0)
        {
            var cells = SplitRow(lines[i]);
            var row = new List<List<MdInline>>();
            for (int c = 0; c < cols; c++) row.Add(InlineParser.Parse(c < cells.Count ? cells[c] : "", refs));
            table.Rows.Add(row);
            i++;
        }
        table.EndLine = i;
        return table;
    }

    public static List<string> SplitRow(string line)
    {
        string t = line.Trim();
        if (t.StartsWith("|")) t = t.Substring(1);
        if (t.EndsWith("|") && !t.EndsWith("\\|")) t = t.Substring(0, t.Length - 1);
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool code = false;
        for (int k = 0; k < t.Length; k++)
        {
            char c = t[k];
            if (c == '\\' && k + 1 < t.Length) { sb.Append(c).Append(t[k + 1]); k++; continue; }
            if (c == '`') code = !code;
            if (c == '|' && !code) { cells.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(c);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    static bool LooksLikeHtmlBlock(string line)
    {
        string t = line.TrimStart();
        if (t.Length < 2 || t[0] != '<') return false;
        if (t.StartsWith("<!--")) return true;
        char c = t[1];
        if (!char.IsLetter(c) && c != '!' && c != '/') return false;
        return true;
    }

    static string[] BlockTags = { "address", "article", "aside", "blockquote", "details", "div", "dl", "fieldset", "figure", "figcaption", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "iframe", "li", "main", "nav", "ol", "p", "pre", "section", "table", "tbody", "td", "tfoot", "th", "thead", "tr", "ul", "script", "style" };

    static bool StartsNewBlock(string line)
    {
        if (IsBlank(line)) return true;
        var f = MatchFence(line);
        if (f != null) return true;
        if (TryAtxHeading(line, out _, out _)) return true;
        if (IsThematicBreak(line)) return true;
        if (IsQuoteLine(line)) return true;
        if (TryMatchListItem(line, out _)) return true;
        return false;
    }

    // ---------------------------------------------------------------- helpers

    public static int LeadingSpaces(string s)
    {
        int n = 0;
        while (n < s.Length && (s[n] == ' ' || s[n] == '\t')) n++;
        return n;
    }

    public static string StripIndent(string s, int n)
    {
        int i = 0, col = 0;
        while (i < s.Length && col < n && (s[i] == ' ' || s[i] == '\t'))
        {
            col += s[i] == '\t' ? 4 - (col % 4) : 1;
            i++;
        }
        return s.Substring(i);
    }

    public static bool IsBlank(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        foreach (char c in s) if (c != ' ' && c != '\t') return false;
        return true;
    }

    public static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[i + 1]); i++; }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
