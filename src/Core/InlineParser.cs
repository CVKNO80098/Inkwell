using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Inkwell.Core;

/// <summary>Inline Markdown parser: emphasis, code spans, links, images,
/// autolinks, escapes, entities, hard/soft breaks, ==mark== and ~~strike~~.</summary>
public static class InlineParser
{
    public static List<MdInline> Parse(string text, Dictionary<string, LinkRef> refs)
    {
        var impl = new Impl(text ?? "", refs ?? new Dictionary<string, LinkRef>(StringComparer.OrdinalIgnoreCase));
        return impl.Run(0, (text ?? "").Length);
    }

    sealed class Impl
    {
        readonly string s;
        readonly Dictionary<string, LinkRef> refs;

        public Impl(string text, Dictionary<string, LinkRef> references)
        {
            s = text; refs = references;
        }

        static bool IsAsciiPunct(char c)
        {
            return (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
        }

        public List<MdInline> Run(int begin, int end)
        {
            var nodes = new List<MdInline>();
            int i = begin;
            var text = new StringBuilder();

            void Flush()
            {
                if (text.Length == 0) return;
                nodes.Add(new TextInline(text.ToString()));
                text.Clear();
            }

            while (i < end)
            {
                char c = s[i];

                if (c == '\\' && i + 1 < end)
                {
                    char n = s[i + 1];
                    if (n == '\n') { Flush(); nodes.Add(new BreakInline { Hard = true }); i += 2; continue; }
                    if (IsAsciiPunct(n)) { text.Append(n); i += 2; continue; }
                    text.Append(c); i++; continue;
                }

                if (c == '\n')
                {
                    bool hard = false;
                    if (text.Length >= 2 && text[text.Length - 1] == ' ' && text[text.Length - 2] == ' ') hard = true;
                    int k = text.Length;
                    while (k > 0 && text[k - 1] == ' ') k--;
                    text.Length = k;
                    Flush();
                    nodes.Add(new BreakInline { Hard = hard });
                    i++;
                    while (i < end && (s[i] == ' ' || s[i] == '\t')) i++;
                    continue;
                }

                if (c == '`')
                {
                    int n = 0;
                    while (i + n < end && s[i + n] == '`') n++;
                    int close = FindBacktickRun(i + n, end, n);
                    if (close >= 0)
                    {
                        Flush();
                        string code = s.Substring(i + n, close - (i + n)).Replace('\n', ' ');
                        if (code.Length >= 2 && code[0] == ' ' && code[code.Length - 1] == ' ') code = code.Substring(1, code.Length - 2);
                        nodes.Add(new CodeInline(code));
                        i = close + n;
                        continue;
                    }
                    text.Append('`', n);
                    i += n;
                    continue;
                }

                if (c == '!' && i + 1 < end && s[i + 1] == '[')
                {
                    if (TryParseLink(i + 1, end, true, out var img, out int after))
                    {
                        Flush();
                        nodes.Add(img);
                        i = after;
                        continue;
                    }
                    text.Append('!'); i++; continue;
                }

                if (c == '[')
                {
                    if (TryParseLink(i, end, false, out var link, out int after))
                    {
                        Flush();
                        nodes.Add(link);
                        i = after;
                        continue;
                    }
                    text.Append('['); i++; continue;
                }

                if (c == '<')
                {
                    int gt = s.IndexOf('>', i + 1);
                    if (gt > i + 1 && gt < end)
                    {
                        string inside = s.Substring(i + 1, gt - i - 1);
                        if (LooksLikeAutolink(inside))
                        {
                            Flush();
                            string url = inside.IndexOf(':') > 0 ? inside : "mailto:" + inside;
                            var li = new LinkInline { Url = url };
                            li.Children.Add(new TextInline(inside));
                            nodes.Add(li);
                            i = gt + 1;
                            continue;
                        }
                        if (LooksLikeHtmlTag(inside))
                        {
                            Flush();
                            nodes.Add(new HtmlInline("<" + inside + ">"));
                            i = gt + 1;
                            continue;
                        }
                    }
                    text.Append('<'); i++; continue;
                }

                if ((c == '*' || c == '_') && TryEmphasis(i, end, out var emph, out int next))
                {
                    Flush();
                    nodes.Add(emph);
                    i = next;
                    continue;
                }

                if (c == '~' && i + 1 < end && s[i + 1] == '~')
                {
                    int close = s.IndexOf("~~", i + 2, end - (i + 2), StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        Flush();
                        var st = new EmphasisInline { Strike = true };
                        st.Children.AddRange(Run(i + 2, close));
                        nodes.Add(st);
                        i = close + 2;
                        continue;
                    }
                }

                if (c == '=' && i + 1 < end && s[i + 1] == '=')
                {
                    int close = s.IndexOf("==", i + 2, end - (i + 2), StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        Flush();
                        var mk = new EmphasisInline { Mark = true };
                        mk.Children.AddRange(Run(i + 2, close));
                        nodes.Add(mk);
                        i = close + 2;
                        continue;
                    }
                }

                if (c == '&')
                {
                    int semi = s.IndexOf(';', i + 1);
                    if (semi > i + 1 && semi - i <= 10)
                    {
                        string ent = s.Substring(i, semi - i + 1);
                        string dec = WebUtility.HtmlDecode(ent);
                        if (dec != ent) { text.Append(dec); i = semi + 1; continue; }
                    }
                }

                text.Append(c);
                i++;
            }
            Flush();
            return nodes;
        }

        int FindBacktickRun(int from, int end, int len)
        {
            int i = from;
            while (i < end)
            {
                if (s[i] == '`')
                {
                    int n = 0;
                    while (i + n < end && s[i + n] == '`') n++;
                    if (n == len) return i;
                    i += n;
                }
                else i++;
            }
            return -1;
        }

        bool TryEmphasis(int i, int end, out MdInline node, out int next)
        {
            node = null; next = i;
            char c = s[i];
            int run = 0;
            while (i + run < end && s[i + run] == c) run++;
            if (run == 0) return false;

            char before = i > 0 ? s[i - 1] : '\0';
            char after = i + run < end ? s[i + run] : '\0';
            bool beforeWs = before == '\0' || char.IsWhiteSpace(before);
            bool afterWs = after == '\0' || char.IsWhiteSpace(after);
            bool beforePunct = before != '\0' && IsAsciiPunct(before);
            bool afterPunct = after != '\0' && IsAsciiPunct(after);
            bool leftFlanking = !afterWs && (!afterPunct || beforeWs || beforePunct);
            bool rightFlanking = !beforeWs && (!beforePunct || afterWs || afterPunct);
            bool canOpen = c == '_' ? (leftFlanking && (!rightFlanking || beforePunct)) : leftFlanking;
            if (!canOpen) return false;

            for (int use = Math.Min(run, 3); use >= 1; use--)
            {
                int close = FindEmphasisClose(i + run, end, c, use);
                if (close < 0) continue;
                node = BuildEmphasis(i + run, close, use);
                next = close + use;
                return true;
            }
            return false;
        }

        MdInline BuildEmphasis(int from, int to, int use)
        {
            var inner = Run(from, to);
            if (use >= 2)
            {
                var strong = new EmphasisInline { Strong = true };
                strong.Children.AddRange(inner);
                if (use >= 3)
                {
                    var em = new EmphasisInline { Italic = true };
                    em.Children.Add(strong);
                    return em;
                }
                return strong;
            }
            var e = new EmphasisInline { Italic = true };
            e.Children.AddRange(inner);
            return e;
        }

        int FindEmphasisClose(int from, int end, char c, int use)
        {
            int i = from;
            while (i < end)
            {
                char ch = s[i];
                if (ch == '\\') { i += 2; continue; }
                if (ch == '`')
                {
                    int n = 0;
                    while (i + n < end && s[i + n] == '`') n++;
                    int close = FindBacktickRun(i + n, end, n);
                    i = close >= 0 ? close + n : i + n;
                    continue;
                }
                if (ch == c)
                {
                    int n = 0;
                    while (i + n < end && s[i + n] == c) n++;
                    char prev = i > 0 ? s[i - 1] : '\0';
                    char next = i + n < end ? s[i + n] : '\0';
                    bool prevWs = prev == '\0' || char.IsWhiteSpace(prev);
                    bool nextWs = next == '\0' || char.IsWhiteSpace(next);
                    bool prevPunct = prev != '\0' && IsAsciiPunct(prev);
                    bool nextPunct = next != '\0' && IsAsciiPunct(next);
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

        bool TryParseLink(int openBracket, int end, bool image, out MdInline node, out int after)
        {
            node = null; after = openBracket;
            int close = FindClosingBracket(openBracket, end);
            if (close < 0) return false;
            string label = s.Substring(openBracket + 1, close - openBracket - 1);
            int p = close + 1;

            string url = null, title = null;
            int consumed = p;

            if (p < end && s[p] == '(')
            {
                if (!ParseDestination(p, end, out url, out title, out consumed)) return false;
            }
            else if (p < end && s[p] == '[')
            {
                int rb = s.IndexOf(']', p + 1);
                if (rb < 0 || rb >= end) return false;
                string refLabel = s.Substring(p + 1, rb - p - 1);
                if (refLabel.Trim().Length == 0) refLabel = label;
                string key = MarkdownParser.NormalizeLabel(refLabel);
                if (!refs.TryGetValue(key, out var r)) return false;
                url = r.Url; title = r.Title;
                consumed = rb + 1;
            }
            else
            {
                string key = MarkdownParser.NormalizeLabel(label);
                if (!refs.TryGetValue(key, out var r)) return false;
                url = r.Url; title = r.Title;
                consumed = p;
            }

            if (string.IsNullOrEmpty(url)) return false;

            if (image)
            {
                node = new ImageInline { Url = url, Alt = StripMarkdown(label), Title = title };
            }
            else
            {
                var link = new LinkInline { Url = url, Title = title };
                link.Children.AddRange(Run(openBracket + 1, close));
                node = link;
            }
            after = consumed;
            return true;
        }

        static string StripMarkdown(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '*' || c == '_' || c == '`' || c == '~' || c == '=' || c == '\\') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        int FindClosingBracket(int open, int end)
        {
            int depth = 0;
            for (int i = open; i < end; i++)
            {
                char c = s[i];
                if (c == '\\') { i++; continue; }
                if (c == '`')
                {
                    int n = 0;
                    while (i + n < end && s[i + n] == '`') n++;
                    int close = FindBacktickRun(i + n, end, n);
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

        bool ParseDestination(int p, int end, out string url, out string title, out int after)
        {
            url = null; title = null; after = p;
            int i = p + 1;
            while (i < end && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n')) i++;
            var sb = new StringBuilder();

            if (i < end && s[i] == '<')
            {
                int gt = s.IndexOf('>', i + 1);
                if (gt < 0 || gt >= end) return false;
                sb.Append(s.Substring(i + 1, gt - i - 1));
                i = gt + 1;
            }
            else
            {
                int parens = 0;
                while (i < end)
                {
                    char c = s[i];
                    if (c == '\\' && i + 1 < end) { sb.Append(s[i + 1]); i += 2; continue; }
                    if (c == '(') { parens++; sb.Append(c); i++; continue; }
                    if (c == ')')
                    {
                        if (parens == 0) break;
                        parens--; sb.Append(c); i++; continue;
                    }
                    if (c == ' ' || c == '\t' || c == '\n') break;
                    sb.Append(c); i++;
                }
            }
            url = sb.ToString();

            int beforeTitle = i;
            while (i < end && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n')) i++;
            if (i < end && (s[i] == '"' || s[i] == '\'' || s[i] == '('))
            {
                char q = s[i] == '(' ? ')' : s[i];
                int qEnd = s.IndexOf(q, i + 1);
                if (qEnd < 0 || qEnd >= end) return false;
                title = s.Substring(i + 1, qEnd - i - 1);
                i = qEnd + 1;
                while (i < end && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n')) i++;
            }
            else
            {
                i = beforeTitle;
            }
            if (i >= end || s[i] != ')') return false;
            after = i + 1;
            return true;
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
    }
}
