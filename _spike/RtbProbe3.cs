using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

public static class RtbProbe3
{
    [STAThread]
    public static void Main()
    {
        var rtb = new RichTextBox();
        new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text = "hello world\r\nsecond line\r\n";
        var doc = rtb.Document;
        int pi = 0;
        foreach (var b in doc.Blocks)
        {
            var p = b as Paragraph;
            if (p == null) continue;
            string pt = new TextRange(p.ContentStart, p.ContentEnd).Text;
            Console.WriteLine($"para[{pi}] len={pt.Length} text=[{pt}]  docStartOff={doc.ContentStart.GetOffsetToPosition(p.ContentStart)} paraEndOff={doc.ContentStart.GetOffsetToPosition(p.ContentEnd)}");
            for (int k = 0; k <= pt.Length; k++)
            {
                var pos = p.ContentStart.GetPositionAtOffset(k);
                string fwd = pos == null ? "null" : pos.GetTextInRun(LogicalDirection.Forward);
                string bwd = pos == null ? "null" : pos.GetTextInRun(LogicalDirection.Backward);
                Console.WriteLine($"  k={k} fwd=[{fwd}] bwd=[{bwd}]");
            }
            pi++;
        }
    }
}
