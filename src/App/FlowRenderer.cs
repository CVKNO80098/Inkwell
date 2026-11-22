using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Inkwell.Core;

namespace Inkwell.App;

/// <summary>Renders the Markdown AST into a native FlowDocument for preview, print and PDF export.</summary>
public sealed class FlowRenderer
{
    public Theme Theme { get; set; } = Theme.Paper();
    public string FontName { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 16.5;
    public string BaseDir { get; set; } = "";
    public Action<string> LinkAction { get; set; }
    public bool ForPrint { get; set; }

    public FlowDocument Build(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(ForPrint ? 0 : 48, ForPrint ? 0 : 30, ForPrint ? 0 : 48, ForPrint ? 0 : 120),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = Fonts.Resolve(FontName),
            FontSize = FontSize,
            Foreground = Theme.FgBrush,
            Background = ForPrint ? Brushes.White : Brushes.Transparent,
            TextAlignment = TextAlignment.Left,
            IsHyphenationEnabled = false
        };
        if (ForPrint)
        {
            doc.FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            doc.Foreground = Brushes.Black;
            doc.PagePadding = new Thickness(0);
            doc.ColumnWidth = 680;
        }

        MdDocument parsed;
        try { parsed = MarkdownParser.Parse(markdown ?? ""); }
        catch { parsed = new MdDocument(); }
        AddBlocks(parsed.Blocks, doc.Blocks, 0);
        return doc;
    }

    void AddBlocks(List<MdBlock> blocks, BlockCollection target, int quoteDepth)
    {
        if (blocks == null) return;
        foreach (var b in blocks)
        {
            var el = RenderBlock(b, quoteDepth);
            if (el != null) target.Add(el);
        }
    }

    void AddBlocksToSection(List<MdBlock> blocks, Section section, int quoteDepth)
    {
        if (blocks == null) return;
        foreach (var b in blocks)
        {
            var el = RenderBlock(b, quoteDepth);
            if (el != null) section.Blocks.Add(el);
        }
    }

    Block RenderBlock(MdBlock block, int quoteDepth)
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                double size = Math.Round(FontSize * HeadingFactor(h.Level));
                var p = new Paragraph
                {
                    FontSize = size,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, h.Level <= 2 ? 28 : 20, 0, h.Level <= 2 ? 12 : 8),
                    LineHeight = Math.Round(size * 1.4),
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                AddInlines(p.Inlines, h.Inlines);
                DecorateQuote(p, quoteDepth);
                return p;
            }
            case ParagraphBlock para:
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 12),
                    LineHeight = Math.Round(FontSize * 1.75),
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                AddInlines(p.Inlines, para.Inlines);
                DecorateQuote(p, quoteDepth);
                return p;
            }
            case CodeBlock code:
            {
                var p = new Paragraph
                {
                    FontFamily = Fonts.Mono,
                    FontSize = Math.Round(FontSize * 0.9),
                    Background = ForPrint ? Brushes.WhiteSmoke : Theme.CodeBgBrush,
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 4, 0, 16),
                    BorderThickness = new Thickness(1),
                    BorderBrush = ForPrint ? Brushes.Gainsboro : Theme.BorderBrush,
                    LineHeight = Math.Round(FontSize * 0.9 * 1.55),
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                string text = (code.Text ?? "").Replace("\r\n", "\n").TrimEnd('\n');
                var runs = CodeHighlighter.Highlight(text, code.Lang, Theme);
                foreach (var r in runs) p.Inlines.Add(r);
                if (p.Inlines.Count == 0) p.Inlines.Add(new Run(""));
                DecorateQuote(p, quoteDepth);
                return p;
            }
            case QuoteBlock quote:
            {
                var section = new Section { Margin = new Thickness(quoteDepth >= 1 ? 20 : 0, 0, 0, 0) };
                AddBlocksToSection(quote.Blocks, section, quoteDepth + 1);
                bool first = true;
                foreach (var b in new List<Block>(section.Blocks))
                {
                    if (b is Paragraph empty && string.IsNullOrWhiteSpace(
                            new TextRange(empty.ContentStart, empty.ContentEnd).Text))
                    {
                        section.Blocks.Remove(empty);
                        continue;
                    }
                    if (b is Paragraph p)
                    {
                        p.BorderThickness = new Thickness(3, 0, 0, 0);
                        p.BorderBrush = ForPrint ? Brushes.Gray : Theme.QuoteBarBrush;
                        p.Padding = new Thickness(14, 0, 0, 0);
                        if (ForPrint) p.Foreground = Brushes.DimGray; else p.Foreground = Theme.MutedBrush;
                        if (first) p.Margin = new Thickness(0, 6, 0, 12);
                    }
                    else if (b is List list)
                    {
                        list.Margin = new Thickness(38, 0, 0, 12);
                    }
                    else if (b is Table table)
                    {
                        table.Margin = new Thickness(18, 0, 0, 12);
                    }
                    first = false;
                }
                return section;
            }
            case ListBlock listBlock:
            {
                var list = new List
                {
                    MarkerStyle = listBlock.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    StartIndex = listBlock.Start,
                    Margin = new Thickness(26, 0, 0, listBlock.Tight ? 10 : 14),
                    Padding = new Thickness(0)
                };
                bool anyTask = false;
                foreach (var it in listBlock.Items) if (it.Task.HasValue) { anyTask = true; break; }
                if (anyTask) list.MarkerStyle = TextMarkerStyle.None;
                foreach (var item in listBlock.Items)
                {
                    var li = new ListItem { Margin = new Thickness(0, 2, 0, 4) };
                    bool task = item.Task.HasValue;
                    bool firstBlock = true;
                    foreach (var child in item.Blocks)
                    {
                        var el = RenderBlock(child, quoteDepth);
                        if (el == null) continue;
                        if (firstBlock && task && el is Paragraph p)
                        {
                            bool done = item.Task.Value;
                            var glyph = new Run(done ? "☑  " : "☐  ")
                            {
                                Foreground = ForPrint ? Brushes.Black : Theme.AccentBrush,
                                FontSize = FontSize * 1.02
                            };
                            p.Inlines.InsertBefore(p.Inlines.FirstInline, glyph);
                        }
                        li.Blocks.Add(el);
                        firstBlock = false;
                    }
                    if (li.Blocks.Count == 0)
                    {
                        var p = new Paragraph();
                        if (task)
                            p.Inlines.Add(new Run(item.Task.Value ? "☑  " : "☐  ") { Foreground = Theme.AccentBrush });
                        li.Blocks.Add(p);
                    }
                    list.ListItems.Add(li);
                }
                return list;
            }
            case TableBlock tableBlock:
            {
                int cols = tableBlock.Header.Count;
                if (cols == 0) return null;
                var table = new Table
                {
                    CellSpacing = 0,
                    Margin = new Thickness(0, 4, 0, 18),
                    BorderThickness = new Thickness(1, 1, 0, 0),
                    BorderBrush = ForPrint ? Brushes.Gainsboro : Theme.BorderBrush
                };
                for (int c = 0; c < cols; c++) table.Columns.Add(new TableColumn());
                var group = new TableRowGroup();
                table.RowGroups.Add(group);

                var headerRow = new TableRow { Background = ForPrint ? Brushes.WhiteSmoke : Theme.TableHeaderBrush };
                for (int c = 0; c < cols; c++)
                {
                    var cell = NewCell();
                    var p = new Paragraph { Margin = new Thickness(0), FontWeight = FontWeights.Bold };
                    AddInlines(p.Inlines, c < tableBlock.Header.Count ? tableBlock.Header[c] : new List<MdInline>(), true);
                    p.TextAlignment = AlignOf(tableBlock.Aligns, c);
                    cell.Blocks.Add(p);
                    headerRow.Cells.Add(cell);
                }
                group.Rows.Add(headerRow);

                foreach (var row in tableBlock.Rows)
                {
                    var tr = new TableRow();
                    for (int c = 0; c < cols; c++)
                    {
                        var cell = NewCell();
                        var p = new Paragraph { Margin = new Thickness(0) };
                        AddInlines(p.Inlines, c < row.Count ? row[c] : new List<MdInline>(), false);
                        p.TextAlignment = AlignOf(tableBlock.Aligns, c);
                        cell.Blocks.Add(p);
                        tr.Cells.Add(cell);
                    }
                    group.Rows.Add(tr);
                }
                return table;
            }
            case ThematicBreakBlock _:
            {
                return new Paragraph
                {
                    Margin = new Thickness(0, 20, 0, 20),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = ForPrint ? Brushes.Gray : Theme.BorderBrush,
                    FontSize = 1,
                    LineHeight = 1
                };
            }
            case HtmlBlock html:
            {
                var p = new Paragraph
                {
                    FontFamily = Fonts.Mono,
                    FontSize = Math.Round(FontSize * 0.85),
                    Foreground = ForPrint ? Brushes.DimGray : Theme.MutedBrush,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                p.Inlines.Add(new Run(html.Text ?? ""));
                return p;
            }
            default:
                return null;
        }
    }

    TableCell NewCell()
    {
        return new TableCell
        {
            Padding = new Thickness(10, 7, 10, 7),
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = ForPrint ? Brushes.Gainsboro : Theme.BorderBrush,
            FontSize = Math.Round(FontSize * 0.95)
        };
    }

    static TextAlignment AlignOf(List<TableAlign> aligns, int col)
    {
        if (aligns != null && col < aligns.Count)
        {
            switch (aligns[col])
            {
                case TableAlign.Center: return TextAlignment.Center;
                case TableAlign.Right: return TextAlignment.Right;
            }
        }
        return TextAlignment.Left;
    }

    void DecorateQuote(Paragraph p, int quoteDepth)
    {
        if (quoteDepth <= 0) return;
        p.Foreground = ForPrint ? Brushes.DimGray : Theme.MutedBrush;
        p.BorderThickness = new Thickness(3, 0, 0, 0);
        p.BorderBrush = ForPrint ? Brushes.Gray : Theme.QuoteBarBrush;
        p.Padding = new Thickness(14, 0, 0, 0);
    }

    static double HeadingFactor(int level)
    {
        switch (level)
        {
            case 1: return 1.9;
            case 2: return 1.55;
            case 3: return 1.3;
            case 4: return 1.16;
            case 5: return 1.05;
            default: return 1.0;
        }
    }

    void AddInlines(InlineCollection target, List<MdInline> nodes, bool bold = false, bool italic = false, bool strike = false, bool underline = false)
    {
        if (nodes == null) return;
        foreach (var node in nodes) AddInline(target, node, bold, italic, strike, underline);
    }

    void AddInline(InlineCollection target, MdInline node, bool bold, bool italic, bool strike, bool underline)
    {
        switch (node)
        {
            case TextInline t:
            {
                var run = new Run(t.Text ?? "");
                if (bold) run.FontWeight = FontWeights.Bold;
                if (italic) run.FontStyle = FontStyles.Italic;
                if (strike) run.TextDecorations = TextDecorations.Strikethrough;
                target.Add(run);
                break;
            }
            case CodeInline c:
            {
                var run = new Run(c.Text ?? "")
                {
                    FontFamily = Fonts.Mono,
                    FontSize = Math.Round(FontSize * 0.88),
                    Background = ForPrint ? Brushes.WhiteSmoke : Theme.CodeBgBrush
                };
                if (bold) run.FontWeight = FontWeights.Bold;
                target.Add(run);
                break;
            }
            case EmphasisInline e:
            {
                var span = new Span();
                bool isItalic = italic || e.Italic;
                if (e.Strong || bold) span.FontWeight = FontWeights.Bold;
                if (e.Strike || strike) span.TextDecorations = TextDecorations.Strikethrough;
                if (!e.Strong && e.Mark) span.Background = ForPrint ? Brushes.LightYellow : Theme.AccentSoftBrush;
                if (isItalic) span.FontStyle = FontStyles.Italic;
                AddInlines(span.Inlines, e.Children, bold || e.Strong, isItalic, strike || e.Strike, underline);
                target.Add(span);
                break;
            }
            case LinkInline link:
            {
                var hl = new Hyperlink
                {
                    Foreground = ForPrint ? Brushes.Black : Theme.LinkBrush,
                    TextDecorations = TextDecorations.Underline
                };
                try
                {
                    if (!string.IsNullOrWhiteSpace(link.Url))
                        hl.NavigateUri = new Uri(link.Url, UriKind.RelativeOrAbsolute);
                }
                catch { }
                hl.RequestNavigate += (s, e) => { e.Handled = true; LinkAction?.Invoke(link.Url); };
                AddInlines(hl.Inlines, link.Children, bold, italic, strike, true);
                if (hl.Inlines.Count == 0) hl.Inlines.Add(new Run(link.Url ?? ""));
                target.Add(hl);
                break;
            }
            case ImageInline img:
            {
                var image = TryLoadImage(img.Url);
                if (image != null)
                {
                    image.MaxWidth = 800;
                    image.Stretch = Stretch.Uniform;
                    image.HorizontalAlignment = HorizontalAlignment.Left;
                    var container = new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center };
                    target.Add(container);
                }
                else
                {
                    var run = new Run(string.IsNullOrEmpty(img.Alt) ? "🖼 " + (img.Url ?? "") : "🖼 " + img.Alt)
                    {
                        Foreground = ForPrint ? Brushes.DimGray : Theme.MutedBrush,
                        Background = ForPrint ? Brushes.WhiteSmoke : Theme.CodeBgBrush,
                        FontSize = Math.Round(FontSize * 0.85)
                    };
                    target.Add(run);
                }
                break;
            }
            case BreakInline _:
                target.Add(new LineBreak());
                break;
            case HtmlInline html:
                target.Add(new Run(html.Text ?? "") { Foreground = ForPrint ? Brushes.DimGray : Theme.MutedBrush });
                break;
        }
    }

    Image TryLoadImage(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string path = url;
            if (!Path.IsPathRooted(path))
            {
                if (string.IsNullOrEmpty(BaseDir)) return null;
                path = Path.Combine(BaseDir, url.Replace('/', '\\'));
            }
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return new Image { Source = bmp, Stretch = Stretch.Uniform };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Small syntax colorizer used by editor and preview code blocks.</summary>
public static class CodeHighlighter
{
    static readonly Dictionary<string, HashSet<string>> Keywords = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = Set("using namespace class struct interface enum record public private protected internal static readonly const void var new return if else switch case for foreach while do break continue try catch finally throw this base null true false async await get set init where out ref in is as typeof nameof yield lock params virtual override abstract sealed partial event delegate operator checked unchecked default goto sizeof stackalloc fixed unsafe extern volatile"),
        ["cs"] = Set("using namespace class struct interface enum record public private protected internal static readonly const void var new return if else switch case for foreach while do break continue try catch finally throw this base null true false async await get set init where out ref in is as typeof nameof yield lock params virtual override abstract sealed partial event delegate operator checked unchecked default goto sizeof stackalloc fixed unsafe extern volatile"),
        ["java"] = Set("package import class interface enum extends implements public private protected static final void new return if else switch case for while do break continue try catch finally throw this super null true false abstract synchronized volatile transient native instanceof record sealed permits var"),
        ["javascript"] = Set("import export from default class extends function const let var return if else switch case for while do break continue try catch finally throw new this super null true false undefined async await yield typeof instanceof of in delete void static get set"),
        ["js"] = Set("import export from default class extends function const let var return if else switch case for while do break continue try catch finally throw new this super null true false undefined async await yield typeof instanceof of in delete void static get set"),
        ["typescript"] = Set("import export from default class extends implements interface type enum namespace declare public private protected readonly static abstract function const let var return if else switch case for while do break continue try catch finally throw new this super null true false undefined async await yield typeof instanceof of in delete void as satisfies keyof infer is asserts override"),
        ["ts"] = Set("import export from default class extends implements interface type enum namespace declare public private protected readonly static abstract function const let var return if else switch case for while do break continue try catch finally throw new this super null true false undefined async await yield typeof instanceof of in delete void as satisfies keyof infer is asserts override"),
        ["python"] = Set("import from as def class return if elif else for while break continue try except finally raise with lambda pass yield global nonlocal assert del in is not and or None True False async await match case self"),
        ["py"] = Set("import from as def class return if elif else for while break continue try except finally raise with lambda pass yield global nonlocal assert del in is not and or None True False async await match case self"),
        ["go"] = Set("package import func var const type struct interface map chan go defer select range return if else switch case for break continue fallthrough goto nil true false make new len cap append panic recover"),
        ["rust"] = Set("use mod fn struct enum trait impl pub crate self super let mut const static return if else match loop while for in break continue unsafe async await move ref where type dyn true false Some None Ok Err"),
        ["json"] = Set("true false null"),
        ["sql"] = Set("select from where insert into values update set delete create table drop alter join inner left right outer full cross on group by order having limit offset as and or not null primary key foreign references index unique distinct union all case when then else end exists between like in is asc desc default constraint view trigger procedure function begin commit rollback transaction with"),
        ["bash"] = Set("if then else elif fi for while until do done case esac function in return exit local export readonly unset echo cd ls cat grep sed awk chmod chown mkdir rm cp mv source alias set"),
        ["sh"] = Set("if then else elif fi for while until do done case esac function in return exit local export readonly unset echo cd ls cat grep sed awk chmod chown mkdir rm cp mv source alias set"),
        ["yaml"] = Set("true false null yes no on off"),
        ["yml"] = Set("true false null yes no on off"),
        ["toml"] = Set("true false"),
        ["dockerfile"] = Set("from run cmd entrypoint copy add env expose volume workdir user arg label onbuild shell"),
        ["css"] = Set("important media supports import keyframes from to and not only"),
        ["scss"] = Set("important media supports import keyframes from to and not only mixin include extend if else each for while function return"),
        ["html"] = Set(""),
        ["xml"] = Set(""),
        ["svg"] = Set("")
    };

    static readonly Dictionary<string, HashSet<string>> Types = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = Set("string int long short byte sbyte uint ulong ushort double float decimal bool char object dynamic void task list dictionary ienumerable iqueryable datetime timespan guid uri stream exception action func nullable span memory"),
        ["cs"] = Set("string int long short byte sbyte uint ulong ushort double float decimal bool char object dynamic void task list dictionary ienumerable iqueryable datetime timespan guid uri stream exception action func nullable span memory"),
        ["java"] = Set("String int long short byte char double float boolean void Object List Map Set Integer Long Double Float Boolean Character Byte Short"),
        ["javascript"] = Set("string number boolean object any unknown never void Array Object Promise Map Set Date RegExp Error"),
        ["js"] = Set("string number boolean object any unknown never void Array Object Promise Map Set Date RegExp Error"),
        ["typescript"] = Set("string number boolean object any unknown never void Array Object Promise Map Set Record Partial Readonly Date RegExp Error"),
        ["ts"] = Set("string number boolean object any unknown never void Array Object Promise Map Set Record Partial Readonly Date RegExp Error"),
        ["python"] = Set("str int float bool list dict set tuple bytes object type Exception None"),
        ["py"] = Set("str int float bool list dict set tuple bytes object type Exception None"),
        ["go"] = Set("string int int8 int16 int32 int64 uint8 uint16 uint32 uint64 float32 float64 bool byte rune error any"),
        ["rust"] = Set("str String i8 i16 i32 i64 i128 u8 u16 u32 u64 u128 f32 f64 bool char Vec Option Result Box Rc Arc HashMap"),
        ["sql"] = Set("int integer bigint smallint decimal numeric float double varchar nvarchar char text date datetime timestamp boolean blob json uuid"),
        ["css"] = Set("px em rem vh vw percent auto none block flex grid inline absolute relative fixed sticky"),
        ["scss"] = Set("px em rem vh vw percent auto none block flex grid inline absolute relative fixed sticky")
    };

    static HashSet<string> Set(string words)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) set.Add(w);
        return set;
    }

    static string NormalizeLang(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "";
        string l = lang.Trim().ToLowerInvariant();
        int space = l.IndexOf(' ');
        if (space > 0) l = l.Substring(0, space);
        switch (l)
        {
            case "c#": return "csharp";
            case "c++": return "cpp";
            case "js": return "javascript";
            case "jsx": return "javascript";
            case "ts": return "typescript";
            case "tsx": return "typescript";
            case "py": return "python";
            case "yml": return "yaml";
            case "htm": return "html";
            case "xhtml": return "html";
            case "rs": return "rust";
            case "golang": return "go";
            case "shell": return "bash";
            case "console": return "bash";
            case "postgresql": return "sql";
            case "mysql": return "sql";
            case "sqlite": return "sql";
            default: return l;
        }
    }

    static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';
    static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    static bool IsMarkup(string id) => id == "html" || id == "xml" || id == "svg" || id == "xaml" || id == "vue" || id == "razor";

    public static List<Run> Highlight(string text, string lang, Theme theme)
    {
        string id = NormalizeLang(lang);
        if (IsMarkup(id)) return HighlightMarkup(text, theme);
        return HighlightCode(text, id, theme);
    }

    static void Add(List<Run> runs, string s, Brush brush, bool italic = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        var r = new Run(s) { Foreground = brush };
        if (italic) r.FontStyle = FontStyles.Italic;
        runs.Add(r);
    }

    static List<Run> HighlightCode(string text, string id, Theme theme)
    {
        var runs = new List<Run>();
        Keywords.TryGetValue(id, out var keywords);
        Types.TryGetValue(id, out var types);
        bool hashComments = id == "python" || id == "bash" || id == "sh" || id == "yaml" || id == "yml" || id == "toml" || id == "dockerfile";
        bool dashComments = id == "sql" || id == "lua";
        bool keyColon = id == "json" || id == "yaml" || id == "yml" || id == "toml" || id == "css" || id == "scss" || id == "ini";
        bool caseInsensitive = id == "sql";

        // Words immediately following these keywords get a dedicated colour.
        string nextWord = null;

        int i = 0, n = text.Length;
        var plain = new System.Text.StringBuilder();

        void Flush()
        {
            if (plain.Length == 0) return;
            Add(runs, plain.ToString(), theme.FgBrush);
            plain.Clear();
        }

        while (i < n)
        {
            char c = text[i];

            if (c == '\r' || c == '\n') { plain.Append(c); i++; continue; }

            // preprocessor / directive lines
            if (c == '#' && (id == "csharp" || id == "cs") && (i == 0 || char.IsWhiteSpace(text[i - 1])))
            {
                int end = text.IndexOf('\n', i);
                if (end < 0) end = n;
                Flush();
                Add(runs, text.Substring(i, end - i), theme.CodeAttrBrush);
                i = end;
                continue;
            }

            if (c == '/' && i + 1 < n && text[i + 1] == '/' && id != "json")
            {
                int end = text.IndexOf('\n', i);
                if (end < 0) end = n;
                Flush();
                Add(runs, text.Substring(i, end - i), theme.CodeCommentBrush, true);
                i = end;
                continue;
            }
            if (c == '#' && hashComments)
            {
                int end = text.IndexOf('\n', i);
                if (end < 0) end = n;
                Flush();
                Add(runs, text.Substring(i, end - i), theme.CodeCommentBrush, true);
                i = end;
                continue;
            }
            if (c == '-' && i + 1 < n && text[i + 1] == '-' && dashComments)
            {
                int end = text.IndexOf('\n', i);
                if (end < 0) end = n;
                Flush();
                Add(runs, text.Substring(i, end - i), theme.CodeCommentBrush, true);
                i = end;
                continue;
            }
            if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? n : end + 2;
                Flush();
                Add(runs, text.Substring(i, end - i), theme.CodeCommentBrush, true);
                i = end;
                continue;
            }

            // strings (verbatim / triple quotes / normal)
            if (c == '"' || c == '\'' || c == '`')
            {
                int start = i;
                char q = c;
                int j = i + 1;
                if ((q == '"' || q == '\'') && i + 2 < n && text[i + 1] == q && text[i + 2] == q)
                {
                    j = i + 3;
                    while (j + 2 < n && !(text[j] == q && text[j + 1] == q && text[j + 2] == q))
                    {
                        if (text[j] == '\\') j++;
                        j++;
                    }
                    j = Math.Min(n, j + 3);
                }
                else
                {
                    bool verbatim = q == '"' && i > 0 && text[i - 1] == '@';
                    while (j < n)
                    {
                        if (!verbatim && text[j] == '\\') { j += 2; continue; }
                        if (text[j] == q)
                        {
                            if (verbatim && j + 1 < n && text[j + 1] == q) { j += 2; continue; }
                            j++; break;
                        }
                        j++;
                    }
                }
                Flush();
                bool isKey = keyColon && LooksLikeKey(text, j);
                Add(runs, text.Substring(start, Math.Min(n, j) - start), isKey ? theme.CodeAttrBrush : theme.CodeStringBrush);
                i = Math.Min(n, j);
                continue;
            }

            if (char.IsDigit(c))
            {
                int j = i;
                while (j < n && (char.IsLetterOrDigit(text[j]) || text[j] == '.' || text[j] == '_' || text[j] == 'x' || text[j] == 'X')) j++;
                Flush();
                Add(runs, text.Substring(i, j - i), theme.CodeNumberBrush);
                i = j;
                continue;
            }

            // decorators / attributes / shell variables
            if (c == '@' && i + 1 < n && IsIdentStart(text[i + 1]))
            {
                int j = i + 1;
                while (j < n && IsIdentPart(text[j])) j++;
                Flush();
                Add(runs, text.Substring(i, j - i), theme.CodeAttrBrush);
                i = j;
                continue;
            }
            if (c == '$' && id != "javascript" && i + 1 < n && IsIdentStart(text[i + 1]))
            {
                int j = i + 1;
                while (j < n && IsIdentPart(text[j])) j++;
                Flush();
                Add(runs, text.Substring(i, j - i), theme.CodeAttrBrush);
                i = j;
                continue;
            }

            if (IsIdentStart(c))
            {
                int j = i;
                while (j < n && IsIdentPart(text[j])) j++;
                string word = text.Substring(i, j - i);
                Flush();

                Brush brush = theme.FgBrush;
                bool handled = false;

                if (nextWord != null)
                {
                    brush = nextWord == "type" ? theme.CodeTypeBrush : theme.CodeFunctionBrush;
                    nextWord = null;
                    handled = true;
                }
                else if (types != null && types.Contains(word))
                {
                    brush = theme.CodeTypeBrush;
                }
                else if (keywords != null && keywords.Contains(word))
                {
                    brush = theme.CodeKeywordBrush;
                    string lower = word.ToLowerInvariant();
                    if (lower == "class" || lower == "struct" || lower == "interface" || lower == "enum" || lower == "record" || lower == "trait" || lower == "type")
                        nextWord = "type";
                    else if (lower == "def" || lower == "function" || lower == "fn" || lower == "func" || lower == "sub")
                        nextWord = "func";
                }
                else if (LooksLikeKey(text, j) && keyColon)
                {
                    brush = theme.CodeAttrBrush;
                }
                else if (j < n && text[j] == '(')
                {
                    brush = theme.CodeFunctionBrush;
                }
                else if (char.IsUpper(c) && id != "bash" && id != "sh")
                {
                    brush = theme.CodeTypeBrush;
                }
                else if (word.Length > 2 && word.StartsWith("_") && char.IsUpper(word[1]))
                {
                    brush = theme.CodeAttrBrush;
                }

                if (!handled)
                {
                    // method call after a dot: obj.Method(...)
                    int p = i - 1;
                    while (p >= 0 && char.IsWhiteSpace(text[p])) p--;
                    if (p >= 0 && text[p] == '.' && j < n && text[j] == '(' && brush == theme.FgBrush)
                        brush = theme.CodeFunctionBrush;
                }

                Add(runs, word, brush, false);
                i = j;
                continue;
            }

            // markup-ish angle brackets inside generic code: keep punctuation colour
            if ("()[]{};,.<>=+-*/%&|^!~?:".IndexOf(c) >= 0)
            {
                Flush();
                Add(runs, c.ToString(), theme.CodePunctBrush);
                i++;
                continue;
            }

            plain.Append(c);
            i++;
        }
        Flush();
        return runs;
    }

    static bool LooksLikeKey(string text, int after)
    {
        int k = after;
        while (k < text.Length && (text[k] == ' ' || text[k] == '\t')) k++;
        return k < text.Length && text[k] == ':';
    }

    static List<Run> HighlightMarkup(string text, Theme theme)
    {
        var runs = new List<Run>();
        int i = 0, n = text.Length;
        var plain = new System.Text.StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            Add(runs, plain.ToString(), theme.FgBrush);
            plain.Clear();
        }
        while (i < n)
        {
            char c = text[i];
            if (c == '<' && i + 3 < n && text.Substring(i, 4) == "<!--")
            {
                int end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                end = end < 0 ? n : end + 3;
                Flush(); Add(runs, text.Substring(i, end - i), theme.CodeCommentBrush, true); i = end; continue;
            }
            if (c == '<')
            {
                Flush();
                int j = i + 1;
                if (j < n && text[j] == '/') { Add(runs, "</", theme.CodeTagBrush); j++; }
                else if (j < n && text[j] == '!' && j + 1 < n && text[j + 1] == '-') { }
                else Add(runs, "<", theme.CodeTagBrush);
                if (j < n && text[j] == '!')
                {
                    int e = text.IndexOf('>', j);
                    e = e < 0 ? n : e + 1;
                    Add(runs, text.Substring(j, e - j), theme.CodeTagBrush);
                    i = e; continue;
                }
                int nameStart = j;
                while (j < n && (char.IsLetterOrDigit(text[j]) || text[j] == ':' || text[j] == '-' || text[j] == '_')) j++;
                if (j > nameStart) Add(runs, text.Substring(nameStart, j - nameStart), theme.CodeTagBrush);
                // attributes until '>'
                while (j < n)
                {
                    if (text[j] == '>') { Add(runs, ">", theme.CodeTagBrush); j++; break; }
                    if (text[j] == '/' && j + 1 < n && text[j + 1] == '>') { Add(runs, "/>", theme.CodeTagBrush); j += 2; break; }
                    if (char.IsWhiteSpace(text[j])) { Add(runs, text[j].ToString(), theme.FgBrush); j++; continue; }
                    if (text[j] == '=')
                    {
                        Add(runs, "=", theme.CodePunctBrush); j++;
                        while (j < n && (text[j] == ' ' || text[j] == '\t')) { Add(runs, text[j].ToString(), theme.FgBrush); j++; }
                        if (j < n && (text[j] == '"' || text[j] == '\''))
                        {
                            char q = text[j]; int k = j + 1;
                            while (k < n && text[k] != q) k++;
                            k = Math.Min(n, k + 1);
                            Add(runs, text.Substring(j, k - j), theme.CodeStringBrush); j = k;
                        }
                        continue;
                    }
                    if (char.IsLetter(text[j]) || text[j] == '_' || text[j] == ':' || text[j] == '-')
                    {
                        int k = j;
                        while (k < n && (char.IsLetterOrDigit(text[k]) || text[k] == '_' || text[k] == ':' || text[k] == '-' || text[k] == '.')) k++;
                        Add(runs, text.Substring(j, k - j), theme.CodeAttrBrush); j = k; continue;
                    }
                    Add(runs, text[j].ToString(), theme.CodePunctBrush); j++;
                }
                i = j; continue;
            }
            plain.Append(c); i++;
        }
        Flush();
        return runs;
    }
}
