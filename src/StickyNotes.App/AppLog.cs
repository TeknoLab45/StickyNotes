using System;
using System.IO;

namespace StickyNotes.App;

/// <summary>Basit dosya logu: &lt;data-dir&gt;\logs\app.log</summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Init(string dataDir)
    {
        try
        {
            var dir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "app.log");
            if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000)
                File.Move(_path, _path + ".old", true);
        }
        catch { _path = null; }
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : msg + " :: " + ex);

    private static void Write(string level, string msg)
    {
        if (_path == null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
