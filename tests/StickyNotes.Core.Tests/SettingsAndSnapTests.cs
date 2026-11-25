using StickyNotes.Core;
namespace StickyNotes.Core.Tests;

public sealed class SettingsAndSnapTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sn_set_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Settings_Defaults_WhenNothingSaved()
    {
        var s = new NoteRepository(Path.Combine(_dir, "a.db")).LoadSettings();
        Assert.Equal(new AppSettings().Normalized(), s);
        Assert.Equal(260, s.DefaultWidth);
        Assert.Equal("Alt+H", s.HotkeyHideAll);
    }

    [Fact]
    public void Settings_SaveThenLoad_RoundTrips_AcrossReopen()
    {
        var path = Path.Combine(_dir, "b.db");
        var s = new AppSettings
        {
            FontFamily = "Consolas", FontSize = 18, FontStyle = NoteFontStyle.Italic,
            DefaultColor = "green", DefaultOpacity = 0.2, DefaultWidth = 300, DefaultHeight = 220, DefaultType = NoteType.Checklist,
            HotkeyHideAll = "Ctrl+Alt+H", HotkeyShowAll = "", HotkeyExplorer = "Alt+X", HotkeyNewNote = "Alt+N",
        };
        new NoteRepository(path).SaveSettings(s);
        Assert.Equal(s, new NoteRepository(path).LoadSettings());
        // üzerine yaz
        new NoteRepository(path).SaveSettings(s with { FontSize = 12 });
        Assert.Equal(12, new NoteRepository(path).LoadSettings().FontSize);
    }

    [Fact]
    public void Settings_AreClamped()
    {
        var path = Path.Combine(_dir, "c.db");
        var repo = new NoteRepository(path);
        repo.SaveSettings(new AppSettings { FontSize = 99, DefaultOpacity = 0.01, DefaultColor = "nope", FontStyle = "x", DefaultWidth = 5 });
        var s = repo.LoadSettings();
        Assert.Equal(24, s.FontSize);
        Assert.Equal(0.2, s.DefaultOpacity);
        Assert.Equal("yellow", s.DefaultColor);
        Assert.Equal(NoteFontStyle.Normal, s.FontStyle);
        Assert.Equal(AppSettings.MinNoteSize, s.DefaultWidth);
        Assert.Equal(10, new AppSettings { FontSize = 1 }.Normalized().FontSize);
    }

    [Fact]
    public void OldDatabase_WithoutSettingsTable_IsUpgraded_AndNotesSurvive()
    {
        var path = Path.Combine(_dir, "old.db");
        Directory.CreateDirectory(_dir);
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            c.Open();
            foreach (var sql in new[]
            {
                "CREATE TABLE notes(id TEXT PRIMARY KEY,type TEXT NOT NULL,content TEXT NOT NULL,color TEXT NOT NULL,edited_at TEXT NOT NULL,deleted_at TEXT NULL,purged_at TEXT NULL,opacity REAL NOT NULL DEFAULT 1.0)",
                "CREATE TABLE window_state(note_id TEXT PRIMARY KEY,x REAL NOT NULL,y REAL NOT NULL,width REAL NOT NULL,height REAL NOT NULL,is_open INTEGER NOT NULL)",
                "INSERT INTO notes(id,type,content,color,edited_at) VALUES('o1','text','{\"v\":1,\"text\":\"eski\"}','pink','2026-01-01T00:00:00.0000000Z')",
            })
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
        var repo = new NoteRepository(path);
        Assert.Equal(new AppSettings().Normalized(), repo.LoadSettings());
        Assert.Equal("eski", NoteContent.ParseText(repo.Get("o1")!.Content));
        repo.SaveSettings(new AppSettings { FontSize = 20 });
        Assert.Equal(20, new NoteRepository(path).LoadSettings().FontSize);
    }

    [Fact]
    public void Settings_DoNotTouchNotes_EditedAt()
    {
        var repo = new NoteRepository(Path.Combine(_dir, "d.db"));
        var n = repo.Create();
        var before = repo.Get(n.Id)!.EditedAt;
        repo.SaveSettings(new AppSettings { FontSize = 16 });
        Assert.Equal(before, repo.Get(n.Id)!.EditedAt);
    }

    [Fact]
    public void Opacity_Min_IsTwentyPercent_AndLevelsStepByTen()
    {
        Assert.Equal(0.2, NoteOpacity.Min);
        Assert.Equal(0.2, NoteOpacity.Normalize(0.0));
        Assert.Equal(0.2, NoteOpacity.Normalize(0.25 - 0.05));
        Assert.Equal(9, NoteOpacity.Levels.Length);
        Assert.Equal(1.0, NoteOpacity.Levels[0]);
        Assert.Equal(0.2, NoteOpacity.Levels[^1], 9);
        var repo = new NoteRepository(Path.Combine(_dir, "e.db"));
        var n = repo.Create();
        repo.SetOpacity(n.Id, 0.2);
        Assert.Equal(0.2, new NoteRepository(repo.DbPath).Get(n.Id)!.Opacity); // yeniden açılışta korunur
    }

    // ---- Snap ----
    private static readonly RectD Work = new(0, 0, 1000, 800);

    [Fact]
    public void Snap_Move_NearLeftTop_SnapsToEdge()
    {
        var r = WindowSnap.Snap(new RectD(9, 11, 260, 260), Work, 12);
        Assert.Equal(new RectD(0, 0, 260, 260), r);
    }

    [Fact]
    public void Snap_Move_NearRightBottom_SnapsUsingRightEdge()
    {
        var r = WindowSnap.Snap(new RectD(1000 - 260 - 5, 800 - 260 - 12, 260, 260), Work, 12);
        Assert.Equal(new RectD(740, 540, 260, 260), r);
    }

    [Fact]
    public void Snap_Move_FarFromEdges_Unchanged()
    {
        var r = new RectD(300, 200, 260, 260);
        Assert.Equal(r, WindowSnap.Snap(r, Work, 12));
    }

    [Fact]
    public void Snap_Move_JustBeyondThreshold_DoesNotSnap()
    {
        var r = WindowSnap.Snap(new RectD(13, 300, 260, 260), Work, 12);
        Assert.Equal(13, r.X);
    }

    [Fact]
    public void Snap_Move_OutsideWorkArea_IsClampedInside()
    {
        Assert.Equal(new RectD(0, 0, 260, 260), WindowSnap.Snap(new RectD(-500, -300, 260, 260), Work, 12));
        Assert.Equal(new RectD(740, 540, 260, 260), WindowSnap.Snap(new RectD(5000, 5000, 260, 260), Work, 12));
    }

    [Fact]
    public void Snap_Move_LargerThanWorkArea_ShrinksToFit()
    {
        var r = WindowSnap.Snap(new RectD(50, 50, 2000, 1500), Work, 12);
        Assert.Equal(new RectD(0, 0, 1000, 800), r);
    }

    [Fact]
    public void Snap_Move_IsIdempotent()
    {
        var once = WindowSnap.Snap(new RectD(7, 9, 260, 260), Work, 12);
        Assert.Equal(once, WindowSnap.Snap(once, Work, 12));
    }

    [Fact]
    public void Snap_Move_NonZeroWorkAreaOrigin_SecondMonitor()
    {
        var work2 = new RectD(1920, 40, 1280, 960);   // görev çubuğu üstte, ikinci monitör
        var r = WindowSnap.Snap(new RectD(1925, 50, 260, 260), work2, 12);
        Assert.Equal(new RectD(1920, 40, 260, 260), r);
    }

    [Fact]
    public void Snap_Resize_RightEdge_SnapsToWorkAreaRight_OthersFixed()
    {
        var r = WindowSnap.Snap(new RectD(500, 100, 490, 300), Work, 12, SnapEdges.Right);
        Assert.Equal(new RectD(500, 100, 500, 300), r);
    }

    [Fact]
    public void Snap_Resize_LeftEdge_BeyondScreen_StopsAtWorkAreaLeft()
    {
        var r = WindowSnap.Snap(new RectD(-80, 100, 380, 300), Work, 12, SnapEdges.Left);
        Assert.Equal(new RectD(0, 100, 300, 300), r);
    }

    [Fact]
    public void Snap_Resize_RespectsMinimumSize()
    {
        var r = WindowSnap.Snap(new RectD(300, 100, 100, 300), Work, 12, SnapEdges.Left | SnapEdges.Top, 220, 110);
        Assert.Equal(220, r.Width);
        Assert.Equal(400, r.Right);
    }

    [Fact]
    public void Snap_None_ReturnsInput()
    {
        var r = new RectD(-5, -5, 100, 100);
        Assert.Equal(r, WindowSnap.Snap(r, Work, 12, SnapEdges.None));
    }
}
