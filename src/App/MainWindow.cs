using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Inkwell.App;

public sealed partial class MainWindow : Window
{
    AppSettings _settings;
    Theme _theme;

    readonly List<DocTab> _tabs = new List<DocTab>();
    int _active = -1;

    Grid _root;
    StackPanel _tabStack;
    ScrollViewer _tabScroll;
    Sidebar _sidebar;
    ColumnDefinition _sidebarCol;
    GridSplitter _sidebarSplitter;
    Border _sidebarBorder;
    double _sidebarWidth = 248;

    Grid _mainGrid;
    Grid _paneGrid;
    ColumnDefinition _editorCol, _splitCol, _previewCol;
    GridSplitter _paneSplitter;
    Grid _editorHost;
    Border _editorPane;
    Border _previewPane;
    FlowDocumentScrollViewer _previewViewer;
    Border _welcome;
    Border _toolbarBorder;
    StackPanel _toolbar;
    Border _statusBorder;
    TextBlock _statusLeft, _statusRight;
    StackPanel _modeSwitch;
    readonly List<Button> _modeButtons = new List<Button>();
    TextBlock _titleDoc;
    Button _menuButton;

    DispatcherTimer _previewTimer;
    DispatcherTimer _outlineTimer;
    DispatcherTimer _autosaveTimer;
    DispatcherTimer _settingsTimer;
    DispatcherTimer _statusTimer;


    double _lastPreviewOffset;

    private void TryLoadAppIcon()
    {
        try
        {
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe))
            {
                if (ico == null) return;
                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ico.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch { }
    }

    public MainWindow(AppSettings settings)
    {
        _settings = settings ?? AppSettings.Load();
        _theme = Theme.ById(_settings.Theme);
        _theme.ApplyToResources();
        Ui.ApplyStyles();

        Title = App.Name;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        TryLoadAppIcon();
        SetResourceReference(BackgroundProperty, "Ink.Bg");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        MinWidth = 820;
        MinHeight = 540;
        Width = Math.Max(820, _settings.WindowWidth);
        Height = Math.Max(540, _settings.WindowHeight);
        WindowStartupLocation = WindowStartupLocation.Manual;
        var wa = SystemParameters.WorkArea;
        if (_settings.HasWindowPos)
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        else
        {
            Left = wa.Left + Math.Max(0, (wa.Width - Width) / 2);
            Top = wa.Top + Math.Max(0, (wa.Height - Height) / 2);
        }
        if (_settings.Maximized) WindowState = WindowState.Maximized;
        if (_settings.WindowWidth >= wa.Width - 40) Width = Math.Min(Width, wa.Width - 40);
        if (_settings.WindowHeight >= wa.Height - 40) Height = Math.Min(Height, wa.Height - 40);

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 42,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        BuildUi();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(190) };
        _previewTimer.Tick += (s, e) =>
        {
            _previewTimer.Stop();
            var tab = ActiveTab;
            if (tab != null && tab.Editor.IsComposing) { _previewTimer.Start(); return; }
            RenderPreview();
        };
        _outlineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _outlineTimer.Tick += (s, e) => { _outlineTimer.Stop(); RebuildOutline(); };
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autosaveTimer.Tick += (s, e) => Autosave();
        _settingsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _settingsTimer.Tick += (s, e) => { _settingsTimer.Stop(); SaveSettings(); };
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); RefreshStatus(); };

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += (s, e) => UpdateMaximizedMargin();
        PreviewKeyDown += OnWindowKeyDown;
        _autosaveTimer.Start();
    }

    public AppSettings Settings => _settings;

    // ------------------------------------------------------------------ UI

    void BuildUi()
    {
        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        _root.SetResourceReference(Panel.BackgroundProperty, "Ink.Bg");
        Content = _root;

        var titleBar = BuildTitleBar();
        Grid.SetRow(titleBar, 0);
        _root.Children.Add(titleBar);

        _tabScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Padding = new Thickness(10, 0, 10, 0),
            Focusable = false
        };
        _tabStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        _tabScroll.Content = _tabStack;
        _tabScroll.PreviewMouseWheel += (s, e) =>
        {
            _tabScroll.ScrollToHorizontalOffset(_tabScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        };
        Grid.SetRow(_tabScroll, 1);
        _root.Children.Add(_tabScroll);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_settings.ShowSidebar ? _sidebarWidth : 0) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _sidebarCol = content.ColumnDefinitions[0];

        _sidebar = new Sidebar(_theme, path => OpenFile(path), PickFolder, line => JumpToLine(line));
        _sidebarBorder = new Border { Child = _sidebar, BorderThickness = new Thickness(0, 0, 1, 0) };
        _sidebarBorder.SetResourceReference(Border.BorderBrushProperty, "Ink.Border");
        _sidebarBorder.SetResourceReference(Border.BackgroundProperty, "Ink.Bg");
        Grid.SetColumn(_sidebarBorder, 0);
        content.Children.Add(_sidebarBorder);

        _sidebarSplitter = new GridSplitter
        {
            Style = (Style)Application.Current.Resources["PaneSplitter"],
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _sidebarSplitter.DragCompleted += (s, e) =>
        {
            if (_sidebarCol.Width.Value > 40) _sidebarWidth = _sidebarCol.Width.Value;
        };
        Grid.SetColumn(_sidebarSplitter, 1);
        content.Children.Add(_sidebarSplitter);

        _mainGrid = new Grid();
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_settings.ShowToolbar ? 38 : 0) });
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _mainGrid.SetResourceReference(Panel.BackgroundProperty, "Ink.Surface");
        Grid.SetColumn(_mainGrid, 2);
        content.Children.Add(_mainGrid);

        _toolbarBorder = new Border { Height = 38, BorderThickness = new Thickness(0, 0, 0, 1) };
        _toolbarBorder.SetResourceReference(Border.BorderBrushProperty, "Ink.Border");
        _toolbarBorder.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        _toolbar = BuildToolbar();
        _toolbarBorder.Child = _toolbar;
        _mainGrid.Children.Add(_toolbarBorder);

        _paneGrid = new Grid();
        _paneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _paneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        _paneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _editorCol = _paneGrid.ColumnDefinitions[0];
        _splitCol = _paneGrid.ColumnDefinitions[1];
        _previewCol = _paneGrid.ColumnDefinitions[2];

        _editorHost = new Grid();
        _editorPane = new Border { Child = _editorHost };
        _editorPane.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        Grid.SetColumn(_editorPane, 0);
        _paneGrid.Children.Add(_editorPane);

        _paneSplitter = new GridSplitter
        {
            Style = (Style)Application.Current.Resources["PaneSplitter"],
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(_paneSplitter, 1);
        _paneGrid.Children.Add(_paneSplitter);

        _previewViewer = new FlowDocumentScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsToolBarVisible = false,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Focusable = false
        };
        _previewPane = new Border { Child = _previewViewer };
        _previewPane.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        Grid.SetColumn(_previewPane, 2);
        _paneGrid.Children.Add(_previewPane);
        _previewViewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnPreviewScrollChanged));
        _previewViewer.SizeChanged += (s, e) => AdjustPreviewPadding();

        _welcome = new Border();
        _welcome.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        Grid.SetColumnSpan(_welcome, 3);
        Panel.SetZIndex(_welcome, 5);
        _paneGrid.Children.Add(_welcome);

        _findBar = BuildFindBar();
        _findBar.Visibility = Visibility.Collapsed;
        Panel.SetZIndex(_findBar, 10);
        _editorHost.Children.Add(_findBar);

        Grid.SetRow(_paneGrid, 1);
        _mainGrid.Children.Add(_paneGrid);

        Grid.SetRow(content, 2);
        _root.Children.Add(content);

        _statusBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0) };
        _statusBorder.SetResourceReference(Border.BorderBrushProperty, "Ink.Border");
        _statusBorder.SetResourceReference(Border.BackgroundProperty, "Ink.Bg");
        var statusGrid = new Grid { Margin = new Thickness(14, 0, 14, 0) };
        _statusLeft = new TextBlock { FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
        _statusLeft.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
        _statusRight = new TextBlock
        {
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _statusRight.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");

        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightPanel.Children.Add(_statusRight);

        _modeSwitch = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var modePairs = new[]
        {
            new[] { "Split", "分栏" }, new[] { "Editor", "编辑" },
            new[] { "Preview", "预览" }, new[] { "Single", "单页" }
        };
        foreach (var pair in modePairs)
        {
            string id = pair[0], label = pair[1];
            var b = Ui.FlatButton(label, "切换到视图：" + label, () => SetViewMode(id), 11.5);
            b.Padding = new Thickness(8, 2, 8, 2);
            b.Margin = new Thickness(1, 0, 1, 0);
            b.MinHeight = 19;
            _modeSwitch.Children.Add(b);
            _modeButtons.Add(b);
        }
        var modeBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2),
            Margin = new Thickness(12, 0, 0, 0),
            Child = _modeSwitch,
            VerticalAlignment = VerticalAlignment.Center
        };
        modeBorder.SetResourceReference(Border.BackgroundProperty, "Ink.SurfaceAlt");
        rightPanel.Children.Add(modeBorder);

        statusGrid.Children.Add(_statusLeft);
        statusGrid.Children.Add(rightPanel);
        _statusBorder.Child = statusGrid;
        Grid.SetRow(_statusBorder, 3);
        _root.Children.Add(_statusBorder);

        ApplyViewMode();
        ApplySidebarVisibility(_settings.ShowSidebar && true);
        RebuildTabStrip();
        RebuildWelcome();
        RefreshStatus();
    }

    FrameworkElement BuildTitleBar()
    {
        var bar = new Grid();
        bar.SetResourceReference(Panel.BackgroundProperty, "Ink.Bg");
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Button menuBtn = null;
        menuBtn = Ui.IconButton("☰", "菜单", () => { }, 32, 15);
        menuBtn.Click += (s, e) => MenuPanel.Show(menuBtn, _theme, BuildMenus());
        _menuButton = menuBtn;
        left.Children.Add(menuBtn);
        var logo = Dialogs.LogoBlock(_theme, 21);
        logo.Margin = new Thickness(6, 0, 8, 0);
        left.Children.Add(logo);
        var name = new TextBlock
        {
            Text = App.Name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Fg");
        left.Children.Add(name);
        Grid.SetColumn(left, 0);
        bar.Children.Add(left);

        var docTitle = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(20, 0, 20, 0)
        };
        docTitle.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
        _titleDoc = docTitle;
        Grid.SetColumn(docTitle, 3);
        bar.Children.Add(docTitle);

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Stretch };
        var themeBtn = Ui.IconButton("◐", "切换主题", () => CycleTheme(), 34, 13);
        themeBtn.VerticalAlignment = VerticalAlignment.Center;
        themeBtn.Height = 30;
        right.Children.Add(themeBtn);

        var sideBtn = Ui.IconButton("▥", "显示/隐藏侧边栏", () => ToggleSidebar(), 34, 13);
        sideBtn.VerticalAlignment = VerticalAlignment.Center;
        sideBtn.Height = 30;
        right.Children.Add(sideBtn);

        var min = new Button { Content = new TextBlock { Text = "─", FontSize = 11 }, Style = (Style)Application.Current.Resources["CaptionButton"], ToolTip = "最小化" };
        min.Click += (s, e) => WindowState = WindowState.Minimized;
        right.Children.Add(min);

        var max = new Button { Content = new TextBlock { Text = "□", FontSize = 11 }, Style = (Style)Application.Current.Resources["CaptionButton"], ToolTip = "最大化" };
        max.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        right.Children.Add(max);

        var close = new Button { Content = new TextBlock { Text = "✕", FontSize = 12 }, Style = (Style)Application.Current.Resources["CloseCaption"], ToolTip = "关闭" };
        close.Click += (s, e) => Close();
        right.Children.Add(close);

        Grid.SetColumn(right, 4);
        bar.Children.Add(right);

        foreach (var element in new UIElement[] { menuBtn, themeBtn, sideBtn, min, max, close })
        {
            WindowChrome.SetIsHitTestVisibleInChrome(element, true);
        }
        return bar;
    }

    StackPanel BuildToolbar()
    {
        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(Tb("B", "加粗 (Ctrl+B)", () => WrapInline("**", "**"), true));
        sp.Children.Add(Tb("I", "斜体 (Ctrl+I)", () => WrapInline("*", "*"), false, true));
        sp.Children.Add(Tb("S", "删除线 (Ctrl+Shift+X)", () => WrapInline("~~", "~~"), false, false, true));
        sp.Children.Add(Tb("</>", "行内代码 (Ctrl+E)", () => WrapInline("`", "`")));
        sp.Children.Add(Tb("==", "高亮", () => WrapInline("==", "==")));
        sp.Children.Add(TbSep());
        sp.Children.Add(Tb("H1", "一级标题 (Ctrl+1)", () => ApplyHeading(1)));
        sp.Children.Add(Tb("H2", "二级标题 (Ctrl+2)", () => ApplyHeading(2)));
        sp.Children.Add(Tb("H3", "三级标题 (Ctrl+3)", () => ApplyHeading(3)));
        sp.Children.Add(TbSep());
        sp.Children.Add(Tb("“", "引用 (Ctrl+Shift+Q)", () => TogglePrefix(new[] { "> " })));
        sp.Children.Add(Tb("•", "无序列表 (Ctrl+Shift+U)", () => TogglePrefix(new[] { "- ", "* ", "+ " })));
        sp.Children.Add(Tb("1.", "有序列表 (Ctrl+Shift+O)", () => TogglePrefix(new[] { "1. " })));
        sp.Children.Add(Tb("☑", "任务列表 (Ctrl+Shift+T)", () => TogglePrefix(new[] { "- [ ] ", "- [x] " })));
        sp.Children.Add(TbSep());
        sp.Children.Add(Tb("{}", "代码块 (Ctrl+Shift+K)", InsertCodeBlock));
        sp.Children.Add(Tb("⊞", "表格 (Ctrl+Shift+M)", InsertTable));
        sp.Children.Add(Tb("―", "分隔线", InsertHr));
        sp.Children.Add(Tb("🔗", "链接 (Ctrl+K)", InsertLink));
        return sp;
    }

    Button Tb(string text, string tip, Action act, bool bold = false, bool italic = false, bool strike = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal
        };
        var b = new Button
        {
            Content = block,
            Style = (Style)Application.Current.Resources["FlatButton"],
            ToolTip = tip,
            Height = 26,
            MinWidth = 26,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 1, 0)
        };
        b.Click += (s, e) => act();
        return b;
    }

    Border TbSep()
    {
        var b = new Border { Width = 1, Height = 16, Margin = new Thickness(7, 0, 7, 0) };
        b.SetResourceReference(Border.BackgroundProperty, "Ink.Border");
        return b;
    }

    // ------------------------------------------------------------------ theme / view

    string CurrentThemeName => _theme.Title;

    void CycleTheme()
    {
        int idx = Array.IndexOf(Theme.Order, _theme.Id);
        if (idx < 0) idx = 0;
        SetTheme(Theme.Order[(idx + 1) % Theme.Order.Length]);
    }

    public void SetTheme(string id)
    {
        _settings.Theme = id;
        _theme = Theme.ById(id);
        _theme.ApplyToResources();
        foreach (var tab in _tabs) tab.Editor.ApplyTheme(_theme);
        _sidebar.SetTheme(_theme);
        RebuildTabStrip();
        RebuildWelcome();
        if (_active >= 0) RenderPreview();
        RefreshStatus();
        ScheduleSettingsSave();
    }

    void ApplyViewMode()
    {
        string mode = _settings.ViewMode;
        if (mode != "Editor" && mode != "Preview" && mode != "Split" && mode != "Single") mode = "Split";
        _settings.ViewMode = mode;
        bool single = mode == "Single";
        bool showEditor = mode != "Preview";
        bool showPreview = mode != "Editor" && mode != "Single";
        _editorPane.Visibility = showEditor ? Visibility.Visible : Visibility.Collapsed;
        _previewPane.Visibility = showPreview ? Visibility.Visible : Visibility.Collapsed;
        _paneSplitter.Visibility = showEditor && showPreview ? Visibility.Visible : Visibility.Collapsed;
        _editorCol.Width = showEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _splitCol.Width = new GridLength(showEditor && showPreview ? 7 : 0);
        _previewCol.Width = showPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        foreach (var tab in _tabs) tab.Editor.SinglePage = single;
        foreach (var tab in _tabs) tab.Editor.StyleNow(false);
        if (showPreview) RenderPreview();
        RefreshStatus();
    }

    public void SetViewMode(string mode)
    {
        _settings.ViewMode = mode;
        ApplyViewMode();
        ScheduleSettingsSave();
    }

    void ToggleSidebar()
    {
        ApplySidebarVisibility(_sidebarCol.Width.Value < 20);
    }

    void ApplySidebarVisibility(bool visible)
    {
        _settings.ShowSidebar = visible;
        if (visible)
        {
            _sidebarCol.Width = new GridLength(_sidebarWidth > 100 ? _sidebarWidth : 248);
            _sidebarBorder.Visibility = Visibility.Visible;
            _sidebarSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            if (_sidebarCol.Width.Value > 40) _sidebarWidth = _sidebarCol.Width.Value;
            _sidebarCol.Width = new GridLength(0);
            _sidebarBorder.Visibility = Visibility.Collapsed;
            _sidebarSplitter.Visibility = Visibility.Collapsed;
        }
        ScheduleSettingsSave();
    }

    void ToggleToolbar()
    {
        _settings.ShowToolbar = !_settings.ShowToolbar;
        _mainGrid.RowDefinitions[0].Height = new GridLength(_settings.ShowToolbar ? 38 : 0);
        ScheduleSettingsSave();
    }

    void UpdateMaximizedMargin()
    {
        _root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
    }

    void ScheduleStatusRefresh()
    {
        if (_statusTimer == null) return;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    void RefreshStatus()
    {
        UpdateModeSwitch();
        var tab = ActiveTab;
        if (tab == null)
        {
            _statusLeft.Text = "就绪 · 支持 CommonMark / GFM";
            _statusRight.Text = "Inkwell v" + App.Version + "   ·   " + _theme.Title;
            return;
        }
        string text = tab.Editor.Text;
        int words = TextUtil.CountWords(text);
        int chars = text.Length;
        int lines = text.Length == 0 ? 1 : text.Split('\n').Length;
        var st = tab.Editor.GetCaretState();
        _statusLeft.Text = string.Format("{0:N0} 字 · {1:N0} 字符 · {2:N0} 行 · 第 {3} 行, 第 {4} 列",
            words, chars, lines, st.Line + 1, st.Col + 1);
        string state = tab.Dirty ? "未保存" : (tab.FilePath != null ? "已保存" : "草稿");
        _statusRight.Text = state + "   ·   " + _theme.Title + "   ·   UTF-8";
    }

    void UpdateModeSwitch()
    {
        if (_modeButtons.Count == 0) return;
        string[] ids = { "Split", "Editor", "Preview", "Single" };
        var accent = Application.Current.Resources["Ink.Accent"] as Brush;
        var muted = Application.Current.Resources["Ink.Muted"] as Brush;
        for (int i = 0; i < _modeButtons.Count && i < ids.Length; i++)
        {
            bool on = _settings.ViewMode == ids[i];
            var b = _modeButtons[i];
            b.Background = on && accent != null ? accent : Brushes.Transparent;
            b.Foreground = on ? Brushes.White : (muted ?? Brushes.Gray);
            b.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    string ViewModeName()
    {
        switch (_settings.ViewMode)
        {
            case "Editor": return "仅编辑";
            case "Preview": return "仅预览";
            case "Single": return "单页";
            default: return "分栏";
        }
    }

    // ------------------------------------------------------------------ tab strip

    void RebuildTabStrip()
    {
        _tabStack.Children.Clear();
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabStack.Children.Add(BuildTabRow(_tabs[i], i));
        }
        var plus = Ui.IconButton("+", "新建文档 (Ctrl+N)", () => NewDocument(), 28, 15);
        plus.Margin = new Thickness(6, 2, 0, 4);
        plus.VerticalAlignment = VerticalAlignment.Center;
        _tabStack.Children.Add(plus);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_active >= 0 && _active < _tabs.Count) ScrollActiveTabIntoView();
        }));
    }

    Border BuildTabRow(DocTab tab, int index)
    {
        bool active = index == _active;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = tab.Dirty ? Visibility.Visible : Visibility.Collapsed
        };
        dot.SetResourceReference(Border.BackgroundProperty, "Ink.Accent");
        grid.Children.Add(dot);

        var title = new TextBlock
        {
            Text = tab.DisplayTitle,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = tab.FilePath ?? tab.DisplayTitle
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, active ? "Ink.Fg" : "Ink.Muted");
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var close = new Button
        {
            Content = new TextBlock { Text = "✕", FontSize = 9 },
            Width = 18,
            Height = 18,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)Application.Current.Resources["CloseButton"],
            ToolTip = "关闭标签 (Ctrl+W)"
        };
        close.Click += (s, e) => { e.Handled = true; CloseTab(tab); };
        Grid.SetColumn(close, 2);
        grid.Children.Add(close);

        var border = new Border
        {
            Height = 32,
            Padding = new Thickness(12, 0, 7, 0),
            Margin = new Thickness(0, 2, 3, 0),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            BorderThickness = new Thickness(active ? 1 : 0, active ? 1 : 0, active ? 1 : 0, 0),
            Child = grid,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = active ? _theme.SurfaceBrush : Brushes.Transparent
        };
        if (active) border.BorderBrush = _theme.BorderBrush;
        border.MouseEnter += (s, e) => { if (!active) border.Background = _theme.HoverBrush; };
        border.MouseLeave += (s, e) => { if (!active) border.Background = Brushes.Transparent; };
        border.MouseLeftButtonUp += (s, e) => ActivateTab(index);
        border.MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle) { e.Handled = true; CloseTab(tab); }
        };
        tab.TabRow = border;
        tab.TabTitle = title;
        tab.DirtyDot = dot;
        tab.TabClose = close;
        return border;
    }

    void ScrollActiveTabIntoView()
    {
        var row = _tabs[_active].TabRow;
        if (row == null) return;
        var transform = row.TransformToAncestor(_tabStack);
        var pos = transform.Transform(new Point(0, 0));
        double left = pos.X;
        double right = left + row.ActualWidth;
        if (left < _tabScroll.HorizontalOffset) _tabScroll.ScrollToHorizontalOffset(Math.Max(0, left - 20));
        else if (right > _tabScroll.HorizontalOffset + _tabScroll.ViewportWidth)
            _tabScroll.ScrollToHorizontalOffset(right - _tabScroll.ViewportWidth + 20);
    }

    // ------------------------------------------------------------------ welcome

    void RebuildWelcome()
    {
        _welcome.Visibility = _tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_tabs.Count > 0) return;
        var outer = new Grid();
        var stack = new StackPanel
        {
            Width = 540,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var logo = Dialogs.LogoBlock(_theme, 64);
        logo.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(logo);
        var title = new TextBlock
        {
            Text = "Inkwell",
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Fg");
        stack.Children.Add(title);
        var sub = new TextBlock
        {
            Text = "把想法写下来，其余的交给我。",
            FontSize = 13.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
        stack.Children.Add(sub);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 28, 0, 0)
        };
        var newBtn = new Button
        {
            Content = new TextBlock { Text = "新建文档", FontSize = 13 },
            Style = (Style)Application.Current.Resources["AccentButton"],
            MinWidth = 110,
            Height = 36,
            Margin = new Thickness(6, 0, 6, 0)
        };
        newBtn.Click += (s, e) => NewDocument();
        buttons.Children.Add(newBtn);
        var openBtn = new Button
        {
            Content = new TextBlock { Text = "打开文件", FontSize = 13 },
            Style = (Style)Application.Current.Resources["OutlineButton"],
            MinWidth = 110,
            Height = 36,
            Margin = new Thickness(6, 0, 6, 0)
        };
        openBtn.Click += (s, e) => OpenFileDialog();
        buttons.Children.Add(openBtn);
        var folderBtn = new Button
        {
            Content = new TextBlock { Text = "打开文件夹", FontSize = 13 },
            Style = (Style)Application.Current.Resources["OutlineButton"],
            MinWidth = 110,
            Height = 36,
            Margin = new Thickness(6, 0, 6, 0)
        };
        folderBtn.Click += (s, e) => PickFolder();
        buttons.Children.Add(folderBtn);
        stack.Children.Add(buttons);

        if (_settings.Recent != null && _settings.Recent.Count > 0)
        {
            var recentTitle = new TextBlock
            {
                Text = "最近打开",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 36, 0, 8)
            };
            recentTitle.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
            stack.Children.Add(recentTitle);

            var recentPanel = new StackPanel();
            int shown = 0;
            foreach (var path in _settings.Recent)
            {
                if (!File.Exists(path)) continue;
                if (shown++ >= 5) break;
                var entry = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 2),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var fileName = new TextBlock
                {
                    Text = Path.GetFileName(path),
                    FontSize = 12.5,
                    MaxWidth = 200,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                fileName.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Fg");
                row.Children.Add(fileName);
                var dirName = new TextBlock
                {
                    Text = Path.GetDirectoryName(path) ?? "",
                    FontSize = 11,
                    Margin = new Thickness(14, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dirName.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
                Grid.SetColumn(dirName, 1);
                row.Children.Add(dirName);
                entry.Child = row;
                string captured = path;
                entry.MouseEnter += (s, e) => entry.SetResourceReference(Border.BackgroundProperty, "Ink.Hover");
                entry.MouseLeave += (s, e) => entry.Background = Brushes.Transparent;
                entry.MouseLeftButtonUp += (s, e) => OpenFile(captured);
                recentPanel.Children.Add(entry);
            }
            stack.Children.Add(recentPanel);
        }

        var hint = new TextBlock
        {
            Text = "Ctrl+N 新建  ·  Ctrl+O 打开  ·  Ctrl+S 保存  ·  Ctrl+F 查找",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 34, 0, 0)
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
        stack.Children.Add(hint);

        outer.Children.Add(stack);
        _welcome.Child = outer;
    }

    // ------------------------------------------------------------------ lifecycle

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMaximizedMargin();
        ApplyViewMode();
        if (App.EmptyDemo) { RebuildWelcome(); RefreshStatus(); return; }

        var startup = App.StartupFiles;
        if (startup != null && startup.Count > 0)
        {
            foreach (var file in startup) OpenFile(file);
        }
        else
        {
            var session = _settings.SessionFiles ?? new List<string>();
            foreach (var file in session)
            {
                if (File.Exists(file)) OpenFile(file, activate: false);
            }
            if (_tabs.Count > 0)
            {
                int idx = Math.Min(Math.Max(0, _settings.ActiveTab), _tabs.Count - 1);
                ActivateTab(idx);
            }
        }

        if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
        {
            _sidebar.SetFolder(_settings.LastFolder);
        }

        if (_tabs.Count == 0) RebuildWelcome();
        RefreshStatus();

        Dispatcher.BeginInvoke(new Action(CheckRecovery));
    }

    void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        bool anyDirty = _tabs.Any(t => t.Dirty);
        if (anyDirty)
        {
            var result = MessageBox.Show(this,
                "还有未保存的文档。关闭前要保存吗？\n\n选择“是”保存全部，选择“否”直接退出（未保存内容会保留在恢复备份中）。",
                "Inkwell", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (result == MessageBoxResult.Yes)
            {
                foreach (var tab in _tabs.ToList())
                {
                    if (!tab.Dirty) continue;
                    if (!SaveTab(tab, false)) { e.Cancel = true; return; }
                }
            }
        }

        _settings.SessionFiles = _tabs.Where(t => !string.IsNullOrEmpty(t.FilePath)).Select(t => t.FilePath).ToList();
        _settings.ActiveTab = Math.Max(0, _active);
        _settings.Maximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.HasWindowPos = true;
        }
        _settings.Save();
    }

    void SaveSettings()
    {
        _settings.Theme = _theme.Id;
        _settings.ShowSidebar = _sidebarCol.Width.Value > 20;
        _settings.Save();
    }

    void ScheduleSettingsSave()
    {
        if (_settingsTimer == null) return;
        _settingsTimer.Stop();
        _settingsTimer.Start();
    }

    // helpers

    static void UseResource(FrameworkElement el, DependencyProperty prop, string key)
    {
        el.SetResourceReference(prop, key);
    }
}
