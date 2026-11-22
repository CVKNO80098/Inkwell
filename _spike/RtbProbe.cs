using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

public static class RtbProbe
{
    [STAThread]
    public static void Main()
    {
        var rtb = new RichTextBox();
        var doc = new FlowDocument();
        rtb.Document = doc;
        // set text as the app will: newline normalized to \r\n? try \n first
        new TextRange(doc.ContentStart, doc.ContentEnd).Text = "a\nb\n\nc";
        Dump("set \\n", rtb);

        var rtb2 = new RichTextBox();
        new TextRange(rtb2.Document.ContentStart, rtb2.Document.ContentEnd).Text = "a\r\nb\r\n\r\nc";
        Dump("set \\r\\n", rtb2);

        // long text with trailing newline
        var rtb3 = new RichTextBox();
        new TextRange(rtb3.Document.ContentStart, rtb3.Document.ContentEnd).Text = "x\r\ny\r\n";
        Dump("trailing", rtb3);
    }

    static void Dump(string label, RichTextBox rtb)
    {
        var doc = rtb.Document;
        string full = new TextRange(doc.ContentStart, doc.ContentEnd).Text;
        Console.WriteLine($"--- {label}: full=[{Show(full)}] len={full.Length} blocks={doc.Blocks.Count}");
        int idx = 0;
        foreach (var b in doc.Blocks)
        {
            var p = b as Paragraph;
            if (p == null) { Console.WriteLine("  non-para block " + b.GetType().Name); continue; }
            string pt = new TextRange(p.ContentStart, p.ContentEnd).Text;
            Console.WriteLine($"  para[{idx}] text=[{Show(pt)}] len={pt.Length}");
            // offset mapping inside paragraph
            for (int k = 0; k <= Math.Min(pt.Length, 3); k++)
            {
                var pos = p.ContentStart.GetPositionAtOffset(k);
                string got = pos == null ? "<null>" : Show(new TextRange(pos, p.ContentEnd).Text);
                Console.WriteLine($"    off {k} -> [{got}]");
            }
            idx++;
        }
    }

    static string Show(string s) => s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
