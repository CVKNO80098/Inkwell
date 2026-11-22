using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Inkwell.App;

public sealed partial class MainWindow
{
    sealed class DocTab
    {
        public EditorView Editor;
        public string FilePath;
        public string Title = "未命名";
        public int SavedRevision = -1;
        public bool Dirty;
        public double PreviewOffset;
        public string BackupPath;
        public Border TabRow;
        public TextBlock TabTitle;
        public Border DirtyDot;
        public Button TabClose;

        public string DisplayTitle => !string.IsNullOrEmpty(FilePath) ? Path.GetFileName(FilePath) : Title;
    }

    int _untitledCounter;
    bool _syncingScroll;

    DocTab ActiveTab => _active >= 0 && _active < _tabs.Count ? _tabs[_active] : null;
    EditorView ActiveEditor => ActiveTab?.Editor;

    // ------------------------------------------------------------------ documents

    public void NewDocument()
    {
        _untitledCounter++;
        CreateTab(null, "", "未命名" + (_untitledCounter > 1 ? " " + _untitledCounter : ""));
    }

    DocTab CreateTab(string filePath, string text, string title = null, bool dirty = false, string backupPath = null)
    {
        var editor = new EditorView(_theme, _settings);
        editor.SinglePage = _settings.ViewMode == "Single";
        editor.SetText(text ?? "", CaretState.At(0, 0));

        var tab = new DocTab
        {
            Editor = editor,
            FilePath = filePath,
            Title = title ?? (!string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "未命名"),
            SavedRevision = dirty ? -1 : editor.Revision,
            Dirty = dirty,
            BackupPath = backupPath
        };

        editor.TextEdited += (s, e) => OnEditorTextEdited(tab);
        editor.CaretMoved += (s, e) => OnEditorCaretMoved(tab);
        editor.Scrolled += (s, e) => OnEditorScrolled(tab);
        editor.FilesDropped += files => HandleDroppedFiles(tab, files);
        editor.PasteImageHandler = () => InsertImageFromClipboard(tab);
        editor.OpenLinkHandler = url => { OpenLink(url); return true; };

        editor.Visibility = Visibility.Collapsed;
        _editorHost.Children.Insert(0, editor);
        _tabs.Add(tab);
        _welcome.Visibility = Visibility.Collapsed;

        ActivateTab(_tabs.Count - 1);
        editor.FocusEditor();
        return tab;
    }

    public void ActivateTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var previous = ActiveTab;
        if (previous != null) previous.PreviewOffset = _lastPreviewOffset;

        _active = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Editor.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
        var tab = _tabs[index];
        RebuildTabStrip();
        UpdateTitleBar();
        RefreshStatus();
        RebuildWelcome();
        _sidebar.SetActiveFile(tab.FilePath);
        _settings.ActiveTab = index;
        ScheduleSettingsSave();

        RenderPreview();
        RebuildOutline();
        tab.Editor.EnsureStyled();
    }

    public void OpenFile(string path, bool activate = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string full;
        try { full = Path.GetFullPath(path); } catch { return; }
        var existing = _tabs.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath) &&
            string.Equals(t.FilePath, full, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (activate) ActivateTab(_tabs.IndexOf(existing));
            return;
        }
        if (!File.Exists(full))
        {
            MessageBox.Show(this, "文件不存在：\n" + full, App.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        string text;
        try { text = File.ReadAllText(full); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法打开文件：\n" + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        CreateTab(full, text);
        _settings.AddRecent(full);
        ScheduleSettingsSave();
        _sidebar.SetActiveFile(full);
        if (string.IsNullOrEmpty(_sidebar.Folder))
        {
            try { SetSidebarFolder(Path.GetDirectoryName(full)); } catch { }
        }
        if (activate) _tabs[_tabs.Count - 1].Editor.FocusEditor();
    }

    public void OpenFileDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title = "打开 Markdown 文件",
            Filter = "Markdown 文件|*.md;*.markdown;*.mdown;*.mkd;*.mdx|文本文件|*.txt|所有文件|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            dlg.InitialDirectory = _settings.LastFolder;
        if (dlg.ShowDialog(this) == true)
        {
            foreach (var file in dlg.FileNames)
            {
                OpenFile(file);
                _settings.LastFolder = Path.GetDirectoryName(file);
            }
            ScheduleSettingsSave();
        }
    }

    public void PickFolder()
    {
        using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
        {
            dlg.Description = "选择要浏览的文件夹";
            dlg.ShowNewFolderButton = true;
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
                dlg.SelectedPath = _settings.LastFolder;
            var owner = new System.Windows.Forms.NativeWindow();
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
            {
                _settings.LastFolder = dlg.SelectedPath;
                _sidebar.SetFolder(dlg.SelectedPath);
                ApplySidebarVisibility(true);
                ScheduleSettingsSave();
            }
        }
    }

    void OnEditorTextEdited(DocTab tab)
    {
        tab.Dirty = tab.Editor.Revision != tab.SavedRevision;
        UpdateTabUi(tab);
        if (tab != ActiveTab) return;
        UpdateTitleBar();
        ScheduleStatusRefresh();
        SchedulePreviewSoon();
        _outlineTimer.Stop();
        _outlineTimer.Start();
    }

    void OnEditorCaretMoved(DocTab tab)
    {
        if (tab != ActiveTab) return;
        ScheduleStatusRefresh();
        if (_settings.Typewriter && tab.Editor.Visibility == Visibility.Visible) TypewriterScroll(tab);
    }

    void OnEditorScrolled(DocTab tab)
    {
        if (tab != ActiveTab) return;
        if (_settings.ViewMode != "Split") return;
        SyncPreviewToEditor(tab);
    }

    void TypewriterScroll(DocTab tab)
    {
        try
        {
            var caret = tab.Editor.Rtb.CaretPosition;
            var rect = caret.GetCharacterRect(LogicalDirection.Forward);
            double target = tab.Editor.VerticalOffset + rect.Top - tab.Editor.ViewportHeight * 0.42;
            tab.Editor.ScrollToVerticalOffset(Math.Max(0, target));
        }
        catch { }
    }

    void UpdateTabUi(DocTab tab)
    {
        if (tab.TabTitle != null) tab.TabTitle.Text = tab.DisplayTitle;
        if (tab.DirtyDot != null) tab.DirtyDot.Visibility = tab.Dirty ? Visibility.Visible : Visibility.Collapsed;
        if (tab.TabRow != null) tab.TabRow.ToolTip = tab.FilePath ?? tab.DisplayTitle;
    }

    void UpdateTitleBar()
    {
        var tab = ActiveTab;
        if (tab == null)
        {
            _titleDoc.Text = "";
            Title = App.Name;
            return;
        }
        string suffix = tab.Dirty ? " •" : "";
        _titleDoc.Text = tab.DisplayTitle;
        Title = tab.DisplayTitle + suffix + " — " + App.Name;
    }

    public void DemoMenu()
    {
        MenuPanel.CloseAll();
        MenuPanel.Show(_menuButton, _theme, BuildMenus());
    }
    public void SetSidebarFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder)) return;
        _sidebar.SetFolder(folder);
        ApplySidebarVisibility(true);
    }

    public void DemoWheelProbe(int notches)
    {
        var ed = ActiveEditor;
        if (ed == null) { App.Log("wheel probe: no editor"); return; }
        App.Log(string.Format("wheel before off={0:F1} extent={1:F1} vp={2:F1}", ed.VerticalOffset, ed.ScrollableHeight, ed.ViewportHeight));
        ed.DemoWheel(notches);
        App.Log(string.Format("wheel after off={0:F1}", ed.VerticalOffset));
        var pv = PreviewScroller();
        if (pv != null)
        {
            pv.ScrollToVerticalOffset(pv.VerticalOffset + 140);
            Dispatcher.BeginInvoke(new Action(() =>
                App.Log(string.Format("after preview scroll: preview={0:F1} editor={1:F1}", pv.VerticalOffset, ed.VerticalOffset))),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    // ------------------------------------------------------------------ save

    bool SaveActive(bool saveAs)
    {
        var tab = ActiveTab;
        return tab != null && SaveTab(tab, saveAs);
    }

    bool SaveTab(DocTab tab, bool saveAs)
    {
        if (tab == null) return false;
        string path = tab.FilePath;
        if (saveAs || string.IsNullOrEmpty(path))
        {
            var dlg = new SaveFileDialog
            {
                Title = "保存 Markdown 文件",
                Filter = "Markdown 文件|*.md|所有文件|*.*",
                FileName = string.IsNullOrEmpty(tab.FilePath) ? (tab.DisplayTitle.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? tab.DisplayTitle : tab.DisplayTitle + ".md") : Path.GetFileName(tab.FilePath),
                AddExtension = true,
                DefaultExt = ".md",
                OverwritePrompt = true
            };
            if (!string.IsNullOrEmpty(tab.FilePath))
                dlg.InitialDirectory = Path.GetDirectoryName(tab.FilePath);
            else if (!string.IsNullOrEmpty(_settings.LastFolder))
                dlg.InitialDirectory = _settings.LastFolder;
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
            if (!Path.HasExtension(path)) path += ".md";
        }

        string text = tab.Editor.Text;
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：\n" + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        tab.FilePath = path;
        tab.SavedRevision = tab.Editor.Revision;
        tab.Dirty = false;
        tab.Title = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(tab.BackupPath))
        {
            BackupService.Delete(tab.BackupPath);
            tab.BackupPath = null;
        }
        _settings.AddRecent(path);
        _settings.LastFolder = Path.GetDirectoryName(path);
        ScheduleSettingsSave();
        RebuildTabStrip();
        UpdateTabUi(tab);
        UpdateTitleBar();
        RefreshStatus();
        _sidebar.SetActiveFile(path);
        _sidebar.Refresh();
        return true;
    }

    // ------------------------------------------------------------------ close

    void CloseTab(DocTab tab)
    {
        if (tab == null) return;
        if (tab.Dirty)
        {
            var result = MessageBox.Show(this,
                "是否保存对“" + tab.DisplayTitle + "”的更改？",
                App.Name, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes)
            {
                if (!SaveTab(tab, false)) return;
            }
            else
            {
                if (!string.IsNullOrEmpty(tab.BackupPath)) BackupService.Delete(tab.BackupPath);
            }
        }
        else if (!string.IsNullOrEmpty(tab.BackupPath))
        {
            BackupService.Delete(tab.BackupPath);
        }

        int index = _tabs.IndexOf(tab);
        if (index < 0) return;
        _tabs.RemoveAt(index);
        _editorHost.Children.Remove(tab.Editor);
        if (_tabs.Count == 0)
        {
            _active = -1;
            RebuildTabStrip();
            UpdateTitleBar();
            RefreshStatus();
            RebuildWelcome();
            _previewViewer.Document = null;
            _sidebar.SetActiveFile(null);
            _sidebar.SetOutline(new List<OutlineItem>());
        }
        else
        {
            if (index <= _active) _active = Math.Max(0, _active - (index < _active ? 1 : 0));
            if (_active >= _tabs.Count) _active = _tabs.Count - 1;
            if (index == _active && index < _tabs.Count) { }
            ActivateTab(Math.Min(_active, _tabs.Count - 1));
        }
        ScheduleSettingsSave();
    }

    void CloseActiveTab()
    {
        var tab = ActiveTab;
        if (tab != null) CloseTab(tab);
    }

    void NextTab(int direction)
    {
        if (_tabs.Count < 2) return;
        int idx = _active + direction;
        if (idx < 0) idx = _tabs.Count - 1;
        if (idx >= _tabs.Count) idx = 0;
        ActivateTab(idx);
    }

    // ------------------------------------------------------------------ autosave / recovery

    void Autosave()
    {
        if (!_settings.Autosave) return;
        foreach (var tab in _tabs)
        {
            if (!tab.Dirty) continue;
            string text = tab.Editor.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (string.IsNullOrEmpty(tab.BackupPath)) tab.BackupPath = BackupService.NewBackupPath();
            BackupService.Save(tab.BackupPath, tab.FilePath, tab.DisplayTitle, text);
        }
    }

    void CheckRecovery()
    {
        var entries = BackupService.Scan();
        if (entries.Count == 0) return;
        var selected = Dialogs.Recover(this, _theme, entries);
        if (selected == null) return;
        if (selected.Count == 0)
        {
            foreach (var e in entries) BackupService.Delete(e.BackupPath);
            return;
        }
        foreach (var entry in selected)
        {
            var already = _tabs.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath) &&
                string.Equals(t.FilePath, entry.OriginalPath, StringComparison.OrdinalIgnoreCase));
            if (already != null)
            {
                already.Editor.SetText(entry.Text, CaretState.At(0, 0));
                already.Dirty = true;
                UpdateTabUi(already);
                continue;
            }
            CreateTab(string.IsNullOrEmpty(entry.OriginalPath) ? null : entry.OriginalPath,
                entry.Text, null, dirty: true, backupPath: entry.BackupPath);
        }
        foreach (var entry in entries)
        {
            if (!selected.Contains(entry)) BackupService.Delete(entry.BackupPath);
        }
    }

    // ------------------------------------------------------------------ misc

    void JumpToLine(int line)
    {
        var tab = ActiveTab;
        if (tab == null) return;
        var state = CaretState.At(Math.Max(0, line - 1), 0);
        tab.Editor.SelectRange(state, state);
        tab.Editor.ScrollCaretIntoView();
        tab.Editor.FocusEditor();
    }

    void HandleDroppedFiles(DocTab tab, string[] files)
    {
        if (files == null) return;
        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file)) continue;
            if (TextUtil.IsImageFile(file)) InsertImageFromFile(tab, file);
            else if (TextUtil.IsMarkdownFile(file) || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) OpenFile(file);
        }
    }

    void OpenLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            if (url.StartsWith("#", StringComparison.Ordinal)) return;
            if (Uri.TryCreate(url, UriKind.Absolute, out var abs) && (abs.Scheme == "http" || abs.Scheme == "https" || abs.Scheme == "mailto"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }
            var tab = ActiveTab;
            string baseDir = tab?.FilePath != null ? Path.GetDirectoryName(tab.FilePath) : _settings.LastFolder;
            if (string.IsNullOrEmpty(baseDir)) return;
            string path = url;
            int hash = path.IndexOf('#');
            if (hash >= 0) path = path.Substring(0, hash);
            path = Uri.UnescapeDataString(path);
            string full = Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path.Replace('/', '\\'));
            if (File.Exists(full)) OpenFile(full);
        }
        catch { }
    }
}
