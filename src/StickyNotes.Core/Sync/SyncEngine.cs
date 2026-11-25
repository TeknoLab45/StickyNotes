namespace StickyNotes.Core.Sync;

/// <summary>Yerel ↔ Memos eşlemesi. SyncedHash: son eşitlenen içeriğin parmak izi (null = taban bilinmiyor).</summary>
public sealed record SyncRecord(
    string NoteId,
    string MemoName,
    string Space,
    string Creator,
    string CreatorName,
    bool ReadOnly,
    DateTime? RemoteUpdateTime,
    string? SyncedHash,
    DateTime? SyncedAt);

public enum SyncStatus { Ok, Offline, Paused, NotConfigured }

public sealed record SyncResult(SyncStatus Status, string Message, int Pulled = 0, int Pushed = 0, int Created = 0, int Trashed = 0, int Conflicts = 0);

/// <summary>
/// Tek eşitleme turu. Kurallar: çakışmada yerel kazanır, sunucu sürümü "(çakışma kopyası)" olarak yeni not olur;
/// çöpe atma sunucuya gitmez; kalıcı silme memoyu sunucudan siler (başarısızsa sonraki turda yeniden); sunucuda kaybolan memolar yalnızca 404 kesinleşirse çöpe taşınır;
/// bir turda eşlenmiş notların hem 5'ten fazlası hem %30'undan fazlası kaybolursa hiçbir şey yazılmaz (sigorta).
/// Uzak yazmalar her başarılı istekten hemen sonra eşleme tablosuna işlenir: yarıda kesilen tur kaldığı yerden devam eder.
/// </summary>
public sealed class SyncEngine
{
    public const string ConflictLabel = "(çakışma kopyası)";
    private const int MissingAbsoluteLimit = 5;
    private const double MissingRatioLimit = 0.30;

    private readonly IMemosApi _api;
    private readonly NoteRepository _repo;
    private readonly Func<DateTime> _now;
    private readonly Func<string, bool>? _isBusy;
    private readonly Action<string>? _onRemoteApplied;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="isBusy">Not şu an düzenleniyorsa true (odakta ya da kaydedilmemiş yazı var). Bu notlara uzak değişiklik uygulanmaz, sonraki turda denenir.</param>
    /// <param name="onRemoteApplied">Uzak içerik yerel nota yazıldıktan hemen sonra çağrılır (açık pencere aynı anda yenilensin diye).</param>
    public SyncEngine(IMemosApi api, NoteRepository repo, Func<DateTime>? now = null,
        Func<string, bool>? isBusy = null, Action<string>? onRemoteApplied = null)
    {
        _api = api;
        _repo = repo;
        _now = now ?? (() => DateTime.UtcNow);
        _isBusy = isBusy;
        _onRemoteApplied = onRemoteApplied;
    }

    /// <summary>Bir tur çalıştırır. resume=true duraklatmayı yok sayar ("Yine de eşitle").</summary>
    public async Task<SyncResult> RunOnceAsync(SyncSettings s, bool resume = false, CancellationToken ct = default)
    {
        if (!s.Enabled || s.Space.Length == 0)
            return new SyncResult(SyncStatus.NotConfigured, "Eşitleme kapalı ya da Space seçilmedi.");
        if (s.Paused && !resume)
            return new SyncResult(SyncStatus.Paused, "Eşitleme duraklatıldı.");

        await _gate.WaitAsync(ct);
        try
        {
            return await RunLockedAsync(s, ct);
        }
        catch (MemosOfflineException e) { return new SyncResult(SyncStatus.Offline, e.Message); }
        catch (MemosException e) { return new SyncResult(SyncStatus.Paused, e.Message); }
        finally { _gate.Release(); }
    }

    private sealed class Counters
    {
        public int Pulled, Pushed, Created, Trashed, Conflicts, Forbidden, Deferred, Deleted, DeleteFailed, DeleteForbidden;
    }

    private async Task<SyncResult> RunLockedAsync(SyncSettings s, CancellationToken ct)
    {
        // 1) Ön kontrol: Space yoksa (silinmiş ya da üye değil) hiçbir şey yazılmaz.
        var space = await _api.GetSpaceAsync(s.Space, ct);
        if (space is null)
        {
            var label = s.SpaceTitle.Length > 0 ? s.SpaceTitle : s.Space;
            return new SyncResult(SyncStatus.Paused, $"'{label}' alanı bulunamadı, yerel notlar korunuyor.");
        }
        var me = await _api.GetMeAsync(ct);
        var isSpaceAdmin = space.CurrentUserRole is "ADMIN" or "HOST";

        var remote = new List<MemoDto>();
        remote.AddRange(await _api.ListMemosAsync(s.Space, "NORMAL", ct));
        remote.AddRange(await _api.ListMemosAsync(s.Space, "ARCHIVED", ct));
        var remoteNames = remote.Select(m => m.Name).ToHashSet();

        var all = _repo.ListSyncState();
        var inSpace = all.Where(r => r.Space == s.Space).ToList();
        var byMemo = inSpace.ToDictionary(r => r.MemoName);
        var c = new Counters();
        var creatorNames = new Dictionary<string, string>();

        // 2) Sigorta ve kayıp memolar. Yalnızca 404 kesinleşirse çöp; sunucuda başka yerde duruyorsa dokunulmaz.
        // Kalıcı silinmiş notlar sayılmaz: memolarını biz sildik, "kayıp" değiller.
        var alive = _repo.ListForSync().Select(n => n.Id).ToHashSet();
        var tracked = inSpace.Where(r => alive.Contains(r.NoteId)).ToList();
        var missing = tracked.Where(r => !remoteNames.Contains(r.MemoName)).ToList();
        if (missing.Count > MissingAbsoluteLimit && missing.Count > tracked.Count * MissingRatioLimit)
            return new SyncResult(SyncStatus.Paused,
                $"Sigorta: eşlenmiş {tracked.Count} notun {missing.Count} tanesi sunucuda bulunamadı; hiçbir not çöpe atılmadı, eşitleme duraklatıldı.");
        foreach (var r in missing)
        {
            var local = _repo.Get(r.NoteId);
            if (local is null || local.IsDeleted) continue; // zaten çöpte: tekrar sorulmaz
            if (await _api.GetMemoAsync(r.MemoName, ct) is not null) continue;
            _repo.SoftDelete(r.NoteId);
            c.Trashed++;
        }

        // Kalıcı silinen notların memoları (yalnızca bu Space). Silinemeyen sırada kalır; yerele geri gelmez
        // (eşleme satırı durur, yerel not yoktur).
        await DeletePendingAsync(s, me, isSpaceAdmin, c, ct);

        // 3) Uzak memolar: yeni (eşlenmemiş) ya da eşlenmiş.
        foreach (var m in remote)
        {
            var creatorName = await CreatorNameAsync(m.Creator, creatorNames, ct);
            var readOnly = !(m.Creator == me.Name || isSpaceAdmin);

            if (byMemo.TryGetValue(m.Name, out var rec))
            {
                await SyncMappedAsync(rec with { CreatorName = creatorName }, m, readOnly, s, c, ct);
                continue;
            }

            // Eşlenmemiş uzak memo. Kimliği 32 hex ise (yerel not kimliğiyle aynı biçim) aynı UUID kullanılır.
            var hex = m.Name.StartsWith("memos/", StringComparison.Ordinal) ? m.Name[6..] : m.Name;
            var noteId = IsHex32(hex) ? Guid.ParseExact(hex, "N").ToString() : Guid.CreateVersion7().ToString();
            var existing = _repo.Get(noteId);
            var archived = m.State == "ARCHIVED";
            var remoteFp = MemoMarkdown.Fingerprint(m.Content, archived);
            if (existing is not null && _repo.ListSyncState().Any(x => x.NoteId == noteId))
            {
                noteId = Guid.CreateVersion7().ToString();
                existing = null;
            }

            if (existing is null)
            {
                var (type, content) = MemoMarkdown.ToNote(m.Content);
                _repo.InsertRemote(noteId, type, content, m.UpdateTime, archived ? _now() : null);
                c.Created++;
                c.Pulled++;
                var created = new SyncRecord(noteId, m.Name, s.Space, m.Creator, creatorName, readOnly, m.UpdateTime, remoteFp, _now());
                _repo.MarkSynced(created);
                byMemo[m.Name] = created;
            }
            else
            {
                // Aynı kimlikli yerel not var ama eşlenmemiş (ör. zaman aşımında gönderilmiş): taban bilinmiyor.
                // İçerik aynıysa eşitlenmiş say; farklıysa çakışma sayılır (yerel kazanır, sunucu sürümü kopya olur).
                var localFp = MemoMarkdown.Fingerprint(MemoMarkdown.FromNote(existing.Type, existing.Content), !readOnly && existing.IsDeleted);
                var adopt = new SyncRecord(noteId, m.Name, s.Space, m.Creator, creatorName, readOnly, m.UpdateTime,
                    localFp == remoteFp ? remoteFp : null, _now());
                _repo.MarkSynced(adopt);
                byMemo[m.Name] = adopt;
                await SyncMappedAsync(adopt, m, readOnly, s, c, ct);
            }
        }

        // 4) Eşlemesi olmayan yerel notlar (çöptekiler hariç) sunucuya gönderilir.
        var mappedNotes = _repo.ListSyncState().Select(r => r.NoteId).ToHashSet();
        foreach (var n in _repo.ListForSync())
        {
            // Not yeniden okunur: tur sürerken silinmiş olabilir.
            if (mappedNotes.Contains(n.Id) || _repo.Get(n.Id) is not { IsDeleted: false }) continue;
            try
            {
                await CreateRemoteAsync(n, s, me, creatorNames, c, ct);
            }
            catch (MemosForbiddenException)
            {
                // Bu kullanıcı bu Space'e yazamıyor: not yerelde kalır, diğerleri gönderilmeye devam eder.
                c.Forbidden++;
            }
        }

        var parts = new List<string>();
        if (c.Pulled > 0) parts.Add($"{c.Pulled} uzak değişiklik alındı");
        if (c.Pushed > 0) parts.Add($"{c.Pushed} yerel değişiklik gönderildi");
        if (c.Created > 0) parts.Add($"{c.Created} yeni not eklendi");
        if (c.Trashed > 0) parts.Add($"{c.Trashed} not çöpe taşındı");
        if (c.Conflicts > 0) parts.Add($"{c.Conflicts} çakışma kopyası");
        if (c.Deleted > 0) parts.Add($"{c.Deleted} not Memos'tan silindi");
        if (c.DeleteFailed > 0) parts.Add($"{c.DeleteFailed} not Memos'tan silinemedi (yeniden denenecek)");
        if (c.DeleteForbidden > 0) parts.Add($"{c.DeleteForbidden} not Memos'tan silinmedi (yetki yok)");
        if (c.Forbidden > 0) parts.Add($"{c.Forbidden} not gönderilemedi (yetki yok, yerelde kaldı)");
        if (c.Deferred > 0) parts.Add($"{c.Deferred} düzenlenen not sonraki turda güncellenecek");
        var msg = parts.Count == 0 ? "Eşitleme tamam: değişiklik yok." : "Eşitleme tamam: " + string.Join(", ", parts) + ".";
        return new SyncResult(SyncStatus.Ok, msg, c.Pulled, c.Pushed, c.Created, c.Trashed, c.Conflicts);
    }

    private async Task DeletePendingAsync(SyncSettings s, MemosUser me, bool isSpaceAdmin, Counters c, CancellationToken ct)
    {
        foreach (var r in _repo.ListPendingDeletes(s.Space))
        {
            try
            {
                // Yetki bu arada kalktıysa istek gönderilmez.
                if (!(r.Creator == me.Name || isSpaceAdmin)) throw new MemosForbiddenException();
                await _api.DeleteMemoAsync(r.MemoName, ct);
                _repo.ClearPendingDelete(r.NoteId);
                c.Deleted++;
            }
            catch (MemosForbiddenException)
            {
                // Salt okunur memo sunucudan silinmez: sıradan çıkar, yalnızca yerelde silinmiş kalır.
                _repo.MarkSynced(r with { ReadOnly = true });
                _repo.ClearPendingDelete(r.NoteId);
                c.DeleteForbidden++;
            }
            catch (MemosException ex) when (ex is not (MemosOfflineException or MemosUnauthorizedException))
            {
                c.DeleteFailed++; // sunucu hatası: diğer notlar eşitlenmeye devam eder
            }
        }
    }

    /// <summary>Eşlenmiş bir memo için iki tarafı karşılaştırır ve tek yönlü/çakışma kararını uygular.</summary>
    private async Task SyncMappedAsync(SyncRecord rec, MemoDto m, bool readOnly, SyncSettings s, Counters c, CancellationToken ct)
    {
        var local = _repo.Get(rec.NoteId);
        if (local is null) return; // kalıcı silinmiş: yerele geri gelmez (memo silme DeletePendingAsync'te)

        var archived = m.State == "ARCHIVED";
        var remoteFp = MemoMarkdown.Fingerprint(m.Content, archived);
        // Kullanıcı bu notu düzenliyorsa uzak değişiklik şimdilik uygulanmaz: taban değişmez, sonraki turda tekrar denenir.
        if (_isBusy?.Invoke(rec.NoteId) == true && remoteFp != rec.SyncedHash)
        {
            c.Deferred++;
            return;
        }
        var localMd = MemoMarkdown.FromNote(local.Type, local.Content);
        var localFp = MemoMarkdown.Fingerprint(localMd, !readOnly && local.IsDeleted);

        bool remoteChanged, localChanged;
        if (rec.SyncedHash is null)
        {
            if (localFp == remoteFp)
            {
                _repo.MarkSynced(rec with { ReadOnly = readOnly, RemoteUpdateTime = m.UpdateTime, SyncedHash = remoteFp, SyncedAt = _now() });
                return;
            }
            remoteChanged = true;
            localChanged = true;
        }
        else
        {
            remoteChanged = remoteFp != rec.SyncedHash;
            localChanged = localFp != rec.SyncedHash;
        }

        // Çöpteki düzenlenebilir not sunucuya gönderilmez. Taban "arşivde ve aynı içerik" değilse (programda çöpe atılmış)
        // uzak değişiklik de uygulanmaz ve taban korunur: geri yüklemede normal karşılaştırma/çakışma kuralı işler.
        // İstisna: bu arada web'de arşivlendiyse ve gönderilmemiş yerel yazı yoksa arşiv tabana alınır
        // (geri yükleme memoyu NORMAL yapsın, not yeniden çöpe düşmesin).
        if (!readOnly && local.IsDeleted && localFp != rec.SyncedHash
            && !(archived && rec.SyncedHash == MemoMarkdown.Fingerprint(localMd, false)))
        {
            _repo.MarkSynced(rec with { ReadOnly = false, RemoteUpdateTime = m.UpdateTime });
            return;
        }
        if (!readOnly && local.IsDeleted) localChanged = false;

        if (!remoteChanged && !localChanged)
        {
            _repo.MarkSynced(rec with { ReadOnly = readOnly, RemoteUpdateTime = m.UpdateTime });
            return;
        }

        if (remoteChanged && !localChanged)
        {
            ApplyRemoteToLocal(local, m, archived, readOnly);
            c.Pulled++;
            _repo.MarkSynced(rec with { ReadOnly = readOnly, RemoteUpdateTime = m.UpdateTime, SyncedHash = remoteFp, SyncedAt = _now() });
            return;
        }

        if (readOnly)
        {
            // Salt okunur not: yerel değişiklik kullanıcının kopyasına gider, asıl not sunucu içeriğine döner.
            if (localChanged) CreateCopy(localMd, c);
            ApplyRemoteToLocal(local, m, archived, readOnly: true);
            c.Pulled++;
            _repo.MarkSynced(rec with { ReadOnly = true, RemoteUpdateTime = m.UpdateTime, SyncedHash = remoteFp, SyncedAt = _now() });
            return;
        }

        // Düzenlenebilir ve yerel değişmiş: yerel gönderilir (çöpteki not buraya gelmez).
        try
        {
            var updated = await _api.UpdateMemoAsync(rec.MemoName, localMd, "NORMAL", ct);
            c.Pushed++;
            if (remoteChanged && remoteFp != localFp)
            {
                // Her iki taraf da değişmişti: sunucu sürümü kopya olarak yerelde kalır (bir kez).
                CreateCopy(m.Content, c);
            }
            _repo.MarkSynced(rec with { ReadOnly = false, RemoteUpdateTime = updated.UpdateTime, SyncedHash = localFp, SyncedAt = _now() });
        }
        catch (MemosForbiddenException)
        {
            // Yetki kalktı: salt okunura çevrilir. Yerel değişiklik kopyaya gider.
            var latest = await _api.GetMemoAsync(rec.MemoName, ct);
            if (latest is null) return;
            // Bekleme sırasında not düzenlendiyse uzak içerik uygulanmaz; sonraki turda salt okunur akışla işlenir.
            if (_isBusy?.Invoke(rec.NoteId) == true)
            {
                c.Deferred++;
                return;
            }
            // Bekleme sırasında değişmiş olabilecek güncel yerel içerikten kopya al (bayat anlık görüntü değil).
            var fresh = _repo.Get(rec.NoteId);
            if (fresh is null) return;
            CreateCopy(MemoMarkdown.FromNote(fresh.Type, fresh.Content), c);
            var latestArchived = latest.State == "ARCHIVED";
            ApplyRemoteToLocal(fresh, latest, latestArchived, readOnly: true);
            c.Pulled++;
            _repo.MarkSynced(rec with { ReadOnly = true, RemoteUpdateTime = latest.UpdateTime,
                SyncedHash = MemoMarkdown.Fingerprint(latest.Content, latestArchived), SyncedAt = _now() });
        }
    }

    /// <summary>Eşlenmemiş yerel notu sunucuda oluşturur. Kimlik = yerel UUID (tiresiz). Çakışan kimlik: sahibi ben ise eşle, değilse yeni kimlik.</summary>
    private async Task CreateRemoteAsync(Note n, SyncSettings s, MemosUser me, Dictionary<string, string> creatorNames, Counters c, CancellationToken ct)
    {
        var md = MemoMarkdown.FromNote(n.Type, n.Content);
        var fp = MemoMarkdown.Fingerprint(md, false);
        var memoId = n.Id.Replace("-", "");
        MemoDto created;
        try
        {
            created = await _api.CreateMemoAsync(memoId, s.Space, s.Visibility, md, ct);
        }
        catch (MemosAlreadyExistsException)
        {
            var existing = await _api.GetMemoAsync("memos/" + memoId, ct);
            if (existing is not null && existing.Creator == me.Name && existing.Space == s.Space)
            {
                // Önceki turda gönderilmiş (yanıt kaybolmuş): eşle; içerik farklıysa güncelle.
                created = fp == MemoMarkdown.Fingerprint(existing.Content, existing.State == "ARCHIVED")
                    ? existing
                    : await _api.UpdateMemoAsync(existing.Name, md, "NORMAL", ct);
            }
            else
            {
                created = await _api.CreateMemoAsync(Guid.CreateVersion7().ToString("N"), s.Space, s.Visibility, md, ct);
            }
        }

        var creatorName = await CreatorNameAsync(created.Creator, creatorNames, ct);
        _repo.MarkSynced(new SyncRecord(n.Id, created.Name, s.Space, created.Creator, creatorName, false, created.UpdateTime, fp, _now()));
        if (_repo.Get(n.Id) is null) _repo.QueueRemoteDelete(n.Id); // gönderilirken kalıcı silinmiş
        c.Pushed++;
        c.Created++;
    }

    /// <summary>Uzak durumu yerele uygular. Web'de geri alınan (NORMAL) memo, düzenlenebilir yerel notta çöpten çıkar.
    /// Salt okunur notta yerel gizleme korunur.</summary>
    private void ApplyRemoteToLocal(Note local, MemoDto m, bool archived, bool readOnly)
    {
        var (type, content) = MemoMarkdown.ToNote(m.Content);
        _repo.ApplyRemote(local.Id, type, content, m.UpdateTime);
        if (archived && !local.IsDeleted) _repo.SoftDelete(local.Id);
        if (!archived && !readOnly && local.IsDeleted) _repo.Restore(local.Id);
        _onRemoteApplied?.Invoke(local.Id);
    }

    /// <summary>Çakışma kopyası: metin notu, başında "(çakışma kopyası)" satırı. Yeni kimlik; sonraki turda gönderilir.</summary>
    private void CreateCopy(string markdown, Counters c)
    {
        var (type, content) = MemoMarkdown.ToNote(ConflictLabel + "\n" + markdown);
        _repo.Create(type, NoteColors.Default, content);
        c.Conflicts++;
    }

    private async Task<string> CreatorNameAsync(string creator, Dictionary<string, string> cache, CancellationToken ct)
    {
        if (creator.Length == 0) return "";
        if (cache.TryGetValue(creator, out var name)) return name;
        var user = await _api.GetUserAsync(creator, ct);
        name = user?.Username ?? creator;
        cache[creator] = name;
        return name;
    }

    private static bool IsHex32(string s) => s.Length == 32 && s.All(Uri.IsHexDigit);
}
