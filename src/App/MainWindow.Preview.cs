using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Inkwell.Core;
using Microsoft.Win32;

namespace Inkwell.App;

public sealed partial class MainWindow
{

    ScrollViewer _previewScroll;

    // ------------------------------------------------------------------ preview

    void RenderPreview()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        if (_settings.ViewMode == "Editor" || _settings.ViewMode == "Single") return;

        string markdown = tab.Editor.Text;
        var renderSw = System.Diagnostics.Stopwatch.StartNew();
        var renderer = new FlowRenderer
        {
            Theme = _theme,
            FontName = _settings.EditorFont,
            FontSize = _settings.PreviewFontSize,
            BaseDir = tab.FilePath != null ? Path.GetDirectoryName(tab.FilePath) : _settings.LastFolder,
            LinkAction = OpenLink
        };
        FlowDocument doc;
        try { doc = renderer.Build(markdown); }
        catch (Exception ex) { App.Log("render failed: " + ex.GetType().Name + " " + ex.Message); return; }

        _previewViewer.Document = doc;
        AdjustPreviewPadding();
        double offset = Math.Max(0, tab.PreviewOffset);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _syncingScroll = true;
                try
                {
                    var scroller = PreviewScroller(); if (scroller != null) scroller.ScrollToVerticalOffset(offset);
                }
                finally { _syncingScroll = false; }
                _lastPreviewOffset = offset;
            }
            catch { }
        }), DispatcherPriority.Background);
        if (_settings.ViewMode == "Preview")
        {
            _previewTimer.Stop();
        }
        if (renderSw.ElapsedMilliseconds >= 120) App.Log("perf preview " + renderSw.ElapsedMilliseconds + "ms len=" + markdown.Length);
    }

    void SchedulePreviewSoon()
    {
        if (_settings.ViewMode == "Editor" || _settings.ViewMode == "Single") { _previewTimer.Stop(); return; }
        if (ActiveTab != null && ActiveTab.Editor.IsComposing) { _previewTimer.Stop(); return; }
        // Throttle rather than debounce: keep refreshing while typing, at a rate
        // the renderer (and the user's eyes) can keep up with.
        if (_previewTimer.IsEnabled) return;
        int len = ActiveTab != null ? ActiveTab.Editor.Text.Length : 0;
        _previewTimer.Interval = TimeSpan.FromMilliseconds(len > 100000 ? 600 : (len > 40000 ? 120 : 55));
        _previewTimer.Start();
    }

    ScrollViewer PreviewScroller()
    {
        if (_previewScroll != null) return _previewScroll;
        _previewScroll = FindChild<ScrollViewer>(_previewViewer);
        return _previewScroll;
    }

    void AdjustPreviewPadding()
    {
        var doc = _previewViewer.Document as FlowDocument;
        if (doc == null) return;
        double width = _previewViewer.ActualWidth;
        if (width < 200) return;
        double pad = Math.Max(48, (width - 780) / 2);
        doc.PagePadding = new Thickness(pad, 30, pad, 120);
    }

    static T FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var nested = FindChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    void OnPreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 && e.ExtentHeightChange == 0) return;
        _lastPreviewOffset = e.VerticalOffset;
        var tab = ActiveTab;
        if (tab == null) return;
        tab.PreviewOffset = e.VerticalOffset;
        if (e.VerticalChange != 0) SyncEditorToPreview(tab);
    }

    void SyncEditorToPreview(DocTab tab)
    {
        if (_syncingScroll) return;
        if (_settings.ViewMode != "Split") return;
        try
        {
            var edScroll = tab.Editor.Scroller;
            var pvScroll = PreviewScroller();
            if (edScroll == null || pvScroll == null) return;
            if (edScroll.ScrollableHeight <= 0 || pvScroll.ScrollableHeight <= 0) return;
            double ratio = Math.Max(0, Math.Min(1, pvScroll.VerticalOffset / pvScroll.ScrollableHeight));
            double target = ratio * edScroll.ScrollableHeight;
            if (Math.Abs(target - edScroll.VerticalOffset) < 1.0) return;
            _syncingScroll = true;
            try { edScroll.ScrollToVerticalOffset(target); }
            finally { _syncingScroll = false; }
        }
        catch { }
    }

    void SyncPreviewToEditor(DocTab tab)
    {
        if (_syncingScroll) return;
        if (_settings.ViewMode != "Split") return;
        try
        {
            var edScroll = tab.Editor.Scroller;
            var pvScroll = PreviewScroller();
            if (edScroll == null || pvScroll == null) return;
            if (edScroll.ScrollableHeight <= 0 || pvScroll.ScrollableHeight <= 0) return;
            double ratio = Math.Max(0, Math.Min(1, edScroll.VerticalOffset / edScroll.ScrollableHeight));
            double target = ratio * pvScroll.ScrollableHeight;
            if (Math.Abs(target - pvScroll.VerticalOffset) < 1.0) return;
            _syncingScroll = true;
            try { pvScroll.ScrollToVerticalOffset(target); }
            finally { _syncingScroll = false; }
        }
        catch { }
    }

    // ------------------------------------------------------------------ outline

    static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})[ \t]+(.+?)[ \t]*#*[ \t]*$", RegexOptions.Compiled);

    void RebuildOutline()
    {
        var tab = ActiveTab;
        if (tab == null)
        {
            _sidebar.SetOutline(new List<OutlineItem>());
            return;
        }
        string[] lines = TextUtil.SplitLines(tab.Editor.Text);
        var items = new List<OutlineItem>();
        bool inFence = false;
        char fenceChar = '`';
        int fenceLen = 0;
        int frontMatterEnd = -1;
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            for (int i = 1; i < Math.Min(lines.Length, 400); i++)
            {
                string t = lines[i].Trim();
                if (t == "---" || t == "...") { frontMatterEnd = i; break; }
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (i <= frontMatterEnd) continue;
            string line = lines[i];
            int indent = 0;
            while (indent < line.Length && indent < 4 && line[indent] == ' ') indent++;
            if (indent <= 3)
            {
                string rest = line.Substring(indent);
                if (rest.Length >= 3 && (rest[0] == '`' || rest[0] == '~'))
                {
                    char c = rest[0];
                    int n = 0;
                    while (n < rest.Length && rest[n] == c) n++;
                    if (n >= 3)
                    {
                        if (!inFence)
                        {
                            inFence = true;
                            fenceChar = c;
                            fenceLen = n;
                        }
                        else if (c == fenceChar && n >= fenceLen)
                        {
                            inFence = false;
                        }
                        continue;
                    }
                }
            }
            if (inFence) continue;
            var match = HeadingRegex.Match(line);
            if (!match.Success) continue;
            string heading = match.Groups[2].Value.Trim();
            if (heading.Length == 0) continue;
            items.Add(new OutlineItem { Level = match.Groups[1].Value.Length, Text = heading, Line = i + 1 });
            if (items.Count >= 400) break;
        }
        _sidebar.SetOutline(items);
    }

    // ------------------------------------------------------------------ export

    void ExportHtml()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        var dlg = new SaveFileDialog
        {
            Title = "导出为 HTML",
            Filter = "HTML 文件|*.html;*.htm",
            FileName = Path.GetFileNameWithoutExtension(tab.DisplayTitle) + ".html",
            AddExtension = true,
            DefaultExt = ".html"
        };
        if (tab.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(tab.FilePath);
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var parsed = MarkdownParser.Parse(tab.Editor.Text);
            string html = HtmlExporter.ToHtml(parsed, new HtmlExportOptions
            {
                Title = Path.GetFileNameWithoutExtension(tab.DisplayTitle),
                Dark = _theme.Dark,
                Standalone = true,
                EmbedImages = false,
                BaseDirectory = Path.GetDirectoryName(dlg.FileName),
                FontFamily = "'Segoe UI', 'Microsoft YaHei UI', system-ui, sans-serif",
                MonoFamily = "'Cascadia Mono', Consolas, 'Microsoft YaHei UI', monospace"
            });
            File.WriteAllText(dlg.FileName, html, new UTF8Encoding(false));
            _statusLeft.Text = "已导出 HTML：" + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：\n" + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void PrintDocument()
    {
        var tab = ActiveTab;
        if (tab == null) return;
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        try
        {
            var renderer = new FlowRenderer
            {
                Theme = Theme.Paper(),
                FontName = _settings.EditorFont,
                FontSize = _settings.PreviewFontSize,
                BaseDir = tab.FilePath != null ? Path.GetDirectoryName(tab.FilePath) : _settings.LastFolder,
                LinkAction = OpenLink,
                ForPrint = true
            };
            var doc = renderer.Build(tab.Editor.Text);
            double width = dialog.PrintableAreaWidth;
            double height = dialog.PrintableAreaHeight;
            doc.PageWidth = width;
            doc.PageHeight = height;
            doc.ColumnWidth = width;
            doc.PagePadding = new Thickness(52, 48, 52, 48);
            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(width, height);
            dialog.PrintDocument(paginator, tab.DisplayTitle);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打印失败：\n" + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------------ images

    bool InsertImageFromClipboard(DocTab tab)
    {
        if (tab == null) return false;
        BitmapSource source = null;
        try
        {
            if (Clipboard.ContainsImage()) source = Clipboard.GetImage();
        }
        catch { }
        if (source == null) return false;

        string saved = SaveImageAsset(tab, source, "clipboard");
        if (saved == null) return true;
        InsertImageMarkdown(tab, saved);
        return true;
    }

    string SaveImageAsset(DocTab tab, BitmapSource source, string kind)
    {
        if (string.IsNullOrEmpty(tab.FilePath))
        {
            if (!SaveTab(tab, true)) return null;
        }
        string dir = Path.GetDirectoryName(tab.FilePath);
        string assets = Path.Combine(dir, "assets");
        Directory.CreateDirectory(assets);
        string baseName = "img-" + kind + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string path = Path.Combine(assets, baseName + ".png");
        int n = 1;
        while (File.Exists(path)) path = Path.Combine(assets, baseName + "-" + n++ + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (var stream = File.Create(path)) encoder.Save(stream);
        return path;
    }

    void InsertImageFromFile(DocTab tab, string file)
    {
        if (tab == null || string.IsNullOrEmpty(file) || !File.Exists(file)) return;
        if (string.IsNullOrEmpty(tab.FilePath))
        {
            if (!SaveTab(tab, true)) return;
        }
        string dir = Path.GetDirectoryName(tab.FilePath);
        string assets = Path.Combine(dir, "assets");
        Directory.CreateDirectory(assets);
        string name = Path.GetFileName(file);
        string dest = Path.Combine(assets, name);
        if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
        {
            string stem = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);
            int n = 1;
            while (File.Exists(dest)) dest = Path.Combine(assets, stem + "-" + n++ + ext);
            try { File.Copy(file, dest); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法复制图片：\n" + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        InsertImageMarkdown(tab, dest);
    }

    void InsertImageMarkdown(DocTab tab, string fullPath)
    {
        string dir = Path.GetDirectoryName(tab.FilePath);
        string rel = TextUtil.RelativePath(dir, fullPath).Replace('\\', '/');
        string alt = Path.GetFileNameWithoutExtension(fullPath);
        string insert = "![" + alt + "](" + TextUtil.EscapeUrl(rel) + ")";
        var ed = tab.Editor;
        string text = ed.Text;
        var st = ed.GetCaretState();
        int pos = OffsetFrom(text, st.Line, st.Col);
        string prefix = (pos == 0 || text[pos - 1] == '\n') ? "" : "\n\n";
        string newText = text.Insert(pos, prefix + insert);
        ed.ReplaceAll(newText, StateFrom(newText, pos + prefix.Length + insert.Length));
        ed.FocusEditor();
    }
}
