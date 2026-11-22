using System;

namespace Inkwell.App;

/// <summary>Scans a single Markdown line and marks character style flags for the editor.</summary>
public sealed class InlineHighlighter
{
    readonly string s;
    readonly int end;
    readonly MdStyleFlags[] flags;

    InlineHighlighter(string line, int end, MdStyleFlags[] flags)
    {
        s = line;
        this.end = end;
        this.flags = flags;
    }

    public static void Scan(string line, int start, int end, MdStyleFlags[] flags)
    {
        int e = Math.Min(end, line.Length);
        if (start < 0) start = 0;
        if (start >= e) return;
        var h = new InlineHighlighter(line, e, flags);
        h.ScanRange(start, e, MdStyleFlags.None);
    }

    static bool IsPunct(char c)
    {
        return (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
    }

    void Apply(int a, int b, MdStyleFlags f)
    {
        if (f == MdStyleFlags.None) return;
        int from = Math.Max(0, a);
        int to = Math.Min(flags.Length, b);
        for (int k = from; k < to; k++) flags[k] |= f;
    }

    void ScanRange(int begin, int finish, MdStyleFlags baseFlags)
    {
        Apply(begin, finish, baseFlags);
        int i = begin;
        while (i < finish)
        {
            char c = s[i];

            if (c == '\\' && i + 1 < finish && IsPunct(s[i + 1]))
            {
                Apply(i, i + 2, MdStyleFlags.Dim);
                i += 2;
                continue;
            }

            if (c == '`')
            {
                int n = CountRun(i, finish, '`');
                int close = FindRun(i + n, finish, '`', n);
                if (close >= 0)
                {
                    Apply(i, i + n, MdStyleFlags.Dim);
                    Apply(i + n, close, MdStyleFlags.Code);
                    Apply(close, close + n, MdStyleFlags.Dim);
                    i = close + n;
                    continue;
                }
                i += n;
                continue;
            }

            if (c == '!' && i + 1 < finish && s[i + 1] == '[')
            {
                if (TryLink(i + 1, finish, true, out int textStart, out int textEnd, out int urlStart, out int urlEnd, out int after))
                {
                    Apply(i, i + 2, MdStyleFlags.Dim);
                    Apply(textStart, textEnd, MdStyleFlags.Link | MdStyleFlags.Underline);
                    Apply(textEnd, textEnd + 1, MdStyleFlags.Dim);
                    if (urlEnd > urlStart) Apply(urlStart, urlEnd, MdStyleFlags.Url | MdStyleFlags.Dim);
                    if (urlStart > 0) Apply(urlStart - 1, urlStart, MdStyleFlags.Dim);
                    Apply(urlEnd, after, MdStyleFlags.Dim);
                    ScanRange(textStart, textEnd, baseFlags | MdStyleFlags.Link | MdStyleFlags.Underline);
                    i = after;
                    continue;
                }
                i++;
                continue;
            }

            if (c == '[')
            {
                if (TryLink(i, finish, false, out int textStart, out int textEnd, out int urlStart, out int urlEnd, out int after))
                {
                    Apply(i, i + 1, MdStyleFlags.Dim);
                    Apply(textStart, textEnd, MdStyleFlags.Link | MdStyleFlags.Underline);
                    Apply(textEnd, textEnd + 1, MdStyleFlags.Dim);
                    if (urlEnd > urlStart) Apply(urlStart, urlEnd, MdStyleFlags.Url | MdStyleFlags.Dim);
                    if (urlStart > 0) Apply(urlStart - 1, urlStart, MdStyleFlags.Dim);
                    Apply(urlEnd, after, MdStyleFlags.Dim);
                    ScanRange(textStart, textEnd, baseFlags | MdStyleFlags.Link | MdStyleFlags.Underline);
                    i = after;
                    continue;
                }
                i++;
                continue;
            }

            if (c == '<')
            {
                int gt = s.IndexOf('>', i + 1);
                if (gt > i + 1 && gt < finish)
                {
                    string inside = s.Substring(i + 1, gt - i - 1);
                    if (LooksLikeAutolink(inside))
                    {
                        Apply(i, i + 1, MdStyleFlags.Dim);
                        Apply(i + 1, gt, MdStyleFlags.Link | MdStyleFlags.Underline);
                        Apply(gt, gt + 1, MdStyleFlags.Dim);
                        i = gt + 1;
                        continue;
                    }
                    if (LooksLikeHtmlTag(inside))
                    {
                        Apply(i, gt + 1, MdStyleFlags.Dim);
                        i = gt + 1;
                        continue;
                    }
                }
                i++;
                continue;
            }

            if (c == '*' || c == '_')
            {
                int next = TryEmphasis(i, finish, baseFlags);
                if (next > i) { i = next; continue; }
                i += CountRun(i, finish, c);
                continue;
            }

            if (c == '~' && i + 1 < finish && s[i + 1] == '~')
            {
                int close = s.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (close > i + 2 && close < finish)
                {
                    Apply(i, i + 2, MdStyleFlags.Dim);
                    Apply(close, close + 2, MdStyleFlags.Dim);
                    ScanRange(i + 2, close, baseFlags | MdStyleFlags.Strike);
                    i = close + 2;
                    continue;
                }
                i += 2;
                continue;
            }

            if (c == '=' && i + 1 < finish && s[i + 1] == '=')
            {
                int close = s.IndexOf("==", i + 2, StringComparison.Ordinal);
                if (close > i + 2 && close < finish)
                {
                    Apply(i, i + 2, MdStyleFlags.Dim);
                    Apply(close, close + 2, MdStyleFlags.Dim);
                    ScanRange(i + 2, close, baseFlags | MdStyleFlags.Mark);
                    i = close + 2;
                    continue;
                }
                i += 2;
                continue;
            }

            i++;
        }
    }

    int CountRun(int i, int finish, char c)
    {
        int n = 0;
        while (i + n < finish && s[i + n] == c) n++;
        return n;
    }

    int FindRun(int from, int finish, char c, int len)
    {
        int i = from;
        while (i < finish)
        {
            if (s[i] == '\\') { i += 2; continue; }
            if (s[i] == c)
            {
                int n = 0;
                while (i + n < finish && s[i + n] == c) n++;
                if (n == len) return i;
                i += n;
                continue;
            }
            i++;
        }
        return -1;
    }

    int TryEmphasis(int i, int finish, MdStyleFlags baseFlags)
    {
        char c = s[i];
        int n = 0;
        while (i + n < finish && s[i + n] == c) n++;
        if (n == 0) return -1;

        char before = i > 0 ? s[i - 1] : '\0';
        char after = i + n < finish ? s[i + n] : '\0';
        bool beforeWs = before == '\0' || char.IsWhiteSpace(before);
        bool afterWs = after == '\0' || char.IsWhiteSpace(after);
        bool beforePunct = before != '\0' && IsPunct(before);
        bool afterPunct = after != '\0' && IsPunct(after);
        bool leftFlanking = !afterWs && (!afterPunct || beforeWs || beforePunct);
        bool rightFlanking = !beforeWs && (!beforePunct || afterWs || afterPunct);
        bool canOpen = c == '_' ? (leftFlanking && (!rightFlanking || beforePunct)) : leftFlanking;
        if (!canOpen) return -1;

        for (int use = Math.Min(n, 3); use >= 1; use--)
        {
            int close = FindClose(i + n, finish, c, use);
            if (close < 0) continue;
            Apply(i, i + use, MdStyleFlags.Dim);
            Apply(close, close + use, MdStyleFlags.Dim);
            MdStyleFlags inner = baseFlags;
            if (use >= 2) inner |= MdStyleFlags.Bold;
            if (use == 1 || use >= 3) inner |= MdStyleFlags.Italic;
            ScanRange(i + use, close, inner);
            return close + use;
        }
        return -1;
    }

    int FindClose(int from, int finish, char c, int use)
    {
        int i = from;
        while (i < finish)
        {
            char ch = s[i];
            if (ch == '\\') { i += 2; continue; }
            if (ch == '`')
            {
                int n = 0;
                while (i + n < finish && s[i + n] == '`') n++;
                int close = FindRun(i + n, finish, '`', n);
                i = close >= 0 ? close + n : i + n;
                continue;
            }
            if (ch == c)
            {
                int n = 0;
                while (i + n < finish && s[i + n] == c) n++;
                char prev = i > 0 ? s[i - 1] : '\0';
                char next = i + n < finish ? s[i + n] : '\0';
                bool prevWs = prev == '\0' || char.IsWhiteSpace(prev);
                bool nextWs = next == '\0' || char.IsWhiteSpace(next);
                bool prevPunct = prev != '\0' && IsPunct(prev);
                bool nextPunct = next != '\0' && IsPunct(next);
                bool rightFlanking = !prevWs && (!prevPunct || nextWs || nextPunct);
                bool leftFlanking = !nextWs && (!nextPunct || prevWs || prevPunct);
                bool canClose = c == '_' ? (rightFlanking && (!leftFlanking || nextPunct)) : rightFlanking;
                if (canClose && n >= use) return i;
                i += n;
                continue;
            }
            i++;
        }
        return -1;
    }

    bool TryLink(int bracket, int finish, bool image, out int textStart, out int textEnd, out int urlStart, out int urlEnd, out int after)
    {
        textStart = textEnd = urlStart = urlEnd = 0;
        after = bracket;
        int close = FindBracket(bracket, finish);
        if (close < 0) return false;
        textStart = bracket + 1;
        textEnd = close;
        int p = close + 1;
        if (p < finish && s[p] == '(')
        {
            int depth = 0;
            int q = p + 1;
            int destStart = -1, destEnd = -1;
            while (q < finish)
            {
                char ch = s[q];
                if (ch == '\\') { q += 2; continue; }
                if (ch == '<' && destStart < 0)
                {
                    int gt = s.IndexOf('>', q + 1);
                    if (gt < 0 || gt >= finish) return false;
                    destStart = q + 1;
                    destEnd = gt;
                    q = gt + 1;
                    continue;
                }
                if (ch == ' ' || ch == '\t')
                {
                    if (destStart < 0) { destStart = p + 1; destEnd = q; }
                    q++;
                    continue;
                }
                if (ch == '(') depth++;
                else if (ch == ')')
                {
                    if (depth == 0) break;
                    depth--;
                }
                q++;
            }
            if (q >= finish || s[q] != ')') return false;
            if (destStart < 0) { destStart = p + 1; destEnd = q; }
            urlStart = destStart;
            urlEnd = destEnd;
            after = q + 1;
            return true;
        }
        if (p < finish && s[p] == '[')
        {
            int rb = s.IndexOf(']', p + 1);
            if (rb < 0 || rb >= finish) return false;
            after = rb + 1;
            urlStart = p + 1;
            urlEnd = rb;
            return true;
        }
        return false;
    }

    int FindBracket(int open, int finish)
    {
        int depth = 0;
        for (int i = open; i < finish; i++)
        {
            char c = s[i];
            if (c == '\\') { i++; continue; }
            if (c == '`')
            {
                int n = 0;
                while (i + n < finish && s[i + n] == '`') n++;
                int close = FindRun(i + n, finish, '`', n);
                if (close >= 0) { i = close + n - 1; continue; }
            }
            if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    static bool LooksLikeAutolink(string inside)
    {
        if (inside.Length < 3 || inside.IndexOf(' ') >= 0) return false;
        int colon = inside.IndexOf(':');
        if (colon > 0)
        {
            for (int i = 0; i < colon; i++)
            {
                char c = inside[i];
                if (!(char.IsLetterOrDigit(c) || c == '+' || c == '.' || c == '-')) return false;
            }
            return true;
        }
        int at = inside.IndexOf('@');
        return at > 0 && inside.IndexOf('.', at) > at + 1;
    }

    static bool LooksLikeHtmlTag(string inside)
    {
        if (inside.Length == 0) return false;
        char c = inside[0];
        return char.IsLetter(c) || c == '/' || c == '!' || c == '?';
    }

    static int IndexOf(string s, string needle, int start)
    {
        return s.IndexOf(needle, start, StringComparison.Ordinal);
    }
}
