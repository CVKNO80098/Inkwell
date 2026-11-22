using System.Collections.Generic;

namespace Inkwell.Core;

public enum TableAlign { None, Left, Center, Right }

public abstract class MdBlock
{
    public int Line;      // 1-based first source line
    public int EndLine;   // 1-based last source line
}

public sealed class MdDocument
{
    public List<MdBlock> Blocks = new List<MdBlock>();
    public Dictionary<string, LinkRef> Refs = new Dictionary<string, LinkRef>(System.StringComparer.OrdinalIgnoreCase);
}

public sealed class LinkRef
{
    public string Url;
    public string Title;
}

public sealed class HeadingBlock : MdBlock
{
    public int Level;
    public List<MdInline> Inlines = new List<MdInline>();
}

public sealed class ParagraphBlock : MdBlock
{
    public List<MdInline> Inlines = new List<MdInline>();
}

public sealed class CodeBlock : MdBlock
{
    public string Info = "";
    public string Lang = "";
    public string Text = "";
}

public sealed class QuoteBlock : MdBlock
{
    public List<MdBlock> Blocks = new List<MdBlock>();
}

public sealed class ListBlock : MdBlock
{
    public bool Ordered;
    public int Start = 1;
    public bool Tight = true;
    public List<ListItemBlock> Items = new List<ListItemBlock>();
}

public sealed class ListItemBlock : MdBlock
{
    public bool? Task;              // null = plain item
    public List<MdBlock> Blocks = new List<MdBlock>();
}

public sealed class TableBlock : MdBlock
{
    public List<List<MdInline>> Header = new List<List<MdInline>>();
    public List<TableAlign> Aligns = new List<TableAlign>();
    public List<List<List<MdInline>>> Rows = new List<List<List<MdInline>>>();
}

public sealed class ThematicBreakBlock : MdBlock { }

public sealed class HtmlBlock : MdBlock
{
    public string Text = "";
}

public abstract class MdInline { }

public sealed class TextInline : MdInline
{
    public string Text;
    public TextInline(string t) { Text = t; }
}

public sealed class CodeInline : MdInline
{
    public string Text;
    public CodeInline(string t) { Text = t; }
}

public sealed class EmphasisInline : MdInline
{
    public bool Strong;
    public bool Italic;
    public bool Strike;
    public bool Mark;
    public List<MdInline> Children = new List<MdInline>();
}

public sealed class LinkInline : MdInline
{
    public string Url = "";
    public string Title;
    public List<MdInline> Children = new List<MdInline>();
}

public sealed class ImageInline : MdInline
{
    public string Url = "";
    public string Alt = "";
    public string Title;
}

public sealed class BreakInline : MdInline
{
    public bool Hard;
}

public sealed class HtmlInline : MdInline
{
    public string Text;
    public HtmlInline(string t) { Text = t; }
}
