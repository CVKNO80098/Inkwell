using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Inkwell.App;

public static class TextUtil
{
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    public static string[] SplitLines(string s) => Normalize(s).Split('\n');

    public static int CountWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int words = 0;
        bool inWord = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (IsCjk(c))
            {
                if (inWord) { inWord = false; }
                words++;
                continue;
            }
            if (char.IsLetterOrDigit(c) || c == '_' || c == '\'' || c == '-')
            {
                if (!inWord) { words++; inWord = true; }
            }
            else
            {
                inWord = false;
            }
        }
        return words;
    }

    public static int CountCjk(string text)
    {
        int n = 0;
        foreach (char c in text) if (IsCjk(c)) n++;
        return n;
    }

    public static bool IsCjk(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xF900 && c <= 0xFAFF)
            || (c >= 0x3040 && c <= 0x30FF) || (c >= 0xAC00 && c <= 0xD7AF) || (c >= 0x3000 && c <= 0x303F);
    }

    public static string HumanSize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
        return (bytes / 1024.0 / 1024.0).ToString("0.#") + " MB";
    }

    public static string LineEndingOf(string text)
    {
        return text.Contains("\r\n") ? "\r\n" : "\n";
    }

    /// <summary>Ensure every line in the range starts with the given prefix (skipping blank lines).</summary>
    public static string AddPrefixToLines(string text, int firstLine, int lastLine, string prefix)
    {
        var lines = SplitLines(text);
        for (int i = firstLine; i <= lastLine && i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            lines[i] = prefix + lines[i];
        }
        return string.Join("\n", lines);
    }

    public static string ToggleLinePrefix(string text, int firstLine, int lastLine, string[] prefixes)
    {
        var lines = SplitLines(text);
        bool all = true;
        for (int i = firstLine; i <= lastLine && i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            bool has = false;
            foreach (var p in prefixes)
                if (lines[i].StartsWith(p, StringComparison.Ordinal)) { has = true; break; }
            if (!has) { all = false; break; }
        }
        for (int i = firstLine; i <= lastLine && i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            if (all)
            {
                foreach (var p in prefixes)
                    if (lines[i].StartsWith(p, StringComparison.Ordinal)) { lines[i] = lines[i].Substring(p.Length); break; }
            }
            else
            {
                bool has = false;
                foreach (var p in prefixes)
                    if (lines[i].StartsWith(p, StringComparison.Ordinal)) { has = true; break; }
                if (!has) lines[i] = prefixes[0] + lines[i];
            }
        }
        return string.Join("\n", lines);
    }

    public static string ToggleHeading(string text, int firstLine, int lastLine, int level)
    {
        var lines = SplitLines(text);
        for (int i = firstLine; i <= lastLine && i < lines.Length; i++)
        {
            string line = lines[i];
            int hashes = 0;
            while (hashes < line.Length && line[hashes] == '#') hashes++;
            bool isHeading = hashes >= 1 && hashes <= 6 && hashes < line.Length && line[hashes] == ' ';
            if (isHeading && hashes == level)
            {
                lines[i] = line.Substring(hashes + 1);
            }
            else
            {
                string content = isHeading ? line.Substring(hashes + 1) : line;
                lines[i] = new string('#', level) + " " + content;
            }
        }
        return string.Join("\n", lines);
    }

    public static string RelativePath(string baseDir, string full)
    {
        try
        {
            if (string.IsNullOrEmpty(baseDir)) return full;
            var b = new Uri(baseDir.EndsWith("\\") ? baseDir : baseDir + "\\");
            var f = new Uri(full);
            var rel = b.MakeRelativeUri(f).ToString();
            return Uri.UnescapeDataString(rel).Replace('/', '\\');
        }
        catch { return full; }
    }

    public static bool IsMarkdownFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".md" || ext == ".markdown" || ext == ".mdown" || ext == ".mkd" || ext == ".mdx";
    }

    public static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp" || ext == ".tif" || ext == ".tiff";
    }

    /// <summary>Escape characters for use in a Markdown inline link destination.</summary>
    public static string EscapeUrl(string url)
    {
        var sb = new StringBuilder();
        foreach (char c in url)
        {
            if (c == '(' || c == ')' || c == ' ') sb.Append(Uri.EscapeDataString(c.ToString()));
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
