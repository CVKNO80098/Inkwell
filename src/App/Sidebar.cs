using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Inkwell.App;

public sealed class OutlineItem
{
    public int Level;
    public string Text;
    public int Line;
}

/// <summary>Left sidebar with a lightweight file tree and the document outline.</summary>
public sealed class Sidebar : Grid
{
    sealed class FileNode
    {
        public string Path;
        public bool IsDirectory;
        public int Depth;
        public bool Expanded;
        public bool Loaded;
        public List<FileNode> Children = new List<FileNode>();
    }

    Theme _theme;
    readonly Action<string> _openFile;
    readonly Action _pickFolder;
    readonly Action<int> _outlineClick;

    string _root;
    string _activeFile;
    List<FileNode> _roots = new List<FileNode>();
    readonly StackPanel _filePanel = new StackPanel();
    readonly StackPanel _outlinePanel = new StackPanel();
    readonly ScrollViewer _fileScroll;
    readonly ScrollViewer _outlineScroll;
    readonly TextBlock _folderText;
    readonly Button _filesTab;
    readonly Button _outlineTab;
    bool _outlineMode;

    static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mdx", ".txt", ".json", ".yml", ".yaml",
        ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx", ".py", ".cs", ".java", ".go",
        ".rs", ".sql", ".sh", ".ps1", ".bat", ".xml", ".log", ".csv", ".toml", ".ini"
    };

    static readonly HashSet<string> SkipDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".vs", ".idea", "node_modules", "bin", "obj", "packages", "dist", ".cache", "__pycache__"
    };

    public Sidebar(Theme theme, Action<string> openFile, Action pickFolder, Action<int> outlineClick)
    {
        _theme = theme;
        _openFile = openFile;
        _pickFolder = pickFolder;
        _outlineClick = outlineClick;

        Background = theme.BgBrush;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(14, 0, 8, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _folderText = new TextBlock
        {
            Text = "未打开文件夹",
            FontSize = 11.5,
            Foreground = theme.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        header.Children.Add(_folderText);

        var refresh = Ui.FlatButton("⟳", "刷新文件列表", () => Refresh(), 13);
        refresh.Width = 26;
        refresh.Padding = new Thickness(0);
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);

        var openFolder = Ui.FlatButton("⊞", "打开文件夹", () => _pickFolder(), 13);
        openFolder.Width = 26;
        openFolder.Padding = new Thickness(0);
        Grid.SetColumn(openFolder, 2);
        header.Children.Add(openFolder);
        Grid.SetRow(header, 0);
        Children.Add(header);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0) };
        _filesTab = MakeTab("文件", true);
        _filesTab.Click += (s, e) => SetMode(false);
        _outlineTab = MakeTab("大纲", false);
        _outlineTab.Click += (s, e) => SetMode(true);
        tabs.Children.Add(_filesTab);
        tabs.Children.Add(_outlineTab);
        Grid.SetRow(tabs, 1);
        Children.Add(tabs);

        _fileScroll = new ScrollViewer
        {
            Content = _filePanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 2, 0, 8)
        };
        _outlineScroll = new ScrollViewer
        {
            Content = _outlinePanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 2, 0, 8),
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_fileScroll, 2);
        Grid.SetRow(_outlineScroll, 2);
        Children.Add(_fileScroll);
        Children.Add(_outlineScroll);

        BuildFileRows();
    }

    Button MakeTab(string text, bool active)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold },
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 2, 6, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        b.Foreground = active ? _theme.AccentBrush : _theme.MutedBrush;
        return b;
    }

    void SetMode(bool outline)
    {
        _outlineMode = outline;
        _fileScroll.Visibility = outline ? Visibility.Collapsed : Visibility.Visible;
        _outlineScroll.Visibility = outline ? Visibility.Visible : Visibility.Collapsed;
        _filesTab.Foreground = outline ? _theme.MutedBrush : _theme.AccentBrush;
        _outlineTab.Foreground = outline ? _theme.AccentBrush : _theme.MutedBrush;
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        Background = theme.BgBrush;
        _folderText.Foreground = theme.MutedBrush;
        _filesTab.Foreground = _outlineMode ? theme.MutedBrush : theme.AccentBrush;
        _outlineTab.Foreground = _outlineMode ? theme.AccentBrush : theme.MutedBrush;
        BuildFileRows();
        BuildOutlineRows();
    }

    public string Folder => _root;

    public void SetFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            _root = null;
            _roots = new List<FileNode>();
            _folderText.Text = "未打开文件夹";
            BuildFileRows();
            return;
        }
        _root = path;
        _folderText.Text = new DirectoryInfo(path).Name;
        _folderText.ToolTip = path;
        _roots = LoadChildren(path, 0);
        BuildFileRows();
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(_root) || !Directory.Exists(_root)) { SetFolder(null); return; }
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpanded(_roots, expanded);
        _roots = LoadChildren(_root, 0);
        RestoreExpanded(_roots, expanded);
        BuildFileRows();
    }

    static void CollectExpanded(List<FileNode> nodes, HashSet<string> set)
    {
        foreach (var n in nodes)
        {
            if (n.IsDirectory && n.Expanded) set.Add(n.Path);
            CollectExpanded(n.Children, set);
        }
    }

    void RestoreExpanded(List<FileNode> nodes, HashSet<string> set)
    {
        foreach (var n in nodes)
        {
            if (!n.IsDirectory) continue;
            if (set.Contains(n.Path))
            {
                EnsureChildren(n);
                n.Expanded = true;
                RestoreExpanded(n.Children, set);
            }
        }
    }

    public void SetActiveFile(string path)
    {
        _activeFile = path;
        BuildFileRows();
    }

    List<FileNode> LoadChildren(string dir, int depth)
    {
        var list = new List<FileNode>();
        try
        {
            foreach (var d in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(d);
                if (string.IsNullOrEmpty(name)) continue;
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (SkipDirectories.Contains(name)) continue;
                list.Add(new FileNode { Path = d, IsDirectory = true, Depth = depth });
            }
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f);
                if (!TextExtensions.Contains(ext)) continue;
                list.Add(new FileNode { Path = f, IsDirectory = false, Depth = depth });
            }
        }
        catch { }
        return list
            .OrderBy(n => n.IsDirectory ? 0 : 1)
            .ThenBy(n => Path.GetFileName(n.Path), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    void EnsureChildren(FileNode node)
    {
        if (!node.IsDirectory || node.Loaded) return;
        node.Children = LoadChildren(node.Path, node.Depth + 1);
        node.Loaded = true;
    }

    void BuildFileRows()
    {
        _filePanel.Children.Clear();
        if (string.IsNullOrEmpty(_root))
        {
            var empty = new StackPanel { Margin = new Thickness(16, 18, 16, 0) };
            empty.Children.Add(new TextBlock
            {
                Text = "尚未打开文件夹",
                Foreground = _theme.MutedBrush,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            });
            var open = Ui.FlatButton("打开文件夹", "选择一个文件夹", () => _pickFolder(), 12.5);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            empty.Children.Add(open);
            _filePanel.Children.Add(empty);
            return;
        }
        AddNodes(_roots);
    }

    void AddNodes(List<FileNode> nodes)
    {
        foreach (var node in nodes) AddRow(node);
    }

    void AddRow(FileNode node)
    {
        var row = new Border
        {
            Height = 26,
            Margin = new Thickness(6, 1, 6, 1),
            Padding = new Thickness(8 + node.Depth * 14, 0, 6, 0),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        string icon;
        if (node.IsDirectory) icon = node.Expanded ? "▾" : "▸";
        else icon = TextUtil.IsMarkdownFile(node.Path) ? "≡" : "·";
        var iconBlock = new TextBlock
        {
            Text = icon,
            FontSize = node.IsDirectory ? 10 : 13,
            Foreground = node.IsDirectory ? _theme.MutedBrush : _theme.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        grid.Children.Add(iconBlock);

        bool active = !node.IsDirectory && string.Equals(node.Path, _activeFile, StringComparison.OrdinalIgnoreCase);
        var name = new TextBlock
        {
            Text = Path.GetFileName(node.Path),
            FontSize = 12.5,
            Foreground = active ? _theme.AccentBrush : _theme.FgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = node.Path
        };
        if (active) name.FontWeight = FontWeights.SemiBold;
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        row.Child = grid;

        if (active) row.Background = _theme.Alpha(_theme.AccentBrush, 0.14);
        row.MouseEnter += (s, e) => { if (!active) row.Background = _theme.HoverBrush; };
        row.MouseLeave += (s, e) => { if (!active) row.Background = Brushes.Transparent; };
        row.MouseLeftButtonUp += (s, e) =>
        {
            if (node.IsDirectory)
            {
                EnsureChildren(node);
                node.Expanded = !node.Expanded;
                BuildFileRows();
            }
            else
            {
                _openFile?.Invoke(node.Path);
            }
        };
        row.MouseRightButtonUp += (s, e) =>
        {
            e.Handled = true;
            var entries = new List<MenuEntry>();
            if (!node.IsDirectory) entries.Add(MenuEntry.Item("打开", "", () => _openFile?.Invoke(node.Path)));
            entries.Add(MenuEntry.Item("复制路径", "", () => { try { Clipboard.SetText(node.Path); } catch { } }));
            entries.Add(MenuEntry.Sep());
            entries.Add(MenuEntry.Item("在资源管理器中显示", "", () =>
            {
                try
                {
                    if (node.IsDirectory) Process.Start("explorer.exe", "\"" + node.Path + "\"");
                    else Process.Start("explorer.exe", "/select,\"" + node.Path + "\"");
                }
                catch { }
            }));
            MenuPanel.ShowAtMouse(_theme, entries.ToArray());
        };
        _filePanel.Children.Add(row);

        if (node.IsDirectory && node.Expanded)
        {
            foreach (var child in node.Children) AddRow(child);
        }
    }

    List<OutlineItem> _outline = new List<OutlineItem>();

    public void SetOutline(List<OutlineItem> items)
    {
        _outline = items ?? new List<OutlineItem>();
        BuildOutlineRows();
    }

    void BuildOutlineRows()
    {
        _outlinePanel.Children.Clear();
        if (_outline.Count == 0)
        {
            _outlinePanel.Children.Add(new TextBlock
            {
                Text = "文档中没有标题",
                Foreground = _theme.MutedBrush,
                FontSize = 12,
                Margin = new Thickness(16, 14, 16, 0)
            });
            return;
        }
        foreach (var item in _outline)
        {
            var b = new Button
            {
                Content = new TextBlock
                {
                    Text = item.Text,
                    FontSize = item.Level <= 2 ? 12.5 : 12,
                    FontWeight = item.Level == 1 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = item.Level >= 4 ? _theme.MutedBrush : _theme.FgBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10 + (item.Level - 1) * 12, 5, 8, 5),
                Margin = new Thickness(6, 0, 6, 1),
                Cursor = Cursors.Hand,
                ToolTip = item.Text
            };
            int line = item.Line;
            b.Click += (s, e) => _outlineClick?.Invoke(line);
            b.MouseEnter += (s, e) => b.Background = _theme.HoverBrush;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            _outlinePanel.Children.Add(b);
        }
    }
}
