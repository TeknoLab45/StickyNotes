using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using StickyNotes.Core;
using StickyNotes.Core.Sync;

namespace StickyNotes.App;

/// <summary>
/// Memos eşitlemesinin arayüz tarafı: zamanlayıcı, kayıttan sonra tetikleme (5 sn), "Şimdi eşitle",
/// tray durum metni ve tek seferlik uyarılar. Eşitleme mantığı Core'daki <see cref="SyncEngine"/>'de.
/// Tüm çağrılar UI iş parçacığında olur; SQLite erişimi de orada kalır.
/// </summary>
public sealed class SyncService
{
    private readonly NoteManager _manager;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _interval;
    private readonly DispatcherTimer _debounce;
    private readonly HashSet<string> _warned = new();
    private bool _running;
    private bool _again;
    private bool _muted;    // iç yazımlar (flush, uzak değişiklik uygulama) yeni tur tetiklemesin
    private bool _missed;   // tur sürerken kullanıcı değiştirdi → tur bitince bir kez daha eşitle

    public SyncService(NoteManager manager, Action<string, string> balloon)
    {
        _manager = manager;
        Balloon = balloon;
        _interval = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _interval.Tick += (_, _) => _ = RunAsync(resume: false);
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RunAsync(resume: false); };
        _manager.NotesChanged += OnNotesChanged;
    }

    /// <summary>Tek seferlik uyarılar için (tray balonu). Başlık, metin.</summary>
    public Action<string, string> Balloon { get; }

    public event Action? StatusChanged;

    public DateTime? LastSyncedAt { get; private set; }
    public SyncResult? LastResult { get; private set; }

    public bool IsRunning => _running;

    public void Start() => Reconfigure();

    public void Stop()
    {
        _interval.Stop();
        _debounce.Stop();
    }

    /// <summary>Ayarlar değişti: zamanlayıcıyı yeni aralığa göre kur.</summary>
    public void Reconfigure()
    {
        var s = _manager.Repo.LoadSyncSettings();
        _interval.Interval = TimeSpan.FromMinutes(s.IntervalMin);
        if (s.Enabled && s.Space.Length > 0) _interval.Start();
        else _interval.Stop();
        StatusChanged?.Invoke();
    }

    private void OnNotesChanged()
    {
        if (_muted) return;
        if (_running) _missed = true;
        else ScheduleSoon();
    }

    private void Mute(Action action)
    {
        var prev = _muted;
        _muted = true;
        try { action(); }
        finally { _muted = prev; }
    }

    /// <summary>Kayıttan kısa süre sonra bir tur (yazma sırasında tur açılmasın diye 5 sn bekler).</summary>
    private void ScheduleSoon()
    {
        var s = _manager.Repo.LoadSyncSettings();
        if (!s.Enabled || s.Space.Length == 0) return;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>"Şimdi eşitle" ya da "Yine de eşitle" (resume: duraklatmayı yok sayar).</summary>
    public Task RunNowAsync(bool resume = false) => RunAsync(resume);

    public async Task RunAsync(bool resume)
    {
        if (_running)
        {
            _again = true;   // tur sürerken gelen tetik, tur bitince bir kez daha çalışır
            return;
        }
        var s = _manager.Repo.LoadSyncSettings();
        if (!s.Enabled || s.Space.Length == 0 || (s.Paused && !resume))
        {
            StatusChanged?.Invoke();   // kapalı ya da duraklatılmış: sessizce atla
            return;
        }
        var token = TokenStore.Unprotect(s.TokenEnc);
        if (token == null)
        {
            Remember(new SyncResult(SyncStatus.NotConfigured, "Token girilmemiş. Ayarlar'dan token girin."));
            return;
        }

        _running = true;
        StatusChanged?.Invoke();
        try
        {
            Mute(_manager.FlushAll);   // açık notlardaki bekleyen yazımlar önce diske
            var client = new MemosClient(_http, s.Url, token, s.ApiRoot);
            var engine = new SyncEngine(client, _manager.Repo);
            var result = await engine.RunOnceAsync(s, resume);

            // Algılanan API kökü ve duraklatma durumu kalıcı olsun (sonraki açılışta yeniden denenmesin)
            var current = _manager.Repo.LoadSyncSettings();
            var next = current with
            {
                ApiRoot = client.ApiRoot ?? current.ApiRoot,
                Paused = result.Status == SyncStatus.Paused,
            };
            if (next != current) _manager.Repo.SaveSyncSettings(next);

            if (result.Status is SyncStatus.Ok) LastSyncedAt = DateTime.Now;
            Remember(result);
            AppLog.Info($"Eşitleme: {result.Status} — {result.Message} (çekilen {result.Pulled}, gönderilen {result.Pushed}, yeni {result.Created}, çöp {result.Trashed}, çakışma {result.Conflicts})");

            Mute(_manager.RefreshAfterSync);
        }
        catch (Exception ex)
        {
            AppLog.Error("Eşitleme turu beklenmeyen hata", ex);
            Remember(new SyncResult(SyncStatus.Paused, "Eşitleme beklenmeyen bir hatayla durdu. Ayrıntı: sticky-notes günlüğü."));
        }
        finally
        {
            _running = false;
            StatusChanged?.Invoke();
        }

        if (_missed)
        {
            _missed = false;
            ScheduleSoon();
        }
        if (_again)
        {
            _again = false;
            await RunAsync(resume: false);
        }
    }

    /// <summary>Son sonucu saklar; ilk kez görülen sorunlarda tray'de tek seferlik uyarı verir.</summary>
    private void Remember(SyncResult r)
    {
        LastResult = r;
        if (r.Status == SyncStatus.Ok)
        {
            _warned.Clear();   // sorun giderildi: bir sonraki sorun yeniden bildirilir
            if (r.Conflicts > 0 && _warned.Add("conflict:" + r.Conflicts))
                Balloon("Sticky Notes", $"{r.Conflicts} çakışma kopyası oluşturuldu. Yerel sürüm korundu.");
        }
        else if (_warned.Add(r.Message))
        {
            Balloon("Sticky Notes · Memos", r.Message);
        }
        StatusChanged?.Invoke();
    }

    /// <summary>Tray ve Ayarlar için kısa durum metni.</summary>
    public string StatusText()
    {
        var s = _manager.Repo.LoadSyncSettings();
        if (!s.Enabled || s.Space.Length == 0) return "Memos: kapalı";
        if (_running) return "Memos: eşitleniyor…";
        if (s.Paused) return "Memos: duraklatıldı";
        if (LastResult is { Status: SyncStatus.Offline })
            return $"Çevrimdışı · {PendingCount(s)} değişiklik bekliyor";
        if (LastResult is { Status: SyncStatus.NotConfigured } r) return r.Message;
        return LastSyncedAt is { } t ? $"Son eşitleme {t:HH:mm}" : "Memos: henüz eşitlenmedi";
    }

    /// <summary>Henüz sunucuya eşlenmemiş yerel notlar (çevrimdışıyken bekleyen değişiklik sayısı).</summary>
    private int PendingCount(SyncSettings s)
    {
        var mapped = _manager.Repo.ListSyncState().Select(r => r.NoteId).ToHashSet();
        return _manager.Repo.ListForSync().Count(n => !mapped.Contains(n.Id));
    }
}
