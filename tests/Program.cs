using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Inkwell.Core;

public static class TestProgram
{
    static int passed, failed;
    static readonly StringBuilder Log = new StringBuilder();

    public static int Main(string[] args)
    {
        Run("headings", Headings);
        Run("emphasis", Emphasis);
        Run("code", Code);
        Run("fences", Fences);
        Run("lists", Lists);
        Run("tasklist", TaskList);
        Run("table", Table);
        Run("quote", Quote);
        Run("links", Links);
        Run("hr", Rule);
        Run("frontmatter", FrontMatter);
        Run("escapes", Escapes);
        Run("mixed", Mixed);

        Console.WriteLine(Log.ToString());
        Console.WriteLine($"\nPASS {passed}  FAIL {failed}");
        return failed == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Log.AppendLine("  ok   " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Log.AppendLine("  FAIL " + name + ": " + ex.Message);
        }
    }

    static void Assert(bool cond, string msg)
    {
        if (!cond) throw new Exception(msg);
    }

    static void Contains(string haystack, string needle)
    {
        if (haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
            throw new Exception("missing <" + needle + "> in:\n" + haystack);
    }

    static string Html(string md)
    {
        var doc = MarkdownParser.Parse(md);
        return HtmlExporter.ToHtml(doc, new HtmlExportOptions { Standalone = false });
    }

    static void Headings()
    {
        string h = Html("# Title\n\n## Sub ##\n\nSetext\n===\n\nSecond\n---\n");
        Contains(h, "<h1>Title</h1>");
        Contains(h, "<h2>Sub</h2>");
        Contains(h, "<h1>Setext</h1>");
        Contains(h, "<h2>Second</h2>");

        var doc = MarkdownParser.Parse("# A\n## B\n");
        Assert(doc.Blocks.Count == 2, "expected 2 blocks, got " + doc.Blocks.Count);
        Assert(((HeadingBlock)doc.Blocks[1]).Level == 2, "level");
    }

    static void Emphasis()
    {
        string h = Html("a **bold** and *em* and `code` and ~~gone~~ and ==hi==\n");
        Contains(h, "<strong>bold</strong>");
        Contains(h, "<em>em</em>");
        Contains(h, "<code>code</code>");
        Contains(h, "<del>gone</del>");
        Contains(h, "<mark>hi</mark>");

        h = Html("**bold with *inner* text**\n");
        Contains(h, "<strong>bold with <em>inner</em> text</strong>");

        h = Html("2*3*4 and snake_case_name\n");
        Contains(h, "2<em>3</em>4");
        Contains(h, "snake_case_name");
    }

    static void Code()
    {
        string h = Html("```csharp\nint x = 1 < 2;\n```\n");
        Contains(h, "language-csharp");
        Contains(h, "int x = 1 &lt; 2;");

        h = Html("    indented code\n    line2\n");
        Contains(h, "<pre><code>indented code\nline2</code></pre>");
    }

    static void Fences()
    {
        string h = Html("```cs\nint a = 1;\n```\n\n```json\n{ \"a\": 1 }\n```\n\nafter\n");
        Contains(h, "<pre><code class=\"language-cs\">int a = 1;");
        Contains(h, "<pre><code class=\"language-json\">{ &quot;a&quot;: 1 }");
        Contains(h, "<p>after</p>");
        Assert(h.IndexOf("```", StringComparison.Ordinal) < 0, "fence leaked into output");
    }
    static void Lists()
    {
        string h = Html("- one\n- two\n  - two.a\n  - two.b\n- three\n");
        Contains(h, "<ul>");
        Contains(h, "<li>one</li>");
        Contains(h, "<li>two\n<ul>");
        Contains(h, "<li>two.a</li>");
        Assert(Count(h, "<ul>") == 2, "expected nested ul, got " + Count(h, "<ul>"));

        h = Html("1. first\n2. second\n");
        Contains(h, "<ol>");
        Contains(h, "<li>first</li>");

        h = Html("- a\n\n- b\n");
        Contains(h, "<p>a</p>");
    }

    static void TaskList()
    {
        var doc = MarkdownParser.Parse("- [ ] todo\n- [x] done\n");
        var list = (ListBlock)doc.Blocks[0];
        Assert(list.Items[0].Task == false, "item0 task false");
        Assert(list.Items[1].Task == true, "item1 task true");
        string h = Html("- [ ] todo\n- [x] done\n");
        Contains(h, "type=\"checkbox\" disabled>");
        Contains(h, "checked");
        Contains(h, "todo");
    }

    static void Table()
    {
        string md = "| Name | Qty |\n|:-----|----:|\n| apple | 3 |\n| pear | 12 |\n";
        var doc = MarkdownParser.Parse(md);
        var t = (TableBlock)doc.Blocks[0];
        Assert(t.Header.Count == 2, "cols");
        Assert(t.Rows.Count == 2, "rows");
        Assert(t.Aligns[1] == TableAlign.Right, "right align");
        string h = Html(md);
        Contains(h, "<th>Name</th>");
        Contains(h, "<td style=\"text-align:right\">12</td>");
    }

    static void Quote()
    {
        string h = Html("> quoted **text**\n>\n> - item\n");
        Contains(h, "<blockquote>");
        Contains(h, "<strong>text</strong>");
        Contains(h, "<li>item</li>");

        h = Html("> outer\n> > inner\n");
        Assert(Count(h, "<blockquote>") == 2, "nested quotes");
    }

    static void Links()
    {
        string h = Html("[site](https://example.com \"Title\") and ![img](pic.png)\n");
        Contains(h, "<a href=\"https://example.com\" title=\"Title\">site</a>");
        Contains(h, "<img src=\"pic.png\" alt=\"img\">");

        h = Html("[ref][id] and <https://auto.link>\n\n[id]: https://ref.example \"R\"\n");
        Contains(h, "<a href=\"https://ref.example\" title=\"R\">ref</a>");
        Contains(h, "<a href=\"https://auto.link\">https://auto.link</a>");

        h = Html("[shortcut]\n\n[shortcut]: https://s.example\n");
        Contains(h, "href=\"https://s.example\">shortcut</a>");
    }

    static void Rule()
    {
        string h = Html("a\n\n---\n\nb\n");
        Contains(h, "<hr>");
        var doc = MarkdownParser.Parse("* * *\n");
        Assert(doc.Blocks[0] is ThematicBreakBlock, "stars rule");
    }

    static void FrontMatter()
    {
        string md = "---\ntitle: Hello\ntags: [a, b]\n---\n\n# Doc\n";
        var doc = MarkdownParser.Parse(md);
        var code = doc.Blocks[0] as CodeBlock;
        Assert(code != null && code.Lang == "yaml", "frontmatter code block");
        Assert(code.Text.Contains("title: Hello"), "frontmatter text");
        Assert(((HeadingBlock)doc.Blocks[1]).Line == 6, "line offset after frontmatter, got " + doc.Blocks[1].Line);
    }

    static void Escapes()
    {
        string h = Html("\\*not em\\* and a \\\\ backslash\n");
        Contains(h, "*not em*");
        Contains(h, "a \\ backslash");

        h = Html("line one  \nline two\n");
        Contains(h, "<br>");
    }

    static void Mixed()
    {
        string md = "# 你好，世界\n\n这是一个 **测试**，包含 `代码`。\n\n1. 第一\n2. 第二\n   - 嵌套\n\n> 引用\n";
        var doc = MarkdownParser.Parse(md);
        Assert(doc.Blocks.Count == 4, "mixed blocks=" + doc.Blocks.Count);
        string h = Html(md);
        Contains(h, "<h1>你好，世界</h1>");
        Contains(h, "<strong>测试</strong>");
    }

    static int Count(string s, string sub)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; }
        return n;
    }
}
