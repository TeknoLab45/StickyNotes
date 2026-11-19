using System.Net;
using StickyNotes.Core;
using StickyNotes.Core.Sync;
using Microsoft.Data.Sqlite;

namespace StickyNotes.Core.Tests;

/// <summary>Bellek içi Memos sahtesi. Sunucu kuralları: aynı memoId → AlreadyExists; ARCHIVED listelenir; Space silinince 404.</summary>
public sealed class FakeMemos : IMemosApi
{
    public const string Space = "spaces/s1";
    public string Me = "users/host";
    public string Role = "ADMIN";
    public bool SpaceExists = true;
    public Dictionary<string, MemoDto> Memos = new();
    /// <summary>Listede gizlenen ama GetMemo ile görünen memolar (yanıt kaybı senaryosu).</summary>
    public HashSet<string> HiddenFromList = new();
    public List<string> Calls = new();
    /// <summary>Bir sonraki çağrıda fırlatılacak istisna (tek seferlik).</summary>
    public Exception? FailNext;
    /// <summary>n. CreateMemo çağrısında çevrimdışı hatası (tek seferlik); -1 = kapalı.</summary>
    public int OfflineOnCreateNumber = -1;
    /// <summary>true ise UpdateMemo 403 döner (yetki kalktı senaryosu).</summary>
    public bool ForbidUpdate;
    private int _createCount;

    private void Enter(string op)
    {
        Calls.Add(op);
        if (FailNext is { } e) { FailNext = null; throw e; }
    }

    public Task<MemosUser> GetMeAsync(CancellationToken ct = default)
    {
        Enter("me");
        return Task.FromResult(new MemosUser(Me, Me.Replace("users/", "")));
    }

    public Task<MemosUser?> GetUserAsync(string userName, CancellationToken ct = default)
    {
        Enter("user");
        return Task.FromResult<MemosUser?>(new MemosUser(userName, userName.Replace("users/", "")));
    }

    public Task<MemosSpace?> GetSpaceAsync(string spaceName, CancellationToken ct = default)
    {
        Enter("space");
        return Task.FromResult(SpaceExists ? new MemosSpace(Space, "Ev Alanı", Role) : null);
    }

    public Task<List<MemosSpace>> ListSpacesAsync(CancellationToken ct = default)
    {
        Enter("spaces");
        return Task.FromResult(new List<MemosSpace> { new(Space, "Ev Alanı", Role) });
    }

    public Task<List<MemoDto>> ListMemosAsync(string spaceName, string state, CancellationToken ct = default)
    {
        Enter("list:" + state);
        var list = Memos.Values
            .Where(m => m.Space == spaceName && m.State == state && !HiddenFromList.Contains(m.Name))
            .ToList();
        return Task.FromResult(list);
    }

    public Task<MemoDto?> GetMemoAsync(string memoName, CancellationToken ct = default)
    {
        Enter("get");
        return Task.FromResult(Memos.GetValueOrDefault(memoName));
    }

    public Task<MemoDto> CreateMemoAsync(string memoId, string spaceName, string visibility, string content, CancellationToken ct = default)
    {
        Enter("create");
        _createCount++;
        if (_createCount == OfflineOnCreateNumber)
        {
            OfflineOnCreateNumber = -1;
            throw new MemosOfflineException();
        }
        var name = "memos/" + memoId;
        if (Memos.ContainsKey(name)) throw new MemosAlreadyExistsException();
        var dto = new MemoDto(name, Me, content, "NORMAL", visibility, spaceName, Now());
        Memos[name] = dto;
        return Task.FromResult(dto);
    }

    public Task<MemoDto> UpdateMemoAsync(string memoName, string content, string state, CancellationToken ct = default)
    {
        Enter("update");
        if (ForbidUpdate) throw new MemosForbiddenException();
        var m = Memos[memoName] with { Content = content, State = state, UpdateTime = Now() };
        Memos[memoName] = m;
        return Task.FromResult(m);
    }

    /// <summary>Sunucu tarafında (başka bir kullanıcı/web) değişiklik.</summary>
    public void RemoteEdit(string name, string? content = null, string? state = null, string? creator = null, string? space = null)
    {
        var m = Memos[name];
        Memos[name] = m with
        {
            Content = content ?? m.Content,
            State = state ?? m.State,
            Creator = creator ?? m.Creator,
            Space = space ?? m.Space,
            UpdateTime = Now(),
        };
    }

    public MemoDto AddRemote(string name, string content, string creator = "users/host", string state = "NORMAL") =>
        Memos[name] = new MemoDto(name, creator, content, state, "SPACE", Space, Now());

    private static DateTime Now() => new(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

    public int Count(string op) => Calls.Count(c => c == op);
}

public static class TestRepo
{
    public static NoteRepository Create(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), "sn-sync-" + Guid.NewGuid().ToString("N") + ".db");
        return new NoteRepository(path, () => new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc));
    }

    public static void Cleanup(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { path, path + "-wal", path + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    /// <summary>Notes, sync_state ve window_state tablolarının metin dökümü (bayt karşılaştırması için).</summary>
    public static string Dump(string path)
    {
        using var c = new SqliteConnection("Data Source=" + path + ";Pooling=False");
        c.Open();
        var sb = new System.Text.StringBuilder();
        foreach (var table in new[] { "notes", "sync_state", "window_state" })
        {
            sb.AppendLine("#" + table);
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} ORDER BY 1";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                for (var i = 0; i < r.FieldCount; i++) sb.Append(r.IsDBNull(i) ? "∅" : r.GetValue(i)?.ToString()).Append('|');
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static List<Note> TextNotes(NoteRepository repo) =>
        repo.ListForSync();
}

/// <summary>HttpClient için sahte taşıyıcı: istekleri kaydeder, yanıtı fonksiyondan alır.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
    public List<HttpRequestMessage> Seen { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Seen.Add(request);
        return Task.FromResult(Respond(request));
    }

    public static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
