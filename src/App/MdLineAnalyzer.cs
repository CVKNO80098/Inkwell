using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Inkwell.App;

[Flags]
public enum MdStyleFlags
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,
    Mark = 16,
    Link = 32,
    Dim = 64,
    Underline = 128,
    Url = 256,
    Accent = 512
}

public enum MdLineKind
{
    Normal, Blank, Heading, CodeFence, Code, FrontMatter, Quote, List, Table, Hr, SetextUnderline
}

public sealed class MdLineInfo
{
    public MdLineKind Kind;
    public int HeadingLevel = 1;
    public int QuoteDepth;
    public int ListIndent;
    public int ContentStart;
    public int ContentEnd;
    public bool TaskItem;
    public bool TaskChecked;
    public string FenceLang = "";
    public bool TableHeader;
    public bool TableDelimiter;
    public bool InTable;
    public bool CodeStart;
    public bool CodeEnd;
    public List<Deco> Decos = new List<Deco>();

    public string Sig()
    {
        return (int)Kind + "|" + HeadingLevel + "|" + QuoteDepth + "|" + ListIndent + "|" +
               (TaskItem ? 1 : 0) + (TaskChecked ? 1 : 0) + "|" + (TableHeader ? 1 : 0) +
               (TableDelimiter ? 1 : 0) + "|" + (CodeStart ? 1 : 0) + (CodeEnd ? 1 : 0) + "|" + FenceLang;
    }
}

public struct Deco
{
    public int Start;
    public int Length;
    public MdStyleFlags Flags;
    public Deco(int s, int l, MdStyleFlags f) { Start = s; Length = l; Flags = f; }
}

/// <summary>Line based Markdown analysis used for editor syntax decoration.</summary>
public static class MdLineAnalyzer
{
    public static MdLineInfo[] Analyze(string[] lines)
    {
        var infos = new MdLineInfo[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            infos[i] = new MdLineInfo { ContentEnd = lines[i].Length };
            infos[i].Kind = MdLineKind.Normal;
        }

        int start = 0;
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            int close = -1;
            for (int j = 1; j < lines.Length && j < 400; j++)
            {
                string t = lines[j].Trim();
                if (t == "---" || t == "...") { close = j; break; }
            }
            if (close > 0)
            {
                for (int k = 0; k <= close; k++)
                {
                    infos[k].Kind = (k == 0 || k == close) ? MdLineKind.CodeFence : MdLineKind.FrontMatter;
                    infos[k].Decos.Add(new Deco(0, lines[k].Length, MdStyleFlags.Dim));
                }
                start = close + 1;
            }
        }

        int li = start;
        int n = lines.Length;
        while (li < n)
        {
            string line = lines[li];
            if (line.Trim().Length == 0)
            {
                infos[li].Kind = MdLineKind.Blank;
                li++;
                continue;
            }

            var fence = MatchFence(line);
            if (fence.HasValue)
            {
                int close = FindFence(lines, li + 1, fence.Value.Char, fence.Value.Len);
                string info = fence.Value.Info.Trim();
                for (int j = li; j <= close; j++)
                {
                    bool edge = j == li || j == close;
                    infos[j].Kind = edge ? MdLineKind.CodeFence : MdLineKind.Code;
                    infos[j].FenceLang = info;
                    if (!edge)
                    {
                        infos[j].CodeStart = j == li + 1;
                        infos[j].CodeEnd = j == close - 1;
                    }
                    if (edge) infos[j].Decos.Add(new Deco(0, lines[j].Length, MdStyleFlags.Dim));
                }
                if (close == li) infos[li].Kind = MdLineKind.CodeFence;
                li = close + 1;
                continue;
            }

            // table
            if (li + 1 < n && IsTableRow(line) && IsDelimiterRow(lines[li + 1]))
            {
                int j = li;
                while (j < n && lines[j].Contains("|") && lines[j].Trim().Length > 0)
                {
                    infos[j].Kind = MdLineKind.Table;
                    infos[j].InTable = true;
                    infos[j].TableHeader = j == li;
                    infos[j].TableDelimiter = j == li + 1;
                    AddTableDecos(lines[j], infos[j]);
                    j++;
                }
                li = j;
                continue;
            }

            // setext heading
            if (li + 1 < n && IsSetextUnderline(lines[li + 1]) && CanBeSetextContent(line))
            {
                var head = new MdLineInfo { Kind = MdLineKind.Heading, ContentEnd = line.Length };
                int level = lines[li + 1].TrimStart().StartsWith("=", StringComparison.Ordinal) ? 1 : 2;
                head.HeadingLevel = level;
                AnalyzeInlineParts(line, head);
                infos[li] = head;

                infos[li + 1].Kind = MdLineKind.SetextUnderline;
                infos[li + 1].Decos.Add(new Deco(0, lines[li + 1].Length, MdStyleFlags.Dim));
                li += 2;
                continue;
            }

            AnalyzeLine(line, infos[li]);
            li++;
        }
        return infos;
    }

    static bool CanBeSetextContent(string line)
    {
        string t = line.TrimStart();
        if (t.Length == 0) return false;
        char c = t[0];
        if (c == '>' || c == '|' || c == '#' || c == '`' || c == '~') return false;
        if (TryListMarker(line, 0, out _, out _, out _, out _)) return false;
        return true;
    }

    struct Fence
    {
        public char Char;
        public int Len;
        public string Info;
    }

    static Fence? MatchFence(string line)
    {
        int ind = 0;
        while (ind < line.Length && line[ind] == ' ' && ind < 4) ind++;
        if (ind > 3) return null;
        if (ind >= line.Length) return null;
        char c = line[ind];
        if (c != '`' && c != '~') return null;
        int n = 0;
        while (ind + n < line.Length && line[ind + n] == c) n++;
        if (n < 3) return null;
        string rest = line.Substring(ind + n);
        if (c == '`' && rest.IndexOf('`') >= 0) return null;
        return new Fence { Char = c, Len = n, Info = rest };
    }

    static int FindFence(string[] lines, int from, char c, int len)
    {
        for (int j = from; j < lines.Length; j++)
        {
            string t = lines[j].TrimStart();
            if (t.Length >= len && t[0] == c)
            {
                int k = 0;
                while (k < t.Length && t[k] == c) k++;
                if (k >= len && t.Substring(k).Trim().Length == 0) return j;
            }
        }
        return lines.Length > from ? lines.Length - 1 : from - 1;
    }

    static bool IsTableRow(string line)
    {
        string t = line.Trim();
        return t.Contains("|") && t.Length > 0;
    }

    static bool IsDelimiterRow(string line)
    {
        string t = line.Trim();
        if (t.Length == 0) return false;
        bool dash = false, pipe = false;
        foreach (char c in t)
        {
            if (c == '|') pipe = true;
            else if (c == '-') dash = true;
            else if (c != ':' && c != ' ' && c != '\t') return false;
        }
        return dash && pipe;
    }

    static void AddTableDecos(string line, MdLineInfo info)
    {
        if (info.TableDelimiter)
        {
            info.Decos.Add(new Deco(0, line.Length, MdStyleFlags.Dim));
            return;
        }
        for (int k = 0; k < line.Length; k++)
        {
            if (line[k] == '|') info.Decos.Add(new Deco(k, 1, MdStyleFlags.Dim));
        }
        if (info.TableHeader)
        {
            info.Decos.Add(new Deco(0, line.Length, MdStyleFlags.Bold));
        }
    }

    static bool IsSetextUnderline(string line)
    {
        string t = line.Trim();
        if (t.Length == 0) return false;
        char c = t[0];
        if (c != '=' && c != '-') return false;
        foreach (char ch in t) if (ch != c) return false;
        return true;
    }

    public static void AnalyzeLine(string line, MdLineInfo info)
    {
        info.ContentStart = 0;
        info.ContentEnd = line.Length;

        // blockquote markers
        int p = 0;
        int quotes = 0;
        while (p < line.Length && line[p] == '>')
        {
            quotes++;
            p++;
            if (p < line.Length && line[p] == ' ') p++;
        }
        if (quotes > 0)
        {
            info.QuoteDepth = quotes;
            info.Kind = MdLineKind.Quote;
            int k = 0;
            for (int q = 0; q < quotes; q++)
            {
                info.Decos.Add(new Deco(k, 1, MdStyleFlags.Accent));
                k++;
                if (k < line.Length && line[k] == ' ') k++;
            }
            info.ContentStart = p;
        }

        // heading
        int h = 0;
        while (p + h < line.Length && line[p + h] == '#') h++;
        if (h >= 1 && h <= 6 && (p + h == line.Length || line[p + h] == ' '))
        {
            info.Kind = MdLineKind.Heading;
            info.HeadingLevel = h;
            info.Decos.Add(new Deco(p, h, MdStyleFlags.Dim));
            info.ContentStart = p + h;
            if (info.ContentStart < line.Length && line[info.ContentStart] == ' ') info.ContentStart++;
            // closing hashes
            int end = line.Length;
            while (end > info.ContentStart && line[end - 1] == ' ') end--;
            int close = end;
            while (close > info.ContentStart && line[close - 1] == '#') close--;
            if (close < end && (close == info.ContentStart || line[close - 1] == ' '))
            {
                info.Decos.Add(new Deco(close, end - close, MdStyleFlags.Dim));
                info.ContentEnd = close;
                while (info.ContentEnd > info.ContentStart && line[info.ContentEnd - 1] == ' ') info.ContentEnd--;
            }
            return;
        }

        // thematic break (must be checked before list: "* * *" is a break, not a list)
        if (IsThematicBreak(line))
        {
            info.Kind = MdLineKind.Hr;
            info.Decos.Add(new Deco(0, line.Length, MdStyleFlags.Dim));
            return;
        }

        // list
        if (TryListMarker(line, p, out int afterMarker, out bool ordered, out int number, out string markerText))
        {
            int contentStart = afterMarker;
            bool task = false;
            bool taskDone = false;
            if (contentStart + 3 <= line.Length && line[contentStart] == '[' && line[contentStart + 2] == ']' &&
                (line[contentStart + 1] == ' ' || line[contentStart + 1] == 'x' || line[contentStart + 1] == 'X'))
            {
                char mark = line[contentStart + 1];
                if (mark == ' ' || mark == 'x' || mark == 'X')
                {
                    int after = contentStart + 3;
                    if (after < line.Length && line[after] == ' ')
                    {
                        task = true;
                        taskDone = mark != ' ';
                        info.Decos.Add(new Deco(contentStart, 3, MdStyleFlags.Accent | (taskDone ? MdStyleFlags.Bold : MdStyleFlags.None)));
                        contentStart = after + 1;
                    }
                }
            }

            info.Kind = MdLineKind.List;
            info.TaskItem = task;
            info.TaskChecked = taskDone;
            int indentSpaces = 0;
            int q = p;
            while (q < line.Length && line[q] == ' ') { indentSpaces++; q++; }
            info.ListIndent = Math.Min(6, indentSpaces / 2);
            info.Decos.Add(new Deco(p + indentSpaces, markerText.Length - indentSpaces, MdStyleFlags.Accent));
            info.ContentStart = contentStart;
            return;
        }

        // thematic break
        if (IsThematicBreak(line))
        {
            info.Kind = MdLineKind.Hr;
            info.Decos.Add(new Deco(0, line.Length, MdStyleFlags.Dim));
            return;
        }

        if (info.Kind != MdLineKind.Quote) info.Kind = MdLineKind.Normal;
    }

    static void AnalyzeInlineParts(string line, MdLineInfo info)
    {
        if (info.ContentEnd <= 0 || info.ContentEnd > line.Length) info.ContentEnd = line.Length;
        if (info.ContentStart > info.ContentEnd) info.ContentStart = info.ContentEnd;
    }

    public static bool IsThematicBreak(string line)
    {
        string t = line.Trim();
        if (t.Length < 3) return false;
        char c = t[0];
        if (c != '*' && c != '-' && c != '_') return false;
        int count = 0;
        foreach (char ch in t)
        {
            if (ch == c) count++;
            else if (ch != ' ' && ch != '\t') return false;
        }
        return count >= 3;
    }

    static bool TryListMarker(string line, int p, out int afterMarker, out bool ordered, out int number, out string markerText)
    {
        afterMarker = 0;
        ordered = false;
        number = 1;
        markerText = "";
        int indent = 0;
        while (p + indent < line.Length && (line[p + indent] == ' ' || line[p + indent] == '\t')) indent++;
        int m = p + indent;
        if (m >= line.Length) return false;

        int markerLen = 0;
        char c = line[m];
        if (c == '-' || c == '+' || c == '*')
        {
            markerLen = 1;
        }
        else if (char.IsDigit(c))
        {
            int d = 0;
            while (m + d < line.Length && char.IsDigit(line[m + d]) && d < 9) d++;
            if (d == 0 || m + d >= line.Length) return false;
            if (line[m + d] != '.' && line[m + d] != ')') return false;
            number = int.Parse(line.Substring(m, d));
            ordered = true;
            markerLen = d + 1;
        }
        else
        {
            return false;
        }

        int after = m + markerLen;
        if (after < line.Length && line[after] != ' ' && line[after] != '\t') return false;
        int spaces = 0;
        while (after + spaces < line.Length && (line[after + spaces] == ' ' || line[after + spaces] == '\t') && spaces < 5) spaces++;
        if (spaces == 0) return false; // empty item without trailing space: treat as text
        afterMarker = after + spaces;
        markerText = line.Substring(m, afterMarker - m);
        return true;
    }

    public static string InlineTextOf(string line, MdLineInfo info)
    {
        if (info.ContentStart >= line.Length) return "";
        int end = Math.Min(info.ContentEnd, line.Length);
        if (end <= info.ContentStart) return "";
        return line.Substring(info.ContentStart, end - info.ContentStart);
    }
}
