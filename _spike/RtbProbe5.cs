using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

public static class RtbProbe5
{
    [STAThread]
    public static void Main()
    {
        var rtb = new RichTextBox();
        var doc = rtb.Document;
        new TextRange(doc.ContentStart, doc.ContentEnd).Text = "hello world";
        var p = (Paragraph)doc.Blocks.FirstBlock;
        var r = (Run)p.Inlines.FirstInline;
        Console.WriteLine("run text=[" + r.Text + "]");
        for (int k = 0; k <= r.Text.Length + 1; k++)
        {
            var pos = r.ContentStart.GetPositionAtOffset(k);
            string fwd = pos == null ? "null" : pos.GetTextInRun(LogicalDirection.Forward);
            Console.WriteLine($"  run k={k} fwd=[{fwd}]");
        }
        Console.WriteLine("--- split run into two, then check second run offsets");
        var mid = r.ContentStart.GetPositionAtOffset(5);
        var split = new TextRange(mid, mid);
        split.Text = ""; // noop
        // manually rebuild: two runs
        p.Inlines.Clear();
        p.Inlines.Add(new Run("hello "));
        p.Inlines.Add(new Run("world"));
        foreach (Run rr in p.Inlines)
        {
            Console.WriteLine("run [" + rr.Text + "] len=" + rr.Text.Length);
            for (int k = 0; k <= rr.Text.Length; k++)
            {
                var pos = rr.ContentStart.GetPositionAtOffset(k);
                Console.WriteLine($"   k={k} fwd=[{(pos == null ? "null" : pos.GetTextInRun(LogicalDirection.Forward))}]");
            }
        }
        Console.WriteLine("--- GetNextInsertionPosition boundaries from paragraph start");
        var pos2 = p.ContentStart;
        int n = 0;
        while (pos2 != null && pos2.CompareTo(p.ContentEnd) < 0 && n < 30)
        {
            var next = pos2.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null) break;
            string seg;
            try { seg = new TextRange(pos2, next).Text; } catch { seg = "?"; }
            Console.WriteLine($"  boundary {n}: seg=[{seg}]");
            pos2 = next; n++;
        }
    }
}
