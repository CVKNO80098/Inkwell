using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Inkwell.App;

public sealed class Theme
{
    public string Id;
    public string Title;
    public bool Dark;

    public Color Bg, Surface, SurfaceAlt, SurfaceHover, Fg, FgMuted, Border, BorderStrong,
                 Accent, AccentSoft, CodeBg, Selection, Caret, Link, TableHeader,
                 CodeKeyword, CodeString, CodeComment, CodeNumber, CodeType, CodeFunction,
                 CodeTag, CodeAttr, CodePunct, QuoteBar, Shadow;

    public Brush BgBrush, SurfaceBrush, SurfaceAltBrush, HoverBrush, FgBrush, MutedBrush,
                 BorderBrush, BorderStrongBrush, AccentBrush, AccentSoftBrush, CodeBgBrush,
                 SelectionBrush, CaretBrush, LinkBrush, TableHeaderBrush, ShadowBrush,
                 CodeKeywordBrush, CodeStringBrush, CodeCommentBrush, CodeNumberBrush,
                 CodeTypeBrush, CodeFunctionBrush, CodeTagBrush, CodeAttrBrush, CodePunctBrush,
                 QuoteBarBrush;

    public static readonly string[] Order = { "Paper", "Sepia", "Night" };

    public static Theme ById(string id)
    {
        switch (id)
        {
            case "Sepia": return Sepia();
            case "Night": return Night();
            default: return Paper();
        }
    }

    public static Theme Paper()
    {
        var t = new Theme
        {
            Id = "Paper", Title = "纸张", Dark = false,
            Bg = C(0xF3, 0xEF, 0xE7),
            Surface = C(0xFC, 0xFA, 0xF5),
            SurfaceAlt = C(0xEE, 0xE8, 0xDD),
            SurfaceHover = C(0xE9, 0xE2, 0xD5),
            Fg = C(0x2B, 0x26, 0x22),
            FgMuted = C(0x92, 0x8A, 0x7E),
            Border = C(0xE2, 0xDB, 0xCD),
            BorderStrong = C(0xD0, 0xC7, 0xB6),
            Accent = C(0xB8, 0x53, 0x2E),
            AccentSoft = C(0xF3, 0xDF, 0xD3),
            CodeBg = C(0xF2, 0xEC, 0xE2),
            Selection = C(0xEC, 0xD3, 0xA8),
            Caret = C(0xB8, 0x53, 0x2E),
            Link = C(0x9A, 0x4A, 0x24),
            TableHeader = C(0xF5, 0xF0, 0xE7),
            CodeKeyword = C(0xA8, 0x44, 0x36),
            CodeString = C(0x4F, 0x74, 0x40),
            CodeComment = C(0x9A, 0x94, 0x88),
            CodeNumber = C(0x4B, 0x63, 0x99),
            CodeType = C(0x2F, 0x6B, 0x7A),
            CodeFunction = C(0x7A, 0x4E, 0x8C),
            CodeTag = C(0x86, 0x4A, 0x2C),
            CodeAttr = C(0x8A, 0x6A, 0x2F),
            CodePunct = C(0x9A, 0x90, 0x82),
            QuoteBar = C(0xB8, 0x53, 0x2E),
            Shadow = C(0x1A, 0x14, 0x0E)
        };
        t.Build();
        return t;
    }

    public static Theme Sepia()
    {
        var t = new Theme
        {
            Id = "Sepia", Title = "羊皮纸", Dark = false,
            Bg = C(0xE9, 0xDF, 0xCB),
            Surface = C(0xF7, 0xEF, 0xDE),
            SurfaceAlt = C(0xEF, 0xE4, 0xCE),
            SurfaceHover = C(0xE7, 0xDB, 0xC3),
            Fg = C(0x4A, 0x3A, 0x27),
            FgMuted = C(0x93, 0x81, 0x66),
            Border = C(0xDD, 0xCD, 0xB2),
            BorderStrong = C(0xC9, 0xB6, 0x96),
            Accent = C(0x99, 0x4E, 0x2A),
            AccentSoft = C(0xEE, 0xDC, 0xC8),
            CodeBg = C(0xEF, 0xE5, 0xD1),
            Selection = C(0xE3, 0xC9, 0x9C),
            Caret = C(0x99, 0x4E, 0x2A),
            Link = C(0x8A, 0x45, 0x22),
            TableHeader = C(0xF1, 0xE7, 0xD3),
            CodeKeyword = C(0x9A, 0x40, 0x2C),
            CodeString = C(0x4F, 0x6E, 0x3C),
            CodeComment = C(0x9C, 0x8A, 0x6E),
            CodeNumber = C(0x4A, 0x5D, 0x8C),
            CodeType = C(0x2C, 0x64, 0x70),
            CodeFunction = C(0x6F, 0x44, 0x7F),
            CodeTag = C(0x84, 0x45, 0x2B),
            CodeAttr = C(0x7E, 0x61, 0x28),
            CodePunct = C(0x9A, 0x89, 0x6E),
            QuoteBar = C(0x99, 0x4E, 0x2A),
            Shadow = C(0x24, 0x18, 0x0A)
        };
        t.Build();
        return t;
    }

    public static Theme Night()
    {
        var t = new Theme
        {
            Id = "Night", Title = "夜间", Dark = true,
            Bg = C(0x16, 0x16, 0x19),
            Surface = C(0x1D, 0x1D, 0x21),
            SurfaceAlt = C(0x24, 0x24, 0x2A),
            SurfaceHover = C(0x2B, 0x2B, 0x32),
            Fg = C(0xD8, 0xD4, 0xCD),
            FgMuted = C(0x8B, 0x86, 0x7E),
            Border = C(0x2C, 0x2C, 0x33),
            BorderStrong = C(0x3C, 0x3C, 0x45),
            Accent = C(0xE2, 0x8B, 0x6C),
            AccentSoft = C(0x3A, 0x2A, 0x24),
            CodeBg = C(0x23, 0x23, 0x29),
            Selection = C(0x3E, 0x3A, 0x50),
            Caret = C(0xE2, 0x8B, 0x6C),
            Link = C(0xE8, 0xA1, 0x7E),
            TableHeader = C(0x23, 0x23, 0x29),
            CodeKeyword = C(0xE0, 0x8A, 0x6D),
            CodeString = C(0x9E, 0xC1, 0x84),
            CodeComment = C(0x6E, 0x6A, 0x63),
            CodeNumber = C(0x8F, 0xA7, 0xE0),
            CodeType = C(0x6F, 0xB3, 0xC0),
            CodeFunction = C(0xC3, 0x9B, 0xD8),
            CodeTag = C(0xE0, 0xA0, 0x7A),
            CodeAttr = C(0xD8, 0xB4, 0x6A),
            CodePunct = C(0x8A, 0x85, 0x7C),
            QuoteBar = C(0xE2, 0x8B, 0x6C),
            Shadow = C(0x00, 0x00, 0x00)
        };
        t.Build();
        return t;
    }

    static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    void Build()
    {
        BgBrush = B(Bg); SurfaceBrush = B(Surface); SurfaceAltBrush = B(SurfaceAlt); HoverBrush = B(SurfaceHover);
        FgBrush = B(Fg); MutedBrush = B(FgMuted); BorderBrush = B(Border); BorderStrongBrush = B(BorderStrong);
        AccentBrush = B(Accent); AccentSoftBrush = B(AccentSoft); CodeBgBrush = B(CodeBg);
        SelectionBrush = B(Selection); CaretBrush = B(Caret); LinkBrush = B(Link); TableHeaderBrush = B(TableHeader);
        ShadowBrush = B(WithAlpha(Shadow, Dark ? (byte)110 : (byte)40));
        CodeKeywordBrush = B(CodeKeyword); CodeStringBrush = B(CodeString); CodeCommentBrush = B(CodeComment);
        CodeNumberBrush = B(CodeNumber); CodeTypeBrush = B(CodeType); CodeFunctionBrush = B(CodeFunction);
        CodeTagBrush = B(CodeTag); CodeAttrBrush = B(CodeAttr); CodePunctBrush = B(CodePunct);
        QuoteBarBrush = B(QuoteBar);
    }

    static Brush B(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public Brush Alpha(Brush brush, double opacity)
    {
        var scb = brush as SolidColorBrush;
        if (scb == null) return brush;
        var b = new SolidColorBrush(WithAlpha(scb.Color, (byte)(scb.Color.A * opacity)));
        b.Freeze();
        return b;
    }

    public void ApplyToResources()
    {
        var r = Application.Current.Resources;
        r["Ink.Bg"] = BgBrush;
        r["Ink.Surface"] = SurfaceBrush;
        r["Ink.SurfaceAlt"] = SurfaceAltBrush;
        r["Ink.Hover"] = HoverBrush;
        r["Ink.Fg"] = FgBrush;
        r["Ink.Muted"] = MutedBrush;
        r["Ink.Border"] = BorderBrush;
        r["Ink.BorderStrong"] = BorderStrongBrush;
        r["Ink.Accent"] = AccentBrush;
        r["Ink.AccentSoft"] = AccentSoftBrush;
        r["Ink.CodeBg"] = CodeBgBrush;
        r["Ink.Selection"] = SelectionBrush;
        r["Ink.Caret"] = CaretBrush;
        r["Ink.Link"] = LinkBrush;
        r["Ink.TableHeader"] = TableHeaderBrush;
        r["Ink.CodeKeyword"] = CodeKeywordBrush;
        r["Ink.CodeString"] = CodeStringBrush;
        r["Ink.CodeComment"] = CodeCommentBrush;
        r["Ink.CodeNumber"] = CodeNumberBrush;
        r["Ink.CodeType"] = CodeTypeBrush;
        r["Ink.CodeFunction"] = CodeFunctionBrush;
        r["Ink.CodeTag"] = CodeTagBrush;
        r["Ink.CodeAttr"] = CodeAttrBrush;
        r["Ink.CodePunct"] = CodePunctBrush;
        r["Ink.QuoteBar"] = QuoteBarBrush;
        r["Ink.Shadow"] = ShadowBrush;
    }
}

public static class Fonts
{
    public const string UiStack = "Segoe UI, Microsoft YaHei UI, Microsoft YaHei, PingFang SC, sans-serif";
    public const string MonoStack = "Cascadia Mono, Consolas, Courier New, Microsoft YaHei UI, monospace";
    public const string IconStack = "Segoe UI Symbol, Segoe UI";

    public static FontFamily Ui => new FontFamily(UiStack);
    public static FontFamily Mono => new FontFamily(MonoStack);
    public static FontFamily Icons => new FontFamily(IconStack);

    public static readonly string[] EditorFontOptions =
    {
        "Segoe UI", "Microsoft YaHei UI", "Cascadia Mono", "Consolas", "Georgia", "Times New Roman", "SimSun", "KaiTi"
    };

    public static FontFamily Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ui;
        try { return new FontFamily(name); }
        catch { return Ui; }
    }
}
