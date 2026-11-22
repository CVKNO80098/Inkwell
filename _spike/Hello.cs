using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public static class Hello
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        var win = new Window
        {
            Title = "Spike",
            Width = 420,
            Height = 240,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
            Content = new TextBlock
            {
                Text = "WPF pipeline OK",
                Foreground = Brushes.White,
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        win.Loaded += (s, e) => Console.WriteLine("loaded");
        app.Run(win);
    }
}
