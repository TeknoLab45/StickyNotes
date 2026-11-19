using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using StickyNotes.Core;

namespace StickyNotes.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private NoteManager? _manager;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private bool _exiting;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;
    private System.Windows.Forms.ToolStripMenuItem? _syncStatusItem;

    /// <summary>Memos eşitlemesi (Ayarlar ve tray bu nesneyi kullanır). Açılışta kurulur.</summary>
    public SyncService? Sync { get; private set; }

    public static string DefaultDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNotes");

    internal static string ParseDataDir(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--data-dir" && i + 1 < args.Length) return Path.GetFullPath(args[i + 1]);
            if (args[i].StartsWith("--data-dir=", StringComparison.Ordinal)) return Path.GetFullPath(args[i].Substring(11));
        }
        return DefaultDataDir;
    }

    private static string MutexName(string dataDir)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDir).TrimEnd('\\').ToLowerInvariant()));
        return "Local\\StickyNotes_" + Convert.ToHexString(hash, 0, 8);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // GPU/sanal ekran kartı olmayan makinelerde boş pencere çizilmesini önlemek için yazılım render.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var dataDir = ParseDataDir(e.Args);
        Directory.CreateDirectory(dataDir);
        AppLog.Init(dataDir);

        _mutex = new Mutex(true, MutexName(dataDir), out var createdNew);
        if (!createdNew)
        {
            AppLog.Info("İkinci örnek engellendi, çıkılıyor");
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        SessionEnding += (_, _) => ExitApp(); // logoff/shutdown: bekleyen kayıtları yaz
        DispatcherUnhandledException += (_, ex) =>
        {
            AppLog.Error("Yakalanmamış UI hatası", ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => AppLog.Error("Yakalanmamış hata", ex.ExceptionObject as Exception);

        try
        {
            AppLog.Info("Başlıyor, data-dir=" + dataDir);
            var repo = new NoteRepository(Path.Combine(dataDir, "notes.db"));
            _manager = new NoteManager(repo);
            CreateTray();
            _hotkeys = new HotkeyManager();
            _manager.SettingsChanged += _ => RegisterHotkeys();
            _manager.HotkeysSuspendedChanged += RegisterHotkeys;
            RegisterHotkeys();
            _manager.Startup();
            Sync = new SyncService(_manager, ShowBalloon);
            Sync.StatusChanged += UpdateSyncStatus;
            Sync.Start();
            UpdateSyncStatus();
            _ = Sync.RunAsync(resume: false);   // açılış turu (ayarlar kapalıysa hiçbir şey yapmaz)
            // Test/ekran görüntüsü yardımcısı: tray menüsünü ekranda açar (gizli simge alanını denemeden).
            if (e.Args.Contains("--show-tray-menu"))
                Dispatcher.BeginInvoke(() =>
                {
                    var a = SystemParameters.WorkArea;
                    _trayMenu?.Show(new System.Drawing.Point((int)(a.Right - 260), (int)(a.Bottom - 340)));
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        catch (Exception ex)
        {
            AppLog.Error("Başlatma hatası", ex);
            MessageBox.Show("Sticky Notes başlatılamadı:\n" + ex.Message, "Sticky Notes", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApp();
        }
    }

    private HotkeyManager? _hotkeys;
    private readonly System.Collections.Generic.HashSet<string> _warnedHotkeys = new();

    /// <summary>Global kısayolları (yeniden) kaydeder. Çakışırsa uygulama çökmez: log + tray'de tek seferlik uyarı.</summary>
    private void RegisterHotkeys()
    {
        if (_hotkeys == null || _manager == null) return;
        _hotkeys.UnregisterAll();
        if (_manager.HotkeysSuspended) return;
        var s = _manager.Settings;
        var items = new (int Id, string Name, string Combo, Action Run)[]
        {
            (1, "Tümünü gizle", s.HotkeyHideAll, () => _manager.HideAll()),
            (2, "Tümünü göster", s.HotkeyShowAll, () => _manager.ShowAll()),
            (3, "Explorer", s.HotkeyExplorer, () => _manager.ShowExplorer()),
            (4, "Yeni not", s.HotkeyNewNote, () => _manager.NewNote()),
        };
        var failed = new System.Collections.Generic.List<string>();
        foreach (var it in items)
        {
            if (_hotkeys.Register(it.Id, it.Combo, it.Run, out var err)) continue;
            AppLog.Error($"Kısayol kaydedilemedi: {it.Name} ({it.Combo}) — {err}", null);
            if (_warnedHotkeys.Add(it.Name + "|" + it.Combo))
                failed.Add($"{it.Combo} ({it.Name})");
        }
        if (failed.Count > 0)
        {
            var text = "Şu kısayollar kullanılamıyor (başka bir program tutuyor olabilir): " + string.Join(", ", failed) +
                       ". Ayarlar'dan değiştirebilirsiniz.";
            // Tray simgesi ekrana yerleştikten sonra göster (açılışta hemen çağrılırsa Windows yutabiliyor)
            Dispatcher.BeginInvoke(() => _tray?.ShowBalloonTip(10000, "Sticky Notes", text, System.Windows.Forms.ToolTipIcon.Warning),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private void CreateTray()
    {
        var res = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        _trayIcon = res != null ? new System.Drawing.Icon(res.Stream) : System.Drawing.SystemIcons.Application;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Yeni not", null, (_, _) => Dispatcher.Invoke(() => _manager!.NewNote()));
        menu.Items.Add("Tüm notları göster", null, (_, _) => Dispatcher.Invoke(() => _manager!.ShowAll()));
        menu.Items.Add("Tüm notları gizle", null, (_, _) => Dispatcher.Invoke(() => _manager!.HideAll()));
        menu.Items.Add("Explorer", null, (_, _) => Dispatcher.Invoke(() => _manager!.ShowExplorer()));
        menu.Items.Add("Ayarlar…", null, (_, _) => Dispatcher.Invoke(() => _manager!.ShowSettings()));
        menu.Items.Add("Tüm notları opak yap", null, (_, _) => Dispatcher.Invoke(() => _manager!.MakeAllOpaque()));
        menu.Items.Add("Çöp kutusu", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _manager!.ShowExplorer();
            (Current.Windows.OfType<ExplorerWindow>().FirstOrDefault())?.ShowTrash();
        }));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _syncStatusItem = new System.Windows.Forms.ToolStripMenuItem("Memos: kapalı") { Enabled = false };
        menu.Items.Add(_syncStatusItem);
        menu.Items.Add("Şimdi eşitle", null, (_, _) => Dispatcher.Invoke(RunSyncNow));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => Dispatcher.Invoke(ExitApp));

        ThemedMenuRenderer.Apply(menu);
        _trayMenu = menu;

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Sticky Notes",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => _manager!.NewNote());
    }

    private void RunSyncNow() => _ = Sync?.RunNowAsync();

    private void UpdateSyncStatus()
    {
        if (_syncStatusItem != null && Sync != null) _syncStatusItem.Text = Sync.StatusText();
    }

    /// <summary>Tek seferlik tray uyarısı (SyncService yineleri eler).</summary>
    private void ShowBalloon(string title, string text) =>
        _tray?.ShowBalloonTip(10000, title, text, System.Windows.Forms.ToolTipIcon.Warning);

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        try { Sync?.Stop(); } catch (Exception ex) { AppLog.Error("Eşitleme durdurma hatası", ex); }
        try { _hotkeys?.Dispose(); _hotkeys = null; } catch (Exception ex) { AppLog.Error("Kısayol temizleme hatası", ex); }
        try { _manager?.Shutdown(); } catch (Exception ex) { AppLog.Error("Shutdown hatası", ex); }
        if (_tray != null)
        {
            _tray.Visible = false; // hayalet ikon kalmasın
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _tray = null;
        }
        _trayIcon?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        AppLog.Info("Çıkış");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
