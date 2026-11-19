using StickyNotes.Core;
namespace StickyNotes.Core.Tests;

public sealed class NoteRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sn_test_" + Guid.NewGuid().ToString("N"));
    private DateTime _t = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private readonly NoteRepository _repo;

    public NoteRepositoryTests() => _repo = new NoteRepository(Path.Combine(_dir, "notes.db"), () => _t = _t.AddMinutes(1));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private Note Add(string text, string color = "yellow")
        => _repo.Create(NoteType.Text, color, NoteContent.CreateText(text));

    [Fact]
    public void Create_ThenGet_ReturnsNote()
    {
        var n = Add("merhaba", "green");
        var g = _repo.Get(n.Id)!;
        Assert.Equal("green", g.Color);
        Assert.Equal("merhaba", NoteContent.ParseText(g.Content));
        Assert.Equal(DateTimeKind.Utc, g.EditedAt.Kind);
        Assert.True(Guid.TryParse(g.Id, out _));
    }

    [Fact]
    public void Create_Default_HasEmptyContent_AndChecklistHasOneItem()
    {
        Assert.Equal("", NoteContent.ParseText(_repo.Create().Content));
        var cl = _repo.Create(NoteType.Checklist);
        Assert.Single(NoteContent.ParseItems(cl.Content));
    }

    [Fact]
    public void Update_ChangesFieldsAndEditedAt()
    {
        var n = Add("a");
        Assert.True(_repo.Update(n.Id, NoteType.Checklist, NoteContent.ToChecklist(n.Content), "blue"));
        var g = _repo.Get(n.Id)!;
        Assert.Equal(NoteType.Checklist, g.Type);
        Assert.Equal("blue", g.Color);
        Assert.True(g.EditedAt > n.EditedAt);
    }

    [Fact]
    public void InvalidColor_FallsBackToDefault()
    {
        var n = _repo.Create(color: "neon");
        Assert.Equal("yellow", n.Color);
    }

    [Fact]
    public void ListActive_OrderedByEditedDesc()
    {
        var a = Add("a"); var b = Add("b");
        _repo.Update(a.Id, a.Type, a.Content, a.Color);
        Assert.Equal(new[] { a.Id, b.Id }, _repo.ListActive().Select(n => n.Id));
    }

    [Fact]
    public void SoftDelete_MovesToTrash()
    {
        var n = Add("x");
        Assert.True(_repo.SoftDelete(n.Id));
        Assert.Empty(_repo.ListActive());
        Assert.Single(_repo.ListTrash());
        Assert.NotNull(_repo.Get(n.Id)!.DeletedAt);
        Assert.False(_repo.SoftDelete(n.Id)); // zaten çöpte
    }

    [Fact]
    public void Restore_BringsBack()
    {
        var n = Add("x");
        _repo.SoftDelete(n.Id);
        Assert.True(_repo.Restore(n.Id));
        Assert.Single(_repo.ListActive());
        Assert.Empty(_repo.ListTrash());
        Assert.False(_repo.Restore(n.Id));
    }

    [Fact]
    public void Purge_OnlyFromTrash_AndClearsContent()
    {
        var n = Add("gizli");
        Assert.False(_repo.Purge(n.Id)); // çöpte değil
        _repo.SoftDelete(n.Id);
        Assert.True(_repo.Purge(n.Id));
        Assert.Null(_repo.Get(n.Id));
        Assert.Empty(_repo.ListTrash());
        Assert.Empty(_repo.ListActive());
        Assert.False(_repo.Restore(n.Id));
        Assert.False(_repo.Update(n.Id, "text", "{}", "yellow"));
    }

    [Fact]
    public void EmptyTrash_PurgesAllTrashed_KeepsActive()
    {
        var keep = Add("keep");
        var a = Add("a"); var b = Add("b");
        _repo.SoftDelete(a.Id); _repo.SoftDelete(b.Id);
        Assert.Equal(2, _repo.EmptyTrash());
        Assert.Empty(_repo.ListTrash());
        Assert.Equal(keep.Id, Assert.Single(_repo.ListActive()).Id);
    }

    [Fact]
    public void Search_CaseInsensitive_AndInChecklist_AndInTrash()
    {
        Add("Alışveriş listesi");
        _repo.Create(NoteType.Checklist, content: NoteContent.CreateChecklist(new[] { NoteContent.NewItem("Süt al"), NoteContent.NewItem("ekmek") }));
        var t = Add("çöpteki not"); _repo.SoftDelete(t.Id);

        Assert.Single(_repo.ListActive("alışveriş"));
        Assert.Single(_repo.ListActive("EKMEK"));
        Assert.Equal(2, _repo.ListActive("l").Count);
        Assert.Empty(_repo.ListActive("çöpteki"));
        Assert.Single(_repo.ListTrash("çöpteki"));
        Assert.Empty(_repo.ListActive("yok böyle"));
        Assert.Equal(2, _repo.ListActive("  ").Count);
    }

    [Fact]
    public void WindowState_SaveAndLoad_Upsert()
    {
        var n = Add("w");
        Assert.Null(_repo.GetWindow(n.Id));
        _repo.SaveWindow(new NoteWindowState(n.Id, 10.5, 20, 300, 250, true));
        _repo.SaveWindow(new NoteWindowState(n.Id, 30, 40, 310, 260, false));
        Assert.Equal(new NoteWindowState(n.Id, 30, 40, 310, 260, false), _repo.GetWindow(n.Id));
    }

    [Fact]
    public void Persistence_SurvivesReopen()
    {
        var n = Add("kalıcı");
        _repo.SaveWindow(new NoteWindowState(n.Id, 1, 2, 3, 4, true));
        var again = new NoteRepository(_repo.DbPath);
        Assert.Equal("kalıcı", NoteContent.ParseText(again.Get(n.Id)!.Content));
        Assert.True(again.GetWindow(n.Id)!.IsOpen);
    }

    [Fact]
    public void WindowState_Collapsed_RoundTrips_AndOldCtorDefaultsToExpanded()
    {
        var n = Add("c");
        _repo.SaveWindow(new NoteWindowState(n.Id, 1, 2, 300, 30, true, true, 240));
        var g = _repo.GetWindow(n.Id)!;
        Assert.True(g.Collapsed);
        Assert.Equal(240, g.ExpandedHeight);
        Assert.Equal(30, g.Height);
        _repo.SaveWindow(new NoteWindowState(n.Id, 1, 2, 300, 240, true));
        Assert.False(_repo.GetWindow(n.Id)!.Collapsed);
    }

    [Fact]
    public void Migration_OldDatabaseWithoutCollapsedColumns_IsUpgraded_AndKeepsData()
    {
        var path = Path.Combine(_dir, "old.db");
        Directory.CreateDirectory(_dir);
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            c.Open();
            foreach (var sql in new[]
            {
                "CREATE TABLE notes(id TEXT PRIMARY KEY,type TEXT NOT NULL,content TEXT NOT NULL,color TEXT NOT NULL,edited_at TEXT NOT NULL,deleted_at TEXT NULL,purged_at TEXT NULL)",
                "CREATE TABLE window_state(note_id TEXT PRIMARY KEY,x REAL NOT NULL,y REAL NOT NULL,width REAL NOT NULL,height REAL NOT NULL,is_open INTEGER NOT NULL)",
                "INSERT INTO notes(id,type,content,color,edited_at) VALUES('old1','text','{}','yellow','2026-01-01T00:00:00.0000000Z')",
                "INSERT INTO window_state VALUES('old1',5,6,300,250,1)",
            })
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
        var repo = new NoteRepository(path);
        var ws = repo.GetWindow("old1")!;
        Assert.Equal(new NoteWindowState("old1", 5, 6, 300, 250, true, false, 0), ws);
        repo.SaveWindow(ws with { Collapsed = true, ExpandedHeight = 250, Height = 30 });
        Assert.True(new NoteRepository(path).GetWindow("old1")!.Collapsed);
        // ikinci açılış (kolonlar zaten var) hata vermez
        Assert.NotNull(new NoteRepository(path).Get("old1"));
    }

    [Fact]
    public void Purge_RemovesWindowState()
    {
        var n = Add("p");
        _repo.SaveWindow(new NoteWindowState(n.Id, 1, 2, 3, 4, true));
        _repo.SoftDelete(n.Id); _repo.Purge(n.Id);
        Assert.Null(_repo.GetWindow(n.Id));
    }

    [Fact]
    public void Opacity_DefaultsToOne_SaveAndReadBack_KeepsEditedAt()
    {
        var n = Add("o");
        Assert.Equal(1.0, _repo.Get(n.Id)!.Opacity);
        Assert.True(_repo.SetOpacity(n.Id, 0.7));
        var g = _repo.Get(n.Id)!;
        Assert.Equal(0.7, g.Opacity);
        Assert.Equal(n.EditedAt, g.EditedAt);
        Assert.Equal(0.7, new NoteRepository(_repo.DbPath).Get(n.Id)!.Opacity); // yeniden açılış
        _repo.Update(n.Id, NoteType.Text, NoteContent.CreateText("yeni"), "blue");
        Assert.Equal(0.7, _repo.Get(n.Id)!.Opacity); // içerik güncellemesi opaklığı bozmaz
        Assert.Equal(NoteOpacity.Min, NoteOpacity.Normalize(0.0));
        Assert.Equal(1.0, NoteOpacity.Normalize(5));
    }

    [Fact]
    public void SetAllOpacity_ResetsEveryNote()
    {
        var a = Add("a"); var b = Add("b");
        _repo.SetOpacity(a.Id, 0.6); _repo.SetOpacity(b.Id, 0.8);
        Assert.Equal(2, _repo.SetAllOpacity(1.0));
        Assert.Equal(1.0, _repo.Get(a.Id)!.Opacity);
        Assert.Equal(1.0, _repo.Get(b.Id)!.Opacity);
    }

    [Fact]
    public void Migration_OldDatabaseWithoutOpacityColumn_IsUpgraded_DefaultsToOpaque()
    {
        var path = Path.Combine(_dir, "old-opacity.db");
        Directory.CreateDirectory(_dir);
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            c.Open();
            foreach (var sql in new[]
            {
                "CREATE TABLE notes(id TEXT PRIMARY KEY,type TEXT NOT NULL,content TEXT NOT NULL,color TEXT NOT NULL,edited_at TEXT NOT NULL,deleted_at TEXT NULL,purged_at TEXT NULL)",
                "INSERT INTO notes(id,type,content,color,edited_at) VALUES('old2','text','{\"v\":1,\"text\":\"eski\"}','pink','2026-01-01T00:00:00.0000000Z')",
            })
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
        var repo = new NoteRepository(path);
        var n = repo.Get("old2")!;
        Assert.Equal(1.0, n.Opacity);
        Assert.Equal("pink", n.Color);
        Assert.Equal("eski", NoteContent.ParseText(n.Content));
        repo.SetOpacity("old2", 0.6);
        Assert.Equal(0.6, new NoteRepository(path).Get("old2")!.Opacity); // ikinci açılış zararsız
    }

    [Fact]
    public void CreatedAt_ComesFromUuidV7()
    {
        var at = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var note = new Note(Guid.CreateVersion7(at).ToString(), NoteType.Text, "", "yellow", at.UtcDateTime.AddDays(5), null, null);
        Assert.Equal(at.UtcDateTime, note.CreatedAt);
        Assert.Equal(note.EditedAt, (note with { Id = "abc" }).CreatedAt);
    }
}
