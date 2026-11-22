using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Inkwell.App;

[DataContract]
public sealed class AppSettings
{
    [DataMember] public string Theme = "Paper";
    [DataMember] public string EditorFont = "Microsoft YaHei UI";
    [DataMember] public double EditorFontSize = 16.5;
    [DataMember] public double PreviewFontSize = 16.5;
    [DataMember] public double EditorWidth = 760;
    [DataMember] public bool ShowSidebar = true;
    [DataMember] public bool ShowToolbar = true;
    [DataMember] public string ViewMode = "Split";
    [DataMember] public bool Typewriter = false;
    [DataMember] public bool Autosave = true;
    [DataMember] public List<string> Recent = new List<string>();
    [DataMember] public List<string> SessionFiles = new List<string>();
    [DataMember] public int ActiveTab = 0;
    [DataMember] public double WindowWidth = 1320;
    [DataMember] public double WindowHeight = 860;
    [DataMember] public double WindowLeft = 0;
    [DataMember] public double WindowTop = 0;
    [DataMember] public bool HasWindowPos = false;
    [DataMember] public bool Maximized = false;
    [DataMember] public string LastFolder = "";
    [DataMember] public bool ShowStatusBar = true;
    [DataMember] public bool SpellCheck = false;

    private static string _dataDir;

    /// <summary>Portable mode: a "portable.txt" file next to the executable keeps data in .\data.</summary>
    public static bool IsPortable
    {
        get
        {
            try { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.txt")); }
            catch { return false; }
        }
    }

    public static string DataDir
    {
        get
        {
            if (_dataDir != null) return _dataDir;
            string dir = IsPortable
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Inkwell");
            try { Directory.CreateDirectory(dir); } catch { }
            _dataDir = dir;
            return dir;
        }
    }

    public static string SettingsPath => Path.Combine(DataDir, "settings.json");
    public static string BackupDir
    {
        get
        {
            string d = Path.Combine(DataDir, "backups");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            using (var fs = File.OpenRead(SettingsPath))
            {
                var ser = new DataContractJsonSerializer(typeof(AppSettings));
                var s = (AppSettings)ser.ReadObject(fs);
                if (s == null) return new AppSettings();
                s.Normalize();
                return s;
            }
        }
        catch
        {
            try { File.Copy(SettingsPath, SettingsPath + ".bad", true); } catch { }
            return new AppSettings();
        }
    }

    public void Normalize()
    {
        if (string.IsNullOrEmpty(Theme)) Theme = "Paper";
        if (string.IsNullOrEmpty(EditorFont)) EditorFont = "Microsoft YaHei UI";
        if (EditorFontSize < 8 || EditorFontSize > 42) EditorFontSize = 16.5;
        if (PreviewFontSize < 8 || PreviewFontSize > 42) PreviewFontSize = 16.5;
        if (EditorWidth < 480 || EditorWidth > 1600) EditorWidth = 760;
        if (Recent == null) Recent = new List<string>();
        if (SessionFiles == null) SessionFiles = new List<string>();
        if (ViewMode != "Split" && ViewMode != "Editor" && ViewMode != "Preview" && ViewMode != "Single") ViewMode = "Split";
        if (WindowWidth < 600) WindowWidth = 1320;
        if (WindowHeight < 400) WindowHeight = 860;
        if (double.IsNaN(WindowLeft) || double.IsNaN(WindowTop)) { WindowLeft = 0; WindowTop = 0; HasWindowPos = false; }
        if (LastFolder == null) LastFolder = "";
    }

    public void Save()
    {
        try
        {
            using (var fs = File.Create(SettingsPath))
            {
                var ser = new DataContractJsonSerializer(typeof(AppSettings));
                ser.WriteObject(fs, this);
            }
        }
        catch { }
    }

    public void AddRecent(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, path);
        while (Recent.Count > 15) Recent.RemoveAt(Recent.Count - 1);
    }
}
