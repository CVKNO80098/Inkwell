using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Inkwell.Core;

public sealed class HtmlExportOptions
{
    public bool Dark;
    public string Title = "Untitled";
    public bool Standalone = true;
    public bool EmbedImages;
    public string BaseDirectory;
    public string FontFamily = "'Segoe UI', 'Microsoft YaHei UI', system-ui, sans-serif";
    public string MonoFamily = "'Cascadia Mono', Consolas, 'Microsoft YaHei UI', monospace";
}

public static class HtmlExporter
{
    public static string ToHtml(MdDocument doc, HtmlExportOptions opt)
    {
        var sb = new StringBuilder();
        foreach (var b in doc.Blocks) RenderBlock(sb, b, opt);
        string body = sb.ToString();
        if (!opt.Standalone) return body;
        return Wrap(body, opt);
    }

    static string Wrap(string body, HtmlExportOptions opt)
    {
        bool dark = opt.Dark;
        string bg = dark ? "#1b1b1d" : "#faf9f6";
        string panel = dark ? "#242427" : "#ffffff";
        string fg = dark ? "#d9d6d0" : "#26221d";
        string muted = dark ? "#8f8b84" : "#7a746b";
        string line = dark ? "#33333a" : "#e8e3da";
        string accent = dark ? "#e08a6d" : "#b0522f";
        string codeBg = dark ? "#202024" : "#f3efe7";
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Esc(opt.Title)).Append("</title>\n<style>\n");
        sb.Append(":root{color-scheme:").Append(dark ? "dark" : "light").Append("}\n");
        sb.Append("*{box-sizing:border-box}\n");
        sb.Append("body{margin:0;background:").Append(bg).Append(";color:").Append(fg)
          .Append(";font-family:").Append(opt.FontFamily).Append(";font-size:16px;line-height:1.75;-webkit-font-smoothing:antialiased}\n");
        sb.Append(".md{max-width:760px;margin:0 auto;padding:64px 40px 120px}\n");
        sb.Append(".md h1,.md h2,.md h3,.md h4,.md h5,.md h6{line-height:1.3;margin:1.9em 0 .65em;font-weight:650;letter-spacing:-.01em}\n");
        sb.Append(".md h1{font-size:2.05em;margin-top:0}\n.md h2{font-size:1.55em;border-bottom:1px solid ").Append(line).Append(";padding-bottom:.25em}\n");
        sb.Append(".md h3{font-size:1.28em}\n.md h4{font-size:1.1em}\n.md h5{font-size:1em}\n.md h6{font-size:.94em;color:").Append(muted).Append("}\n");
        sb.Append(".md p{margin:0 0 1em}\n.md a{color:").Append(accent).Append(";text-decoration:none;border-bottom:1px solid ").Append(dark ? "#e08a6d55" : "#b0522f44").Append("}\n");
        sb.Append(".md strong{font-weight:670}\n.md em{font-style:italic}\n");
        sb.Append(".md code{font-family:").Append(opt.MonoFamily).Append(";font-size:.86em;background:").Append(codeBg).Append(";padding:.15em .35em;border-radius:4px}\n");
        sb.Append(".md pre{background:").Append(codeBg).Append(";border:1px solid ").Append(line).Append(";border-radius:8px;padding:16px 18px;overflow:auto;margin:0 0 1.2em}\n");
        sb.Append(".md pre code{background:none;padding:0;font-size:.84em;line-height:1.6}\n");
        sb.Append(".md blockquote{margin:0 0 1.1em;padding:.1em 0 .1em 18px;border-left:3px solid ").Append(accent).Append(";color:").Append(muted).Append("}\n");
        sb.Append(".md blockquote p:last-child{margin-bottom:0}\n");
        sb.Append(".md ul,.md ol{margin:0 0 1em;padding-left:1.6em}\n.md li{margin:.15em 0}\n.md li>p{margin:0}\n");
        sb.Append(".md li.task{list-style:none;margin-left:-1.25em}\n.md li.task input{margin-right:.5em;accent-color:").Append(accent).Append("}\n");
        sb.Append(".md hr{border:none;border-top:1px solid ").Append(line).Append(";margin:2.2em 0}\n");
        sb.Append(".md table{border-collapse:collapse;width:100%;margin:0 0 1.2em;font-size:.94em}\n");
        sb.Append(".md th,.md td{border:1px solid ").Append(line).Append(";padding:7px 12px;text-align:left}\n");
        sb.Append(".md th{background:").Append(panel).Append(";font-weight:640}\n.md tr:nth-child(even) td{background:").Append(dark ? "#202023" : "#fbf9f5").Append("}\n");
        sb.Append(".md img{max-width:100%;height:auto;border-radius:6px}\n");
        sb.Append(".md mark{background:").Append(dark ? "#6b5a2a" : "#f7e6a1").Append(";color:inherit;border-radius:3px;padding:0 .15em}\n");
        sb.Append(".md del{color:").Append(muted).Append("}\n");
        sb.Append(".md .htmlraw{font-family:").Append(opt.MonoFamily).Append(";font-size:.82em;color:").Append(muted).Append(";white-space:pre-wrap}\n");
        sb.Append("</style>\n</head>\n<body>\n<article class=\"md\">\n").Append(body).Append("\n</article>\n</body>\n</html>\n");
        return sb.ToString();
    }

    static void RenderBlock(StringBuilder sb, MdBlock block, HtmlExportOptions opt)
    {
        switch (block)
        {
            case HeadingBlock h:
                sb.Append("<h").Append(h.Level).Append('>');
                RenderInlines(sb, h.Inlines, opt);
                sb.Append("</h").Append(h.Level).Append(">\n");
                break;

            case ParagraphBlock p:
                sb.Append("<p>");
                RenderInlines(sb, p.Inlines, opt);
                sb.Append("</p>\n");
                break;

            case CodeBlock c:
                sb.Append("<pre><code");
                if (!string.IsNullOrEmpty(c.Lang)) sb.Append(" class=\"language-").Append(Esc(c.Lang)).Append('"');
                sb.Append(">").Append(Esc(c.Text)).Append("</code></pre>\n");
                break;

            case QuoteBlock q:
                sb.Append("<blockquote>\n");
                foreach (var b in q.Blocks) RenderBlock(sb, b, opt);
                sb.Append("</blockquote>\n");
                break;

            case ListBlock l:
                sb.Append(l.Ordered ? "<ol" : "<ul");
                if (l.Ordered && l.Start != 1) sb.Append(" start=\"").Append(l.Start).Append('"');
                sb.Append(">\n");
                foreach (var item in l.Items) RenderItem(sb, item, opt, l.Tight);
                sb.Append(l.Ordered ? "</ol>\n" : "</ul>\n");
                break;

            case TableBlock t:
                sb.Append("<table>\n<thead><tr>");
                for (int i = 0; i < t.Header.Count; i++)
                {
                    sb.Append("<th").Append(AlignAttr(t, i)).Append('>');
                    RenderInlines(sb, t.Header[i], opt);
                    sb.Append("</th>");
                }
                sb.Append("</tr></thead>\n<tbody>\n");
                foreach (var row in t.Rows)
                {
                    sb.Append("<tr>");
                    for (int i = 0; i < t.Header.Count; i++)
                    {
                        sb.Append("<td").Append(AlignAttr(t, i)).Append('>');
                        RenderInlines(sb, i < row.Count ? row[i] : new List<MdInline>(), opt);
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>\n");
                }
                sb.Append("</tbody></table>\n");
                break;

            case ThematicBreakBlock:
                sb.Append("<hr>\n");
                break;

            case HtmlBlock:
                sb.Append("<div class=\"htmlraw\">").Append(Esc(((HtmlBlock)block).Text)).Append("</div>\n");
                break;
        }
    }

    static string AlignAttr(TableBlock t, int i)
    {
        if (i >= t.Aligns.Count) return "";
        switch (t.Aligns[i])
        {
            case TableAlign.Center: return " style=\"text-align:center\"";
            case TableAlign.Right: return " style=\"text-align:right\"";
            default: return "";
        }
    }

    static void RenderItem(StringBuilder sb, ListItemBlock item, HtmlExportOptions opt, bool tight)
    {
        sb.Append("<li");
        if (item.Task.HasValue) sb.Append(" class=\"task\"");
        sb.Append('>');
        if (item.Task.HasValue)
            sb.Append("<input type=\"checkbox\" disabled").Append(item.Task.Value ? " checked" : "").Append('>');
        bool first = true;
        foreach (var b in item.Blocks)
        {
            if (b is ParagraphBlock p && tight)
            {
                if (!first) sb.Append('\n');
                RenderInlines(sb, p.Inlines, opt);
                continue;
            }
            sb.Append('\n');
            RenderBlock(sb, b, opt);
        }
        sb.Append("</li>\n");
    }

    public static void RenderInlines(StringBuilder sb, List<MdInline> nodes, HtmlExportOptions opt)
    {
        foreach (var n in nodes)
        {
            switch (n)
            {
                case TextInline t: sb.Append(Esc(t.Text)); break;
                case CodeInline c: sb.Append("<code>").Append(Esc(c.Text)).Append("</code>"); break;
                case EmphasisInline e:
                    if (e.Strong) sb.Append("<strong>");
                    if (e.Italic) sb.Append("<em>");
                    if (e.Strike) sb.Append("<del>");
                    if (e.Mark) sb.Append("<mark>");
                    RenderInlines(sb, e.Children, opt);
                    if (e.Mark) sb.Append("</mark>");
                    if (e.Strike) sb.Append("</del>");
                    if (e.Italic) sb.Append("</em>");
                    if (e.Strong) sb.Append("</strong>");
                    break;
                case LinkInline l:
                    sb.Append("<a href=\"").Append(Esc(l.Url)).Append('"');
                    if (!string.IsNullOrEmpty(l.Title)) sb.Append(" title=\"").Append(Esc(l.Title)).Append('"');
                    sb.Append('>');
                    RenderInlines(sb, l.Children, opt);
                    sb.Append("</a>");
                    break;
                case ImageInline img:
                    sb.Append("<img src=\"").Append(Esc(ImageSource(img.Url, opt))).Append("\" alt=\"").Append(Esc(img.Alt)).Append('"');
                    if (!string.IsNullOrEmpty(img.Title)) sb.Append(" title=\"").Append(Esc(img.Title)).Append('"');
                    sb.Append('>');
                    break;
                case BreakInline br:
                    sb.Append(br.Hard ? "<br>\n" : "\n");
                    break;
                case HtmlInline h:
                    sb.Append(h.Text);
                    break;
            }
        }
    }

    static string ImageSource(string url, HtmlExportOptions opt)
    {
        if (!opt.EmbedImages || string.IsNullOrEmpty(opt.BaseDirectory)) return url;
        try
        {
            string path = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.IsFile) path = u.LocalPath;
            if (!Path.IsPathRooted(path)) path = Path.Combine(opt.BaseDirectory, path);
            if (!File.Exists(path)) return url;
            string mime = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch { return url; }
    }

    public static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
