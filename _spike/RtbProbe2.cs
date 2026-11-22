using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

public static class RtbProbe2
{
    [STAThread]
    public static void Main()
    {
        var rtb = new RichTextBox();
        new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text = "hello world\r\nsecond line\r\n";
        var doc = rtb.Document;
        Console.WriteLine("full=[" + Show(new TextRange(doc.ContentStart, doc.ContentEnd).Text) + "] blocks=" + doc.Blocks.Count);
        int pi = 0;
        foreach (var b in doc.Blocks)
        {
            var p = b as Paragraph;
            if (p == null) continue;
            string pt = new TextRange(p.ContentStart, p.ContentEnd).Text;
            Console.WriteLine($"para[{pi}] len={pt.Length} text=[{Show(pt)}]");
            for (int k = 0; k <= pt.Length; k++)
            {
                var a = p.ContentStart.GetPositionAtOffset(k);
                var bb = p.ContentStart.GetPositionAtOffset(k + 1);
                string seg = (a == null || bb == null) ? "<null>" : Show(new TextRange(a, bb).Text);
                int? off = a == null ? null : p.ContentStart.GetOffsetToPosition(a);
                Console.WriteLine($"  k={k} off={off} seg=[{seg}]");
            }
            // also test range covering [2,5)
            var s = p.ContentStart.GetPositionAtOffset(2);
            var e = p.ContentStart.GetPositionAtOffset(5);
            Console.WriteLine("  range[2,5)=[" + Show(new TextRange(s, e).Text) + "]");
            pi++;
        }

        // selection offsets
        var c = doc.ContentStart.GetPositionAtOffset(3);
        Console.WriteLine("doc offset 3 char: " + (c != null ? Show(new TextRange(c, c.GetPositionAtOffset(1)).Text) : "null"));
        Console.WriteLine("doc end offset: " + doc.ContentStart.GetOffsetToPosition(doc.ContentEnd));
    }

    static string Show(string s) => s.Replace("\r", "\\r").Replace("\n", "\\n");
}
