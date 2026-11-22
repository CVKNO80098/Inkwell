using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

public static class Probe
{
    static void Log(string s)
    {
        File.AppendAllText(@"D:\Code\MdAppTest\_spike\probe.log", DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
    }

    [STAThread]
    public static void Main()
    {
        Log("main start");
        try
        {
            var app = new Application();
            Log("application created");
            var win = new Window { Title = "Probe", Width = 400, Height = 300, Content = new TextBlock { Text = "hi" } };
            Log("window created");
            win.Loaded += (s, e) => { Log("loaded"); win.Close(); };
            win.Show();
            Log("shown");
            app.Run();
            Log("run returned");
        }
        catch (Exception ex)
        {
            Log("EX " + ex);
        }
        Log("exit");
    }
}
