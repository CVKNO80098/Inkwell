using System;
using System.IO;
using System.Windows;
using InkApp = Inkwell.App.App;

namespace Inkwell;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            var app = new InkApp();
            app.Run();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                    DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
