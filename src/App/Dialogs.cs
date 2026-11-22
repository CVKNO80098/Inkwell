using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Inkwell.App;

public sealed class RecoveryEntry
{
    public string BackupPath;
    public string OriginalPath;
    public string Title;
    public DateTime SavedAt;
    public string Text;
}

/// <summary>Autosave crash backups stored in %APPDATA%\Inkwell\backups.</summary>
public static class BackupService
{
    public static string NewBackupPath()
    {
        return Path.Combine(AppSettings.BackupDir, "b_" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".md");
    }

    public static void Save(string backupPath, string originalPath, string title, string text)
    {
        try
        {
            string dir = Path.GetDirectoryName(backupPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(backupPath, text ?? "", new UTF8Encoding(false));
            string meta = MetaPath(backupPath);
            string cleanTitle = (title ?? "").Replace("\r", " ").Replace("\n", " ");
            File.WriteAllLines(meta, new[]
            {
                DateTime.UtcNow.ToString("o"),
                originalPath ?? "",
                cleanTitle
            }, new UTF8Encoding(false));
        }
        catch { }
    }

    public static void Delete(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            string meta = MetaPath(backupPath);
            if (File.Exists(meta)) File.Delete(meta);
        }
        catch { }
    }

    static string MetaPath(string backupPath) => Path.ChangeExtension(backupPath, ".meta");

    public static List<RecoveryEntry> Scan()
    {
        var result = new List<RecoveryEntry>();
        try
        {
            foreach (var file in Directory.GetFiles(AppSettings.BackupDir, "b_*.md"))
            {
                try
                {
                    var metaFile = MetaPath(file);
                    if (!File.Exists(metaFile)) continue;
                    var lines = File.ReadAllLines(metaFile);
                    if (lines.Length < 3) continue;
                    if (!DateTime.TryParse(lines[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var when)) continue;
                    string original = lines[1] ?? "";
                    string title = lines[2] ?? "未命名";
                    var text = File.ReadAllText(file, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (!string.IsNullOrEmpty(original) && File.Exists(original))
                    {
                        var origTime = File.GetLastWriteTimeUtc(original);
                        if (when <= origTime.AddSeconds(2)) continue; // already saved
                    }
                    result.Add(new RecoveryEntry
                    {
                        BackupPath = file,
                        OriginalPath = original,
                        Title = string.IsNullOrWhiteSpace(title) ? "未命名" : title,
                        SavedAt = when.ToLocalTime(),
                        Text = text
                    });
                }
                catch { }
            }
        }
        catch { }
        result.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return result;
    }
}

public static class Dialogs
{
    public static Window Create(Window owner, Theme theme, string title, UIElement body, IList<DialogButton> buttons, double width = 540)
    {
        var win = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.Height,
            Width = width,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            MaxHeight = SystemParameters.WorkArea.Height - 80
        };

        var root = new Grid { Margin = new Thickness(22) };
        var border = new Border
        {
            Background = theme.SurfaceBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Effect = Ui.Shadow(theme, 30, 8, 0.45)
        };
        var stack = new StackPanel { Margin = new Thickness(26, 20, 26, 22) };

        var header = new Grid { Background = Brushes.Transparent };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = theme.FgBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var close = new Button
        {
            Content = new TextBlock { Text = "✕", FontSize = 13 },
            Width = 28,
            Height = 28,
            Style = (Style)Application.Current.Resources["IconButton"],
            ToolTip = "关闭"
        };
        close.Click += (s, e) => win.Close();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        header.MouseLeftButtonDown += (s, e) => { try { win.DragMove(); } catch { } };
        stack.Children.Add(header);

        if (body is FrameworkElement bodyElement) bodyElement.Margin = new Thickness(0, 16, 0, 4);
        stack.Children.Add(body);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        foreach (var b in buttons)
        {
            var style = (Style)Application.Current.Resources[b.Primary ? "AccentButton" : "OutlineButton"];
            var btn = new Button
            {
                Content = new TextBlock { Text = b.Text, FontSize = 13 },
                Style = style,
                Margin = new Thickness(10, 0, 0, 0),
                MinWidth = 86
            };
            var action = b.Action;
            btn.Click += (s, e) => { try { action(win); } catch { } };
            if (b.Primary) win.Tag = btn;
            footer.Children.Add(btn);
        }
        stack.Children.Add(footer);

        border.Child = stack;
        root.Children.Add(border);
        win.Content = root;

        win.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) { win.Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && win.Tag is Button primary)
            {
                // let multi-line text boxes keep Enter
                if (Keyboard.FocusedElement is TextBox) return;
                primary.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                e.Handled = true;
            }
        };
        win.Loaded += (s, e) => win.Dispatcher.BeginInvoke(new Action(() =>
        {
            var focusTarget = win.Tag as Button;
            focusTarget?.Focus();
        }));
        return win;
    }

    public sealed class DialogButton
    {
        public string Text;
        public Action<Window> Action;
        public bool Primary;
        public static DialogButton PrimaryButton(string text, Action<Window> act) => new DialogButton { Text = text, Action = act, Primary = true };
        public static DialogButton Normal(string text, Action<Window> act) => new DialogButton { Text = text, Action = act };
    }

    public static void ShowAbout(Window owner, Theme theme)
    {
        var body = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(LogoBlock(theme, 54));
        var textCol = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        textCol.Children.Add(new TextBlock { Text = "Inkwell", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = theme.FgBrush });
        textCol.Children.Add(new TextBlock { Text = "专注写作的 Markdown 编辑器 · v" + App.Version, FontSize = 12.5, Foreground = theme.MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
        head.Children.Add(textCol);
        body.Children.Add(head);

        var info = new TextBlock
        {
            Text = "原生 WPF 桌面程序，单文件运行，无需联网。\n支持 CommonMark / GFM 常用语法、实时预览、多标签、文件树、大纲、\n查找替换、主题切换、崩溃恢复与 HTML / PDF 导出。",
            FontSize = 13,
            Foreground = theme.FgBrush,
            LineHeight = 23,
            Margin = new Thickness(0, 20, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        body.Children.Add(info);

        var meta = new TextBlock
        {
            Text = "运行环境: .NET Framework 4.7.2  ·  界面: WPF  ·  设置目录: %APPDATA%\\Inkwell",
            FontSize = 11.5,
            Foreground = theme.MutedBrush,
            Margin = new Thickness(0, 16, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        body.Children.Add(meta);

        var win = Create(owner, theme, "关于 Inkwell", body, new[]
        {
            DialogButton.PrimaryButton("开始写作", w => w.Close())
        });
        win.ShowDialog();
    }

    public static void ShowShortcuts(Window owner, Theme theme)
    {
        var pairs = new[]
        {
            new[] { "新建文档", "Ctrl + N" }, new[] { "打开文件", "Ctrl + O" },
            new[] { "保存", "Ctrl + S" }, new[] { "另存为", "Ctrl + Shift + S" },
            new[] { "查找 / 替换", "Ctrl + F / Ctrl + H" }, new[] { "关闭标签", "Ctrl + W" },
            new[] { "下一个标签", "Ctrl + Tab" }, new[] { "撤销 / 重做", "Ctrl + Z / Ctrl + Y" },
            new[] { "加粗 / 斜体", "Ctrl + B / Ctrl + I" }, new[] { "插入链接", "Ctrl + K" },
            new[] { "行内代码", "Ctrl + E" }, new[] { "标题 1-6", "Ctrl + 1 … Ctrl + 6" },
            new[] { "正文", "Ctrl + 0" }, new[] { "引用", "Ctrl + Shift + Q" },
            new[] { "无序 / 有序列表", "Ctrl + Shift + L / O" }, new[] { "任务列表", "Ctrl + Shift + T" },
            new[] { "代码块", "Ctrl + Shift + K" }, new[] { "表格", "Ctrl + Shift + M" },
            new[] { "编辑 / 分栏 / 预览", "Ctrl + Alt + 1 / 2 / 3" }, new[] { "专注模式", "F11" },
            new[] { "打字机模式", "Ctrl + Shift + Y" }, new[] { "侧边栏", "Ctrl + Shift + B" },
            new[] { "放大 / 缩小字号", "Ctrl + = / Ctrl + -" }, new[] { "重置字号", "Ctrl + Shift + 0" }
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int rows = (pairs.Length + 1) / 2;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < pairs.Length; i++)
        {
            int col = i < rows ? 0 : 1;
            int row = i < rows ? i : i - rows;
            var item = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            item.Children.Add(new TextBlock { Text = pairs[i][0], FontSize = 12.5, Foreground = theme.FgBrush, VerticalAlignment = VerticalAlignment.Center });
            var key = new TextBlock { Text = pairs[i][1], FontSize = 11.5, Foreground = theme.MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(key, 1);
            item.Children.Add(key);
            Grid.SetColumn(item, col);
            Grid.SetRow(item, row);
            grid.Children.Add(item);
        }
        var panel = new ScrollViewer
        {
            Content = grid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 430,
            Margin = new Thickness(0, 4, 10, 0)
        };
        var win = Create(owner, theme, "快捷键", panel, new[]
        {
            DialogButton.PrimaryButton("知道了", w => w.Close())
        }, 660);
        win.ShowDialog();
    }

    public static string Prompt(Window owner, Theme theme, string title, string label, string initial = "")
    {
        string result = null;
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = label, FontSize = 12.5, Foreground = theme.MutedBrush, Margin = new Thickness(0, 0, 0, 8) });
        var box = new TextBox { Text = initial ?? "", FontSize = 13.5, Height = 36, Padding = new Thickness(10, 6, 10, 6) };
        body.Children.Add(box);
        DialogButton ok = null;
        ok = new DialogButton
        {
            Text = "确定",
            Primary = true,
            Action = w => { result = box.Text; w.Close(); }
        };
        var win = Create(owner, theme, title, body, new[]
        {
            ok,
            DialogButton.Normal("取消", w => w.Close())
        }, 460);
        win.Loaded += (s, e) => win.Dispatcher.BeginInvoke(new Action(() =>
        {
            box.Focus();
            box.SelectAll();
        }));
        win.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { result = box.Text; win.Close(); e.Handled = true; }
        };
        win.ShowDialog();
        return result;
    }

    public static List<RecoveryEntry> Recover(Window owner, Theme theme, List<RecoveryEntry> entries)
    {
        List<RecoveryEntry> result = null;
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "检测到上次退出前未保存的内容。选择要恢复的文档：",
            FontSize = 12.5,
            Foreground = theme.MutedBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var list = new StackPanel();
        var checks = new List<Tuple<CheckBox, RecoveryEntry>>();
        foreach (var entry in entries)
        {
            var row = new Border
            {
                Background = theme.SurfaceAltBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var check = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            grid.Children.Add(check);
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(entry.OriginalPath) ? entry.Title + "（未命名）" : entry.OriginalPath,
                FontSize = 12.5,
                Foreground = theme.FgBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            texts.Children.Add(new TextBlock
            {
                Text = entry.SavedAt.ToString("yyyy-MM-dd HH:mm") + "  ·  " + TextUtil.CountWords(entry.Text) + " 字",
                FontSize = 11,
                Foreground = theme.MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);
            row.Child = grid;
            list.Children.Add(row);
            checks.Add(Tuple.Create(check, entry));
        }
        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 300,
            Margin = new Thickness(0, 0, 8, 0)
        };
        body.Children.Add(scroll);

        var win = Create(owner, theme, "恢复未保存的文档", body, new[]
        {
            new DialogButton { Text = "恢复所选", Primary = true, Action = w =>
                {
                    result = checks.Where(c => c.Item1.IsChecked == true).Select(c => c.Item2).ToList();
                    w.Close();
                } },
            new DialogButton { Text = "全部删除", Action = w => { result = new List<RecoveryEntry>(); w.Close(); } },
            new DialogButton { Text = "稍后处理", Action = w => { result = null; w.Close(); } }
        }, 560);
        win.ShowDialog();
        return result;
    }

    public static Border LogoBlock(Theme theme, double size)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.26),
            Background = new LinearGradientBrush(theme.Accent, Theme.WithAlpha(theme.Accent, 190), 45),
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = new TextBlock
        {
            Text = "I",
            FontSize = size * 0.56,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia, Times New Roman, Microsoft YaHei UI"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -size * 0.06, 0, 0)
        };
        return border;
    }
}
