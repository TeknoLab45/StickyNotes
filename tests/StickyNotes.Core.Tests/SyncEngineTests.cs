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

    private static string MemoOf(Note n) => "memos/" + n.Id.Replace("-", "");

    private const string OtherHex = "0123456789abcdef0123456789abcdef";
    private static string OtherId => Guid.ParseExact(OtherHex, "N").ToString();

    private int Writes() => _api.Count("update") + _api.Count("create") + _api.Count("delete");

    private async Task<Note> Synced(string text)
    {
        var n = TextNote(text);
        await Run();
        _api.Calls.Clear();
        return n;
    }

    private void TrashAndPurge(string id)
    {
        _repo.SoftDelete(id);
        Assert.True(_repo.Purge(id));
    }

    [Fact]
    public async Task LocalTrash_SendsNothing_AndNoteStaysTrashed_WithoutDuplicate()
    {
        var n = await Synced("çöpe gidecek");
        _repo.SoftDelete(n.Id);

        await Run();
        await Run();

        Assert.Equal(0, Writes());
        Assert.Equal("NORMAL", _api.Memos[MemoOf(n)].State);
        Assert.True(_repo.Get(n.Id)!.IsDeleted);
        Assert.Single(_repo.ListForSync());
    }

    [Fact]
    public async Task RemoteArchive_TrashesLocal()
    {
        var n = await Synced("arşivlenecek");
        _api.RemoteEdit(MemoOf(n), state: "ARCHIVED");

        await Run();

        Assert.True(_repo.Get(n.Id)!.IsDeleted);
        Assert.Equal(0, Writes());
    }

    [Fact]
    public async Task Purge_DeletesMemoOnce_AndNoteDoesNotReturn()
    {
        var n = await Synced("kalıcı silinecek");
        TrashAndPurge(n.Id);

        var r = await Run();
        await Run();

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.False(_api.Memos.ContainsKey(MemoOf(n)));
        Assert.Equal(1, _api.Count("delete"));
        Assert.Equal(0, _api.Count("update") + _api.Count("create"));
        Assert.Empty(_repo.ListForSync());
    }

    [Fact]
    public async Task EmptyTrash_ManyNotes_DeletesAll_WithoutTrippingSafety()
    {
        for (var i = 0; i < 10; i++) _repo.SoftDelete((await Synced("not " + i)).Id);
        Assert.Equal(10, _repo.EmptyTrash());

        var r1 = await Run();
        var r2 = await Run();

        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Equal(SyncStatus.Ok, r2.Status);
        Assert.Empty(_api.Memos);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Purge_FailedDelete_StaysPending_AndIsRetried(bool offline)
    {
        var n = await Synced("çevrimdışı silme");
        TrashAndPurge(n.Id);
        _api.FailDelete = offline ? new MemosOfflineException() : new MemosException("500");

        var r1 = await Run();

        Assert.Equal(offline ? SyncStatus.Offline : SyncStatus.Ok, r1.Status);
        Assert.Single(_repo.ListPendingDeletes(FakeMemos.Space));
        Assert.Empty(_repo.ListForSync()); // memo duruyor ama yerele geri gelmedi

        _api.FailDelete = null;
        var r2 = await Run();

        Assert.Equal(SyncStatus.Ok, r2.Status);
        Assert.False(_api.Memos.ContainsKey(MemoOf(n)));
        Assert.Empty(_repo.ListPendingDeletes(FakeMemos.Space));
    }

    [Fact]
    public async Task Purge_ReadOnlyNote_NeverDeletesMemo()
    {
        _api.AddRemote("memos/" + OtherHex, "başkasının notu", creator: "users/ali");
        _api.Role = "USER";
        await Run();
        TrashAndPurge(OtherId);
        _api.Calls.Clear();

        await Run();

        Assert.Equal(0, Writes());
        Assert.True(_api.Memos.ContainsKey("memos/" + OtherHex));
        Assert.Empty(_repo.ListForSync());
    }

    [Fact]
    public async Task Purge_PermissionLostBeforeSync_NeverDeletesMemo()
    {
        _api.AddRemote("memos/" + OtherHex, "başkasının notu", creator: "users/ali");
        await Run(); // admin: düzenlenebilir
        TrashAndPurge(OtherId);
        _api.Role = "USER";

        await Run();

        Assert.Equal(0, _api.Count("delete"));
        Assert.Empty(_repo.ListPendingDeletes(FakeMemos.Space));
        Assert.Empty(_repo.ListForSync());
    }

    [Fact]
    public async Task Purge_Forbidden_IsDropped_NotRetried()
    {
        var n = await Synced("403");
        TrashAndPurge(n.Id);
        _api.FailDelete = new MemosForbiddenException();

        await Run();
        await Run();

        Assert.Equal(1, _api.Count("delete"));
        Assert.True(_api.Memos.ContainsKey(MemoOf(n)));
        Assert.Empty(_repo.ListForSync());
    }

    [Fact]
    public async Task Purge_OtherSpaceMapping_SendsNothing_UntilThatSpaceIsSelectedAgain()
    {
        var n = await Synced("eski alan");
        TrashAndPurge(n.Id);

        await _engine.RunOnceAsync(_settings with { Space = "spaces/s2" });
        Assert.Equal(0, _api.Count("delete"));

        await Run();
        Assert.False(_api.Memos.ContainsKey(MemoOf(n)));
    }

    [Fact]
    public async Task OldTombstone_FromBeforeUpdate_IsNeverDeletedFromServer()
    {
        var n = await Synced("eski mezar taşı");
        TrashAndPurge(n.Id);
        // Güncelleme öncesi veritabanı: delete_pending sütunu yok.
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + _path + ";Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "ALTER TABLE sync_state DROP COLUMN delete_pending";
            cmd.ExecuteNonQuery();
        }
        var upgraded = new SyncEngine(_api, new NoteRepository(_path));

        var r = await upgraded.RunOnceAsync(_settings);

        Assert.Equal(SyncStatus.Ok, r.Status);
        Assert.Equal(0, Writes());
        Assert.True(_api.Memos.ContainsKey(MemoOf(n)));
        Assert.Empty(_repo.ListForSync());
    }

    [Fact]
    public async Task PurgedWhileBeingCreated_IsDeletedOnNextRun()
    {
        var n = TextNote("gönderilirken silindi");
        _api.OnCreate = () => TrashAndPurge(n.Id);
        await Run();

        await Run();

        Assert.False(_api.Memos.ContainsKey(MemoOf(n)));
    }

    [Fact]
    public async Task Restore_KeepsMapping_NoDuplicate_NoWrites()
    {
        var n = await Synced("geri alınacak");
        _repo.SoftDelete(n.Id);
        await Run();
        _repo.Restore(n.Id);

        await Run();

        Assert.Equal(0, Writes());
        Assert.Single(_api.Memos);
        Assert.Single(_repo.ListForSync());
        Assert.False(_repo.Get(n.Id)!.IsDeleted);
    }

    [Fact]
    public async Task WebEditWhileTrashed_StaysTrashed_AndRestoreTakesWebContent()
    {
        var n = await Synced("ilk");
        _repo.SoftDelete(n.Id);
        _api.RemoteEdit(MemoOf(n), content: "web'de değişti");
        await Run();
        Assert.True(_repo.Get(n.Id)!.IsDeleted);

        _repo.Restore(n.Id);
        var r = await Run();

        Assert.Equal("web'de değişti", Text(n.Id));
        Assert.Equal("web'de değişti", _api.Memos[MemoOf(n)].Content);
        Assert.Equal(0, r.Conflicts);
        Assert.False(_repo.Get(n.Id)!.IsDeleted);
    }

    [Fact]
    public async Task WebEditAndUnsentLocalEdit_RestoreMakesConflictCopy()
    {
        var n = await Synced("ilk");
        _repo.Update(n.Id, NoteType.Text, NoteContent.CreateText("yerel"), NoteColors.Default);
        _repo.SoftDelete(n.Id);
        _api.RemoteEdit(MemoOf(n), content: "web");
        await Run();
        Assert.Equal(0, Writes());

        _repo.Restore(n.Id);
        var r = await Run();

        Assert.Equal(1, r.Conflicts);
        Assert.Equal("yerel", _api.Memos[MemoOf(n)].Content);
        Assert.Contains(_repo.ListActive(), x => Text(x.Id) == SyncEngine.ConflictLabel + "\nweb");
    }

    [Theory]
    [InlineData("ilk")]
    [InlineData("web'de değişti")]
    public async Task WebArchiveWhileTrashed_RestoreUnarchives(string webContent)
    {
        var n = await Synced("ilk");
        _repo.SoftDelete(n.Id);
        _api.RemoteEdit(MemoOf(n), content: webContent, state: "ARCHIVED");
        await Run();

        _repo.Restore(n.Id);
        await Run();

        Assert.Equal("NORMAL", _api.Memos[MemoOf(n)].State);
        Assert.Equal(webContent, _api.Memos[MemoOf(n)].Content);
        Assert.Equal(webContent, Text(n.Id));
        Assert.False(_repo.Get(n.Id)!.IsDeleted);
    }

    [Fact]
    public async Task Restore_NoteArchivedByOldBehaviour_UnarchivesMemo()
    {
        var n = await Synced("eski çöp");
        // Eski sürümün bıraktığı durum: yerelde çöpte, sunucuda ARCHIVED, taban arşivli.
        _repo.SoftDelete(n.Id);
        _api.RemoteEdit(MemoOf(n), state: "ARCHIVED");
        _repo.MarkSynced(_repo.ListSyncState().Single() with { SyncedHash = MemoMarkdown.Fingerprint("eski çöp", true) });
        await Run();
        Assert.Equal(0, Writes());

        _repo.Restore(n.Id);
        await Run();

        Assert.Equal("NORMAL", _api.Memos[MemoOf(n)].State);
        Assert.Single(_api.Memos);
        Assert.False(_repo.Get(n.Id)!.IsDeleted);
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
