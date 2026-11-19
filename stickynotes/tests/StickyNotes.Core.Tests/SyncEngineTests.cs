using StickyNotes.Core;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core.Tests;

public class SyncEngineTests : IDisposable
{
    private readonly NoteRepository _repo;
    private readonly string _path;
    private readonly FakeMemos _api = new();
    private readonly SyncEngine _engine;
    private readonly SyncSettings _settings = new()
    {
        Enabled = true,
        Url = "http://memos.test",
        Space = FakeMemos.Space,
        SpaceTitle = "Ev Alanı",
        Visibility = MemosVisibility.Space,
    };

    public SyncEngineTests()
    {
        _repo = TestRepo.Create(out _path);
        _engine = new SyncEngine(_api, _repo, () => new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc));
    }

    public void Dispose() => TestRepo.Cleanup(_path);

    private Note TextNote(string text) => _repo.Create(NoteType.Text, NoteColors.Default, NoteContent.CreateText(text));

    private string Text(string id) => NoteContent.ParseText(_repo.Get(id)!.Content);

    private Task<SyncResult> Run(bool resume = false) => _engine.RunOnceAsync(_settings, resume);

    [Fact]
    public async Task FirstSync_PushesLocalNote_WithNoteIdAsMemoId()
    {
        var n = TextNote("merhaba");
        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        var memo = _api.Memos["memos/" + n.Id.Replace("-", "")];
        Assert.Equal("merhaba", memo.Content);
        Assert.Equal(FakeMemos.Space, memo.Space);
        Assert.Equal("SPACE", memo.Visibility);
        Assert.Equal(1, r.Created);
        Assert.Single(_repo.ListSyncState());
    }

    [Fact]
    public async Task FirstSync_PullsRemoteMemo_KeepingUuidWhenIdIsHex()
    {
        const string hex = "0123456789abcdef0123456789abcdef";
        _api.AddRemote("memos/" + hex, "uzak not **kalın**");

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        var localId = Guid.ParseExact(hex, "N").ToString();
        var note = _repo.Get(localId);
        Assert.NotNull(note);
        Assert.Equal("uzak not kalın", NoteContent.ParseText(note!.Content));
        Assert.Single(NoteContent.ParseMarks(note.Content));
    }

    [Fact]
    public async Task RemoteChange_AppliesLocally_WhenLocalUnchanged()
    {
        var n = TextNote("ilk");
        await Run();
        _api.RemoteEdit("memos/" + n.Id.Replace("-", ""), content: "sunucuda değişti");

        var r = await Run();

        Assert.Equal(1, r.Pulled);
        Assert.Equal("sunucuda değişti", Text(n.Id));
    }

    [Fact]
    public async Task LocalChange_PatchesRemote_WhenRemoteUnchanged()
    {
        var n = TextNote("ilk");
        await Run();
        _repo.Update(n.Id, NoteType.Text, NoteContent.CreateText("yerelde değişti"), NoteColors.Default);

        var r = await Run();

        Assert.Equal(1, r.Pushed);
        Assert.Equal("yerelde değişti", _api.Memos["memos/" + n.Id.Replace("-", "")].Content);
    }

    [Fact]
    public async Task BothChanged_LocalWins_AndExactlyOneConflictCopy()
    {
        var n = TextNote("ortak");
        await Run();
        var memoName = "memos/" + n.Id.Replace("-", "");
        _api.RemoteEdit(memoName, content: "sunucu sürümü");
        _repo.Update(n.Id, NoteType.Text, NoteContent.CreateText("yerel sürüm"), NoteColors.Default);

        var r1 = await Run();
        var r2 = await Run();

        Assert.Equal(1, r1.Conflicts);
        Assert.Equal("yerel sürüm", Text(n.Id));
        Assert.Equal("yerel sürüm", _api.Memos[memoName].Content);

        var copies = _repo.ListActive().Where(x => Text(x.Id).StartsWith(SyncEngine.ConflictLabel, StringComparison.Ordinal)).ToList();
        Assert.Single(copies);
        Assert.Equal(SyncEngine.ConflictLabel + "\nsunucu sürümü", Text(copies[0].Id));
        Assert.Equal(0, r2.Conflicts);
        Assert.Equal(SyncStatus.Ok, r2.Status);
    }

    [Fact]
    public async Task LocalTrash_ArchivesRemote_AndRemoteArchive_TrashesLocal()
    {
        var a = TextNote("çöpe gidecek");
        var b = TextNote("arşivlenecek");
        await Run();

        _repo.SoftDelete(a.Id);
        _api.RemoteEdit("memos/" + b.Id.Replace("-", ""), state: "ARCHIVED");
        await Run();

        Assert.Equal("ARCHIVED", _api.Memos["memos/" + a.Id.Replace("-", "")].State);
        Assert.True(_repo.Get(b.Id)!.IsDeleted);
    }

    [Fact]
    public async Task PurgedNote_SendsNothingToServer()
    {
        var n = TextNote("kalıcı silinecek");
        await Run();
        var memoName = "memos/" + n.Id.Replace("-", "");
        _repo.SoftDelete(n.Id);
        _repo.Purge(n.Id);
        _api.Calls.Clear();

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.Equal(0, _api.Count("update"));
        Assert.Equal(0, _api.Count("create"));
        Assert.Equal("NORMAL", _api.Memos[memoName].State);
    }

    [Fact]
    public async Task SpaceMissing_PausesAndLeavesDatabaseByteIdentical()
    {
        TextNote("yerel");
        await Run();
        _repo.Create(NoteType.Text, NoteColors.Default, NoteContent.CreateText("sunucuda yok"));
        _api.SpaceExists = false;
        var before = TestRepo.Dump(_path);

        var r = await Run();

        Assert.Equal(SyncStatus.Paused, r.Status);
        Assert.Contains("'Ev Alanı' alanı bulunamadı", r.Message);
        Assert.Equal(before, TestRepo.Dump(_path));
    }

    [Fact]
    public async Task Unauthorized_PausesSync()
    {
        TextNote("x");
        _api.FailNext = new MemosUnauthorizedException();

        var r = await Run();

        Assert.Equal(SyncStatus.Paused, r.Status);
        Assert.Contains("Token geçersiz", r.Message);
    }

    [Fact]
    public async Task Offline_ReturnsOffline()
    {
        TextNote("x");
        _api.FailNext = new MemosOfflineException();

        var r = await Run();

        Assert.Equal(SyncStatus.Offline, r.Status);
    }

    [Fact]
    public async Task PausedSettings_DoNotCallServer_UnlessResumed()
    {
        TextNote("x");
        var paused = _settings with { Paused = true };

        var r1 = await _engine.RunOnceAsync(paused, resume: false);
        Assert.Equal(SyncStatus.Paused, r1.Status);
        Assert.Empty(_api.Calls);

        var r2 = await _engine.RunOnceAsync(paused, resume: true);
        Assert.Equal(SyncStatus.Ok, r2.Status);
    }

    [Fact]
    public async Task Safety_TenMappedMissing_PausesWithoutTrashing()
    {
        for (var i = 0; i < 10; i++) TextNote("not " + i);
        await Run();
        _api.Memos.Clear();

        var r = await Run();

        Assert.Equal(SyncStatus.Paused, r.Status);
        Assert.Contains("Sigorta", r.Message);
        Assert.All(_repo.ListActive(), n => Assert.False(n.IsDeleted));
    }

    [Fact]
    public async Task SingleMissing_TrashedOnlyWhenGetMemoConfirms404()
    {
        var gone = TextNote("sunucudan silindi");
        var moved = TextNote("başka alana taşındı");
        await Run();
        _api.Memos.Remove("memos/" + gone.Id.Replace("-", ""));
        _api.RemoteEdit("memos/" + moved.Id.Replace("-", ""), space: "spaces/other");

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.True(_repo.Get(gone.Id)!.IsDeleted);
        Assert.False(_repo.Get(moved.Id)!.IsDeleted);
    }

    [Fact]
    public async Task ReadOnly_OtherUsersNote_LocalEditBecomesCopy_AdminCanEdit()
    {
        _api.AddRemote("memos/0123456789abcdef0123456789abcdef", "başkasının notu", creator: "users/ali");
        _api.Me = "users/host";
        _api.Role = "USER";
        await Run();
        var id = Guid.ParseExact("0123456789abcdef0123456789abcdef", "N").ToString();
        _repo.Update(id, NoteType.Text, NoteContent.CreateText("benim düzenlemem"), NoteColors.Default);
        _api.Calls.Clear();

        var r = await Run();

        Assert.Equal(0, _api.Count("update"));
        Assert.Equal("başkasının notu", Text(id));
        Assert.Contains(_repo.ListActive(), n => Text(n.Id) == SyncEngine.ConflictLabel + "\nbenim düzenlemem");
        Assert.True(r.Conflicts >= 1);
        Assert.True(_repo.ListSyncState().Single(s => s.NoteId == id).ReadOnly);

        // Admin ise aynı memo düzenlenebilir.
        _api.Role = "ADMIN";
        _repo.Update(id, NoteType.Text, NoteContent.CreateText("admin düzenledi"), NoteColors.Default);
        await Run();
        Assert.Equal("admin düzenledi", _api.Memos["memos/0123456789abcdef0123456789abcdef"].Content);
    }

    [Fact]
    public async Task ReadOnly_LocalTrash_IsHiddenOnly_AndNeverSentToServer()
    {
        _api.AddRemote("memos/0123456789abcdef0123456789abcdef", "başkasının notu", creator: "users/ali");
        _api.Role = "USER";
        await Run();
        var id = Guid.ParseExact("0123456789abcdef0123456789abcdef", "N").ToString();
        _repo.SoftDelete(id);
        _api.Calls.Clear();

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.Equal(0, _api.Count("update"));
        Assert.Equal("NORMAL", _api.Memos["memos/0123456789abcdef0123456789abcdef"].State);
        Assert.True(_repo.Get(id)!.IsDeleted);
    }

    [Fact]
    public async Task Forbidden_OnPush_DemotesToReadOnly_KeepsLocalAsCopy()
    {
        var n = TextNote("benim");
        await Run();
        var memoName = "memos/" + n.Id.Replace("-", "");
        _repo.Update(n.Id, NoteType.Text, NoteContent.CreateText("yerel değişiklik"), NoteColors.Default);
        _api.ForbidUpdate = true;
        _api.RemoteEdit(memoName, content: "sunucu içeriği");

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.Equal("sunucu içeriği", Text(n.Id));
        Assert.True(_repo.ListSyncState().Single(s => s.NoteId == n.Id).ReadOnly);
        Assert.Contains(_repo.ListActive(), x => Text(x.Id) == SyncEngine.ConflictLabel + "\nyerel değişiklik");
    }

    [Fact]
    public async Task Collision409_OwnMemoNotYetListed_IsAdoptedWithoutDuplicate()
    {
        var n = TextNote("yanıt kayboldu");
        var memoName = "memos/" + n.Id.Replace("-", "");
        // Sunucuda oluşmuş ama listede henüz görünmüyor (zaman aşımında gönderilmiş).
        _api.AddRemote(memoName, "yanıt kayboldu");
        _api.HiddenFromList.Add(memoName);

        var r = await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.Single(_api.Memos, kv => kv.Key == memoName);
        Assert.Single(_repo.ListSyncState());
        // Tek POST denendi (409 aldı), yeniden kimlikle ikinci POST yok.
        Assert.Equal(1, _api.Count("create"));
    }

    [Fact]
    public async Task Offline_MidPush_ResumesWithoutDuplicates()
    {
        TextNote("bir");
        TextNote("iki");
        TextNote("üç");
        _api.OfflineOnCreateNumber = 2;

        var r1 = await Run();
        Assert.Equal(SyncStatus.Offline, r1.Status);
        Assert.Single(_repo.ListSyncState());

        var r2 = await Run();
        Assert.Equal(SyncStatus.Ok, r2.Status);
        Assert.Equal(3, _api.Memos.Count);
        Assert.Equal(3, _repo.ListSyncState().Count);
    }

    [Fact]
    public async Task Checklist_RoundTripsThroughSync()
    {
        var items = new[] { NoteContent.NewItem("araç 34 ABC 123", done: true), NoteContent.NewItem("yedek parça", indent: 1) };
        var n = _repo.Create(NoteType.Checklist, NoteColors.Default, NoteContent.CreateChecklist(items));
        await Run();

        var memo = _api.Memos["memos/" + n.Id.Replace("-", "")];
        Assert.Equal("- [x] araç 34 ABC 123\n  - [ ] yedek parça", memo.Content);

        _api.RemoteEdit(memo.Name, content: "- [x] araç 34 ABC 123\n  - [x] yedek parça\n- [ ] yeni madde");
        await Run();

        var parsed = NoteContent.ParseItems(_repo.Get(n.Id)!.Content);
        Assert.Equal(NoteType.Checklist, _repo.Get(n.Id)!.Type);
        Assert.Equal(new[] { ("araç 34 ABC 123", true, 0), ("yedek parça", true, 1), ("yeni madde", false, 0) },
            parsed.Select(i => (i.Text, i.Done, i.Indent)));
    }

    [Fact]
    public async Task EachRun_IsStableWhenNothingChanged()
    {
        TextNote("sabit");
        await Run();
        _api.Calls.Clear();

        var r = await Run();

        Assert.Equal(0, r.Pushed + r.Pulled + r.Created + r.Trashed + r.Conflicts);
        Assert.Equal(0, _api.Count("update"));
        Assert.Equal(0, _api.Count("create"));
    }

    [Fact]
    public void Settings_RoundTripAndNormalize()
    {
        var s = new SyncSettings { Enabled = true, Url = "http://h:5230/", Space = "spaces/x", Visibility = "BOGUS", IntervalMin = 500, Paused = true, ApiRoot = "/api/v1", TokenEnc = "abc" };
        var back = SyncSettings.FromPairs(s.Normalized().ToPairs());
        Assert.Equal("http://h:5230", back.Url);
        Assert.Equal("SPACE", back.Visibility);
        Assert.Equal(60, back.IntervalMin);
        Assert.True(back.Paused);
        Assert.Equal("/api/v1", back.ApiRoot);
        Assert.Equal("abc", back.TokenEnc);
    }

    [Fact]
    public async Task BusyNote_RemoteChangeDeferred_ThenAppliedWhenIdle()
    {
        var n = TextNote("ilk");
        await Run();                                    // ilk eşitleme: taban oluşur
        var memo = "memos/" + n.Id.Replace("-", "");
        _api.RemoteEdit(memo, content: "web'de değişti");

        var busy = true;
        var engine = new SyncEngine(_api, _repo, () => new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc), id => busy && id == n.Id);

        // Kullanıcı yazıyor: uzak sürüm yerel nota yazılmaz, taban değişmez
        var r1 = await engine.RunOnceAsync(_settings);
        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Equal("ilk", Text(n.Id));
        Assert.Contains("sonraki turda", r1.Message);

        // Düzenleme bitti: bir sonraki tur uzak değişikliği alır
        busy = false;
        var r2 = await engine.RunOnceAsync(_settings);
        Assert.Equal(SyncStatus.Ok, r2.Status);
        Assert.Equal("web'de değişti", Text(n.Id));
    }

    [Fact]
    public async Task RemoteApplied_InvokesCallbackWithNoteId()
    {
        var n = TextNote("ilk");
        await Run();
        _api.RemoteEdit("memos/" + n.Id.Replace("-", ""), content: "uzak");

        var applied = new List<string>();
        var engine = new SyncEngine(_api, _repo, null, null, id => applied.Add(id));
        await engine.RunOnceAsync(_settings);

        Assert.Equal(new[] { n.Id }, applied);
    }
}
