using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Inkwell.Core;

namespace Inkwell.App;

public sealed class App : Application
{
    public const string Name = "Inkwell";
    public const string Version = "1.0.6";

    public static readonly List<string> StartupFiles = new List<string>();
    public static bool DemoMode;
    public static string ThemeArg;
    public static string ModeArg;
    public static bool FindDemo;
    public static bool WheelProbe;
    public static bool EmptyDemo;
    public static bool MenuDemo;
    public static string ScreenshotPath;
    public static string ExportHtmlPath;
    public static string ExportSourcePath;

    MainWindow _window;

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, args) => Log("dispatcher: " + args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, args) => Log("domain: " + args.ExceptionObject);
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        try
        {
            Log("startup args: " + string.Join(" | ", e.Args));
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (string.Equals(arg, "--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    ScreenshotPath = e.Args[++i];
                }
                else if (string.Equals(arg, "--demo", StringComparison.OrdinalIgnoreCase))
                {
                    DemoMode = true;
                }
                else if (string.Equals(arg, "--theme", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    ThemeArg = e.Args[++i];
                }
                else if (string.Equals(arg, "--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    ModeArg = e.Args[++i];
                }
                else if (string.Equals(arg, "--empty", StringComparison.OrdinalIgnoreCase))
                {
                    EmptyDemo = true;
                }
                else if (string.Equals(arg, "--menu", StringComparison.OrdinalIgnoreCase))
                {
                    MenuDemo = true;
                }
                else if (string.Equals(arg, "--export-html", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    ExportHtmlPath = e.Args[++i];
                }
                else if (string.Equals(arg, "--find", StringComparison.OrdinalIgnoreCase))
                {
                    FindDemo = true;
                }
                else if (string.Equals(arg, "--wheelprobe", StringComparison.OrdinalIgnoreCase))
                {
                    WheelProbe = true;
                }
                else if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    StartupFiles.Add(arg);
                }
            }

            if (!string.IsNullOrEmpty(ExportHtmlPath) && StartupFiles.Count > 0)
            {
                ExportOnce(ExportHtmlPath, StartupFiles[0]);
                Shutdown(0);
                return;
            }
            var settings = AppSettings.Load();
            if (!string.IsNullOrEmpty(ThemeArg)) settings.Theme = ThemeArg;
            if (!string.IsNullOrEmpty(ModeArg)) settings.ViewMode = ModeArg;
            Log("settings loaded font=" + settings.EditorFont + " size=" + settings.EditorFontSize + " mode=" + settings.ViewMode);
            _window = new MainWindow(settings);
            Log("window created");
            MainWindow = _window;
            _window.Show();
            Log("window shown");

            if (DemoMode)
            {
                Dispatcher.BeginInvoke(new Action(LoadDemo), DispatcherPriority.Background);
            }
            if (!string.IsNullOrEmpty(ScreenshotPath))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
                timer.Tick += (s, args) => { timer.Stop(); CaptureAndExit(ScreenshotPath); };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            Log("startup failed: " + ex);
            throw;
        }
    }

    void ExportOnce(string outPath, string input)
    {
        try
        {
            string full = Path.GetFullPath(input);
            var doc = MarkdownParser.Parse(File.ReadAllText(full));
            string html = HtmlExporter.ToHtml(doc, new HtmlExportOptions
            {
                Standalone = true,
                Title = Path.GetFileNameWithoutExtension(full),
                BaseDirectory = Path.GetDirectoryName(full),
                EmbedImages = true
            });
            File.WriteAllText(outPath, html);
            Log("exported html -> " + outPath);
        }
        catch (Exception ex) { Log("export failed: " + ex); }
    }
    void LoadDemo()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = null;
            foreach (string candidate in new string[]
            {
                Path.Combine(baseDir, "docs", "欢迎.md"),
                Path.Combine(baseDir, "..", "docs", "欢迎.md"),
                Path.Combine(baseDir, "..", "..", "docs", "欢迎.md")
            })
            {
                if (File.Exists(candidate)) { path = Path.GetFullPath(candidate); break; }
            }
            if (path != null)
            {
                _window.OpenFile(path);
                _window.SetSidebarFolder(Path.GetDirectoryName(path));
            }
            else _window.NewDocument();
            if (string.IsNullOrEmpty(ModeArg)) _window.SetViewMode("Split");
            if (FindDemo) _window.DemoFind();
            if (MenuDemo) _window.DemoMenu();
            if (WheelProbe)
            {
                Dispatcher.BeginInvoke(new Action(() => _window.DemoWheelProbe(3)), DispatcherPriority.ApplicationIdle);
            }
            Log("demo loaded");
        }
        catch (Exception ex)
        {
            Log("demo failed: " + ex);
        }
    }

    void CaptureAndExit(string path)
    {
        try
        {
            if (MenuDemo)
            {
                string dir0 = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir0)) Directory.CreateDirectory(dir0);
                int sw = (int)SystemParameters.PrimaryScreenWidth;
                int sh = (int)SystemParameters.PrimaryScreenHeight;
                using (var screenBmp = new System.Drawing.Bitmap(sw, sh))
                {
                    using (var g = System.Drawing.Graphics.FromImage(screenBmp))
                        g.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);
                    screenBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                Log("screen saved: " + path + " " + sw + "x" + sh);
                Shutdown();
                return;
            }
            var root = (FrameworkElement)_window.Content;
            root.UpdateLayout();
            int w = (int)Math.Ceiling(root.ActualWidth);
            int h = (int)Math.Ceiling(root.ActualHeight);
            if (w <= 0 || h <= 0) { w = (int)_window.Width; h = (int)_window.Height; }
            var bmp = new RenderTargetBitmap(Math.Max(1, w), Math.Max(1, h), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var stream = File.Create(path)) encoder.Save(stream);
            Log("screenshot saved: " + path + " " + w + "x" + h);
        }
        catch (Exception ex)
        {
            Log("screenshot failed: " + ex);
            try { File.WriteAllText(path + ".error.txt", ex.ToString()); } catch { }
        }
        Shutdown();
    }
}
