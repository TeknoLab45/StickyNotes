using StickyNotes.Core;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core.Tests;

/// <summary>Plan G2 senaryoları: Space değişimi, iki PC, uzun çevrimdışı, DB yeniden kurulumu, 403 (POST), geri alma.</summary>
public class SyncScenarioTests
{
    private static readonly DateTime T0 = new(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
    private static string Hex(string id) => id.Replace("-", "");
    private static string MemoOf(Note n) => "memos/" + Hex(n.Id);
    private static SyncSettings Settings(string space = FakeMemos.Space) => new()
    {
        Enabled = true,
        Url = "http://memos.test",
        Space = space,
        SpaceTitle = "Ev Alanı",
        Visibility = MemosVisibility.Space,
    };

    private sealed class Pc : IDisposable
    {
        public NoteRepository Repo { get; }
        public SyncEngine Engine { get; }
        private readonly string _path;

        public Pc(FakeMemos api)
        {
            Repo = TestRepo.Create(out _path);
            Engine = new SyncEngine(api, Repo, () => T0);
        }

        public void Dispose() => TestRepo.Cleanup(_path);
    }

    private static string Text(NoteRepository repo, string id) => NoteContent.ParseText(repo.Get(id)!.Content);

    private static Note Local(NoteRepository repo, string text) =>
        repo.Create(NoteType.Text, NoteColors.Default, NoteContent.CreateText(text));

    [Fact]
    public async Task ArchivedThenRestoredOnWeb_StaysRestored_OverTwoTicks()
    {
        var api = new FakeMemos();
        using var pc = new Pc(api);
        var n = Local(pc.Repo, "geri alınacak");
        await pc.Engine.RunOnceAsync(Settings());

        api.RemoteEdit(MemoOf(n), state: "ARCHIVED");
        await pc.Engine.RunOnceAsync(Settings());
        Assert.True(pc.Repo.Get(n.Id)!.IsDeleted);

        api.RemoteEdit(MemoOf(n), state: "NORMAL");
        var r1 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.False(pc.Repo.Get(n.Id)!.IsDeleted);

        api.Calls.Clear();
        var r2 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(0, r2.Pulled + r2.Pushed + r2.Created + r2.Trashed + r2.Conflicts);
        Assert.Equal(0, api.Count("update"));
        Assert.Equal("NORMAL", api.Memos[MemoOf(n)].State);
        Assert.False(pc.Repo.Get(n.Id)!.IsDeleted);
    }

    [Fact]
    public async Task SpaceChange_NotesDoNotLeakIntoNewSpace_AndOldMappingsResumeOnReturn()
    {
        var api = new FakeMemos();
        using var pc = new Pc(api);
        var a = Local(pc.Repo, "A");
        var s1 = Settings();
        await pc.Engine.RunOnceAsync(s1);

        var s2 = Settings("spaces/s2");
        var b = Local(pc.Repo, "B");
        var r1 = await pc.Engine.RunOnceAsync(s2);
        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Equal("spaces/s1", api.Memos[MemoOf(a)].Space);            // A yeni alana gönderilmedi
        Assert.Equal("spaces/s2", api.Memos[MemoOf(b)].Space);            // B eşlenmemişti: yeni alana gitti
        Assert.Equal(1, api.Memos.Values.Count(m => m.Space == "spaces/s2"));
        Assert.Equal("spaces/s1", pc.Repo.ListSyncState().Single(x => x.NoteId == a.Id).Space);

        // Eski Space'e dönüş: A'nın eşlemesi çalışır (yerel değişiklik gönderilir).
        Pc_Edit(pc.Repo, a.Id, "A düzenlendi");
        var r2 = await pc.Engine.RunOnceAsync(s1);
        Assert.Equal(SyncStatus.Ok, r2.Status);
        Assert.Equal(1, r2.Pushed);
        Assert.Equal("A düzenlendi", api.Memos[MemoOf(a)].Content);
        Assert.Equal("spaces/s2", api.Memos[MemoOf(b)].Space);            // B eski alana dönmedi
    }

    private static void Pc_Edit(NoteRepository repo, string id, string text) =>
        repo.Update(id, NoteType.Text, NoteContent.CreateText(text), NoteColors.Default);

    [Fact]
    public async Task TwoPcs_WithSameUuid_AdoptWithoutDuplicate()
    {
        var api = new FakeMemos();
        using var pcA = new Pc(api);
        using var pcB = new Pc(api);
        var id = Guid.CreateVersion7().ToString();
        pcA.Repo.InsertRemote(id, NoteType.Text, NoteContent.CreateText("ortak"), T0, null);
        pcB.Repo.InsertRemote(id, NoteType.Text, NoteContent.CreateText("ortak"), T0, null);

        var rA = await pcA.Engine.RunOnceAsync(Settings());
        Assert.Equal(1, rA.Created);

        var rB = await pcB.Engine.RunOnceAsync(Settings());
        Assert.Equal(SyncStatus.Ok, rB.Status);
        Assert.Equal(0, rB.Created + rB.Pushed + rB.Conflicts);
        Assert.Single(api.Memos);

        Pc_Edit(pcB.Repo, id, "B'den düzenleme");
        var rB2 = await pcB.Engine.RunOnceAsync(Settings());
        Assert.Equal(1, rB2.Pushed);

        var rA2 = await pcA.Engine.RunOnceAsync(Settings());
        Assert.Equal(1, rA2.Pulled);
        Assert.Equal(0, rA2.Conflicts);
        Assert.Equal("B'den düzenleme", Text(pcA.Repo, id));
        Assert.Single(api.Memos);
    }

    [Fact]
    public async Task LongOffline_BothSidesChanged_OneConflictCopy_ThenStable()
    {
        var api = new FakeMemos();
        using var pc = new Pc(api);
        var n1 = Local(pc.Repo, "bir");
        var n2 = Local(pc.Repo, "iki");
        var n3 = Local(pc.Repo, "üç");
        var n4 = Local(pc.Repo, "dört");
        await pc.Engine.RunOnceAsync(Settings());

        // Yerelde: n1 düzenlendi, n3 çöpe atıldı, n4 düzenlendi, n5 yeni.
        Pc_Edit(pc.Repo, n1.Id, "bir-yerel");
        pc.Repo.SoftDelete(n3.Id);
        Pc_Edit(pc.Repo, n4.Id, "dört-yerel");
        var n5 = Local(pc.Repo, "beş-yerel");
        // Sunucuda: n2 düzenlendi, n4 de düzenlendi (çakışma), yeni memo r6 oluştu.
        api.RemoteEdit(MemoOf(n2), content: "iki-uzak");
        api.RemoteEdit(MemoOf(n4), content: "dört-uzak");
        api.AddRemote("memos/fedcba9876543210fedcba9876543210", "uzak yeni");

        var r1 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Equal(1, r1.Conflicts);

        var r2 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(0, r2.Pulled + r2.Pushed + r2.Created + r2.Trashed + r2.Conflicts);

        // Etkin notlar NORMAL memolarla aynı; çöpe atılan "üç" sunucuda NORMAL kalır, hiçbir şey arşivlenmez.
        var localActive = pc.Repo.ListActive().Select(x => MemoMarkdown.FromNote(x.Type, x.Content)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var remoteNormal = api.Memos.Values.Where(m => m.State == "NORMAL").Select(m => m.Content).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "(çakışma kopyası)\ndört-uzak", "beş-yerel", "bir-yerel", "dört-yerel", "iki-uzak", "uzak yeni" }, localActive);
        Assert.Equal(localActive.Append("üç").OrderBy(x => x, StringComparer.Ordinal), remoteNormal);
        Assert.Equal(new[] { "üç" }, pc.Repo.ListTrash().Select(x => MemoMarkdown.FromNote(x.Type, x.Content)));
        _ = (n2, n5);
    }

    [Fact]
    public async Task FreshDatabase_AgainstPopulatedServer_CreatesNoCopiesOrDuplicates()
    {
        var api = new FakeMemos();
        using (var old = new Pc(api))
        {
            Local(old.Repo, "bir");
            Local(old.Repo, "iki");
            Local(old.Repo, "üç");
            await old.Engine.RunOnceAsync(Settings());
        }
        Assert.Equal(3, api.Memos.Count);
        api.Calls.Clear();

        using var fresh = new Pc(api);
        var r1 = await fresh.Engine.RunOnceAsync(Settings());

        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Equal(3, r1.Created);
        Assert.Equal(0, r1.Conflicts);
        Assert.Equal(0, api.Count("create"));
        Assert.Equal(3, api.Memos.Count);
        Assert.Equal(3, fresh.Repo.ListActive().Count);

        api.Calls.Clear();
        var r2 = await fresh.Engine.RunOnceAsync(Settings());
        Assert.Equal(0, r2.Pulled + r2.Pushed + r2.Created + r2.Trashed + r2.Conflicts);
        Assert.Equal(0, api.Count("update") + api.Count("create"));
    }

    [Fact]
    public async Task Forbidden_OnCreate_LeavesNoteLocal_OthersStillSync()
    {
        var api = new FakeMemos();
        using var pc = new Pc(api);
        var a = Local(pc.Repo, "a");
        var b = Local(pc.Repo, "b");

        api.ForbidCreate = true;
        var r1 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(SyncStatus.Ok, r1.Status);
        Assert.Contains("yetki yok", r1.Message);
        Assert.Empty(pc.Repo.ListSyncState());
        Assert.Empty(api.Memos);

        api.ForbidCreate = false;
        var r2 = await pc.Engine.RunOnceAsync(Settings());
        Assert.Equal(2, r2.Created);
        Assert.Equal(2, pc.Repo.ListSyncState().Count);
        _ = (a, b);
    }
}
