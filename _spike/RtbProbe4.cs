using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

public static class RtbProbe4
{
    [STAThread]
    public static void Main()
    {
        var rtb = new RichTextBox();
        new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text = "hello world\r\nsecond line\r\n";
        var p = (Paragraph)rtb.Document.Blocks.FirstBlock;

        var b = p.ContentStart.GetPositionAtOffset(2 + 1);
        var e = p.ContentStart.GetPositionAtOffset(5 + 1);
        var r = new TextRange(b, e);
        Console.WriteLine("range text=[" + r.Text + "]");
        r.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

        // now re-check offsets after split into runs
        var doc = rtb.Document;
        foreach (var blk in doc.Blocks)
        {
            var par = blk as Paragraph;
            if (par == null) continue;
            Console.WriteLine("para text=[" + new TextRange(par.ContentStart, par.ContentEnd).Text + "] runs=" + CountRuns(par));
        }
        var r2b = p.ContentStart.GetPositionAtOffset(0 + 1);
        var r2e = p.ContentStart.GetPositionAtOffset(3 + 1);
        Console.WriteLine("range[0,3)=[" + new TextRange(r2b, r2e).Text + "]");
        var r3b = p.ContentStart.GetPositionAtOffset(6 + 1);
        var r3e = p.ContentStart.GetPositionAtOffset(11 + 1);
        Console.WriteLine("range[6,11)=[" + new TextRange(r3b, r3e).Text + "]");
        // bold presence check at char 2..4
        var c2 = p.ContentStart.GetPositionAtOffset(2 + 1);
        var c3 = p.ContentStart.GetPositionAtOffset(4 + 1);
        object w = new TextRange(c2, c3).GetPropertyValue(TextElement.FontWeightProperty);
        Console.WriteLine("weight at 2..4=" + w);
        var c0 = p.ContentStart.GetPositionAtOffset(1);
        object w0 = new TextRange(c0, p.ContentStart.GetPositionAtOffset(2)).GetPropertyValue(TextElement.FontWeightProperty);
        Console.WriteLine("weight at 0..1=" + w0);
    }

    static int CountRuns(Paragraph p)
    {
        int n = 0;
        foreach (var inl in p.Inlines) n++;
        return n;
    }
}
