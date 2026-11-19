using Microsoft.Data.Sqlite;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core;

/// <summary>Ham SQL ile SQLite not deposu. Her işlem kısa ömürlü bağlantı kullanır.</summary>
public sealed class NoteRepository
{
    private readonly string _connStr;
    private readonly Func<DateTime> _now;

    public string DbPath { get; }

    public NoteRepository(string dbPath, Func<DateTime>? now = null)
    {
        DbPath = dbPath;
        _now = now ?? (() => DateTime.UtcNow);
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connStr = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
        Init();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connStr);
        c.Open();
        return c;
    }

    private void Init()
    {
        using var c = Open();
        Exec(c, "PRAGMA journal_mode=WAL;");
        Exec(c, @"CREATE TABLE IF NOT EXISTS notes(
            id TEXT PRIMARY KEY,
            type TEXT NOT NULL,
            content TEXT NOT NULL,
            color TEXT NOT NULL,
            edited_at TEXT NOT NULL,
            deleted_at TEXT NULL,
            purged_at TEXT NULL,
            opacity REAL NOT NULL DEFAULT 1.0);");
        Exec(c, @"CREATE TABLE IF NOT EXISTS window_state(
            note_id TEXT PRIMARY KEY,
            x REAL NOT NULL, y REAL NOT NULL, width REAL NOT NULL, height REAL NOT NULL,
            is_open INTEGER NOT NULL);");
        // Bu PC'ye özel ayarlar (senkronizasyona girmez). Eski DB'lerde yoksa burada oluşur.
        Exec(c, "CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);");
        // Memos eşitleme eşlemesi (yerel, senkronizasyona girmez). Silinen notun satırı mezar taşı olarak kalır.
        Exec(c, @"CREATE TABLE IF NOT EXISTS sync_state(
            note_id TEXT PRIMARY KEY,
            memo_name TEXT NOT NULL,
            space TEXT NOT NULL,
            creator TEXT NOT NULL DEFAULT '',
            creator_name TEXT NOT NULL DEFAULT '',
            read_only INTEGER NOT NULL DEFAULT 0,
            remote_update_time TEXT NULL,
            synced_hash TEXT NULL,
            synced_at TEXT NULL);");
        Exec(c, "CREATE UNIQUE INDEX IF NOT EXISTS sync_state_memo ON sync_state(memo_name);");
        MigrateNotes(c);
        MigrateWindowState(c);
    }

    // ---- Memos eşitlemesi ----

    /// <summary>Memos ayarlarını okur (settings tablosunda memos_* anahtarları).</summary>
    public Sync.SyncSettings LoadSyncSettings()
    {
        var kv = new Dictionary<string, string>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT key,value FROM settings WHERE key LIKE 'memos_%'";
        using var r = cmd.ExecuteReader();
        while (r.Read()) kv[r.GetString(0)] = r.GetString(1);
        return Sync.SyncSettings.FromPairs(kv);
    }

    public void SaveSyncSettings(Sync.SyncSettings settings)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var (k, v) in settings.Normalized().ToPairs())
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";
            cmd.Parameters.AddWithValue("$k", k);
            cmd.Parameters.AddWithValue("$v", v);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>Tüm eşlemeler (tüm Space'ler). Kalıcı silinmiş notların satırı da döner.</summary>
    public List<SyncRecord> ListSyncState()
    {
        var res = new List<SyncRecord>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT note_id,memo_name,space,creator,creator_name,read_only,remote_update_time,synced_hash,synced_at FROM sync_state";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            res.Add(new SyncRecord(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetInt32(5) != 0,
                r.IsDBNull(6) ? null : ParseTs(r.GetString(6)),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : ParseTs(r.GetString(8))));
        }
        return res;
    }

    /// <summary>Eşlemeyi yazar (ekler ya da günceller). Her başarılı uzak işlemden hemen sonra çağrılır.</summary>
    public void MarkSynced(SyncRecord r)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO sync_state(note_id,memo_name,space,creator,creator_name,read_only,remote_update_time,synced_hash,synced_at)
            VALUES($id,$m,$s,$cr,$crn,$ro,$rut,$h,$at)
            ON CONFLICT(note_id) DO UPDATE SET memo_name=$m,space=$s,creator=$cr,creator_name=$crn,read_only=$ro,
              remote_update_time=$rut,synced_hash=$h,synced_at=$at";
        cmd.Parameters.AddWithValue("$id", r.NoteId);
        cmd.Parameters.AddWithValue("$m", r.MemoName);
        cmd.Parameters.AddWithValue("$s", r.Space);
        cmd.Parameters.AddWithValue("$cr", r.Creator);
        cmd.Parameters.AddWithValue("$crn", r.CreatorName);
        cmd.Parameters.AddWithValue("$ro", r.ReadOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("$rut", r.RemoteUpdateTime is { } rut ? Ts(rut) : DBNull.Value);
        cmd.Parameters.AddWithValue("$h", (object?)r.SyncedHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$at", r.SyncedAt is { } at ? Ts(at) : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Uzaktan gelen not için yerel kayıt oluşturur (kimlik verilir). deletedAt doluysa çöpte başlar.</summary>
    public void InsertRemote(string id, string type, string content, DateTime editedAt, DateTime? deletedAt)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO notes(id,type,content,color,edited_at,deleted_at) VALUES($id,$t,$c,$col,$e,$d)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$t", type);
        cmd.Parameters.AddWithValue("$c", content);
        cmd.Parameters.AddWithValue("$col", NoteColors.Default);
        cmd.Parameters.AddWithValue("$e", Ts(editedAt));
        cmd.Parameters.AddWithValue("$d", deletedAt is { } d ? Ts(d) : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Uzak içeriği uygular (tür, içerik, düzenleme zamanı). Renk, opaklık ve çöp durumu korunur.</summary>
    public void ApplyRemote(string noteId, string type, string content, DateTime editedAt)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE notes SET type=$t,content=$c,edited_at=$e WHERE id=$id AND purged_at IS NULL";
        cmd.Parameters.AddWithValue("$t", type);
        cmd.Parameters.AddWithValue("$c", content);
        cmd.Parameters.AddWithValue("$e", Ts(editedAt));
        cmd.Parameters.AddWithValue("$id", noteId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Kalıcı silinmemiş tüm notlar (çöptekiler dahil). Eşitleme için.</summary>
    public List<Note> ListForSync()
    {
        var res = new List<Note>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM notes WHERE purged_at IS NULL ORDER BY edited_at";
        using var r = cmd.ExecuteReader();
        while (r.Read()) res.Add(Read(r));
        return res;
    }

    /// <summary>Ayarları okur; satır yoksa/bozuksa varsayılan.</summary>
    public AppSettings LoadSettings()
    {
        var kv = new Dictionary<string, string>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT key,value FROM settings";
        using var r = cmd.ExecuteReader();
        while (r.Read()) kv[r.GetString(0)] = r.GetString(1);
        return AppSettings.FromPairs(kv);
    }

    public void SaveSettings(AppSettings settings)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var (k, v) in settings.Normalized().ToPairs())
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";
            cmd.Parameters.AddWithValue("$k", k);
            cmd.Parameters.AddWithValue("$v", v);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>Eski veritabanları için: notes.opacity sütunu (varsayılan 1.0 = opak).</summary>
    private static void MigrateNotes(SqliteConnection c)
    {
        if (!Columns(c, "notes").Contains("opacity"))
            Exec(c, "ALTER TABLE notes ADD COLUMN opacity REAL NOT NULL DEFAULT 1.0");
    }

    private static HashSet<string> Columns(SqliteConnection c, string table)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(1));
        return cols;
    }

    /// <summary>Eski veritabanları için geriye uyumlu migrasyon: collapsed + expanded_height sütunları (varsayılan 0).</summary>
    private static void MigrateWindowState(SqliteConnection c)
    {
        var cols = Columns(c, "window_state");
        if (!cols.Contains("collapsed")) Exec(c, "ALTER TABLE window_state ADD COLUMN collapsed INTEGER NOT NULL DEFAULT 0");
        if (!cols.Contains("expanded_height")) Exec(c, "ALTER TABLE window_state ADD COLUMN expanded_height REAL NOT NULL DEFAULT 0");
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string Ts(DateTime d) => d.ToUniversalTime().ToString("O");
    private static DateTime ParseTs(string s) =>
        DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static Note Read(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
        ParseTs(r.GetString(4)),
        r.IsDBNull(5) ? null : ParseTs(r.GetString(5)),
        r.IsDBNull(6) ? null : ParseTs(r.GetString(6)),
        NoteOpacity.Normalize(r.GetDouble(7)));

    private const string Cols = "id,type,content,color,edited_at,deleted_at,purged_at,opacity";

    public Note Create(string type = NoteType.Text, string color = NoteColors.Default, string? content = null)
    {
        var note = new Note(Guid.CreateVersion7().ToString(), type, content ?? NoteContent.Empty(type),
            NoteColors.Normalize(color), _now(), null, null);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO notes(id,type,content,color,edited_at) VALUES($id,$t,$c,$col,$e)";
        cmd.Parameters.AddWithValue("$id", note.Id);
        cmd.Parameters.AddWithValue("$t", note.Type);
        cmd.Parameters.AddWithValue("$c", note.Content);
        cmd.Parameters.AddWithValue("$col", note.Color);
        cmd.Parameters.AddWithValue("$e", Ts(note.EditedAt));
        cmd.ExecuteNonQuery();
        return note;
    }

    public Note? Get(string id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM notes WHERE id=$id AND purged_at IS NULL";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>Not opaklığını yazar (edited_at değişmez: görünüm tercihi). Kalıcı silinmiş notu etkilemez.</summary>
    public bool SetOpacity(string id, double opacity)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE notes SET opacity=$o WHERE id=$id AND purged_at IS NULL";
        cmd.Parameters.AddWithValue("$o", NoteOpacity.Normalize(opacity));
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>Tüm (silinmemiş-kalıcı) notların opaklığını verilen değere çeker; etkilenen satır sayısı döner.</summary>
    public int SetAllOpacity(double opacity)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE notes SET opacity=$o WHERE purged_at IS NULL AND opacity<>$o";
        cmd.Parameters.AddWithValue("$o", NoteOpacity.Normalize(opacity));
        return cmd.ExecuteNonQuery();
    }

    /// <summary>İçerik/tür/renk günceller; edited_at yenilenir. Kalıcı silinmiş notu etkilemez.</summary>
    public bool Update(string id, string type, string content, string color)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE notes SET type=$t,content=$c,color=$col,edited_at=$e WHERE id=$id AND purged_at IS NULL";
        cmd.Parameters.AddWithValue("$t", type);
        cmd.Parameters.AddWithValue("$c", content);
        cmd.Parameters.AddWithValue("$col", NoteColors.Normalize(color));
        cmd.Parameters.AddWithValue("$e", Ts(_now()));
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>Aktif (çöpte olmayan) notlar, en son düzenlenen üstte. query doluysa düz metinde arar.</summary>
    public List<Note> ListActive(string? query = null) => List(false, query);

    public List<Note> ListTrash(string? query = null) => List(true, query);

    private List<Note> List(bool trash, string? query)
    {
        var res = new List<Note>();
        using (var c = Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = $"SELECT {Cols} FROM notes WHERE purged_at IS NULL AND deleted_at IS {(trash ? "NOT " : "")}NULL ORDER BY {(trash ? "deleted_at" : "edited_at")} DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) res.Add(Read(r));
        }
        if (string.IsNullOrWhiteSpace(query)) return res;
        var q = query.Trim();
        return res.Where(n => NoteContent.PlainText(n.Type, n.Content)
            .Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    /// <summary>Çöpe taşır (yumuşak silme).</summary>
    public bool SoftDelete(string id) => SetDeleted(id, _now(), "deleted_at IS NULL");

    /// <summary>Çöpten geri alır.</summary>
    public bool Restore(string id) => SetDeleted(id, null, "deleted_at IS NOT NULL");

    private bool SetDeleted(string id, DateTime? at, string cond)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"UPDATE notes SET deleted_at=$d,edited_at=$e WHERE id=$id AND purged_at IS NULL AND {cond}";
        cmd.Parameters.AddWithValue("$d", at == null ? DBNull.Value : Ts(at.Value));
        cmd.Parameters.AddWithValue("$e", Ts(_now()));
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>Kalıcı siler: yalnızca çöptekiler. Satır sync için mezar taşı olarak kalır; içerik boşaltılır.</summary>
    public bool Purge(string id)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE notes SET purged_at=$p,content='',edited_at=$p WHERE id=$id AND deleted_at IS NOT NULL AND purged_at IS NULL";
        cmd.Parameters.AddWithValue("$p", Ts(_now()));
        cmd.Parameters.AddWithValue("$id", id);
        var ok = cmd.ExecuteNonQuery() > 0;
        if (ok)
        {
            using var d = c.CreateCommand();
            d.Transaction = tx;
            d.CommandText = "DELETE FROM window_state WHERE note_id=$id";
            d.Parameters.AddWithValue("$id", id);
            d.ExecuteNonQuery();
        }
        tx.Commit();
        return ok;
    }

    /// <summary>Çöpteki tüm notları kalıcı siler; silinen adedi döner.</summary>
    public int EmptyTrash()
    {
        var ids = ListTrash().Select(n => n.Id).ToList();
        return ids.Count(Purge);
    }

    public void SaveWindow(NoteWindowState w)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO window_state(note_id,x,y,width,height,is_open,collapsed,expanded_height) VALUES($id,$x,$y,$w,$h,$o,$cl,$eh)
            ON CONFLICT(note_id) DO UPDATE SET x=$x,y=$y,width=$w,height=$h,is_open=$o,collapsed=$cl,expanded_height=$eh";
        cmd.Parameters.AddWithValue("$id", w.NoteId);
        cmd.Parameters.AddWithValue("$x", w.X);
        cmd.Parameters.AddWithValue("$y", w.Y);
        cmd.Parameters.AddWithValue("$w", w.Width);
        cmd.Parameters.AddWithValue("$h", w.Height);
        cmd.Parameters.AddWithValue("$o", w.IsOpen ? 1 : 0);
        cmd.Parameters.AddWithValue("$cl", w.Collapsed ? 1 : 0);
        cmd.Parameters.AddWithValue("$eh", w.ExpandedHeight);
        cmd.ExecuteNonQuery();
    }

    public NoteWindowState? GetWindow(string noteId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT note_id,x,y,width,height,is_open,collapsed,expanded_height FROM window_state WHERE note_id=$id";
        cmd.Parameters.AddWithValue("$id", noteId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new NoteWindowState(r.GetString(0), r.GetDouble(1), r.GetDouble(2), r.GetDouble(3), r.GetDouble(4), r.GetInt32(5) != 0, r.GetInt32(6) != 0, r.GetDouble(7)) : null;
    }
}
