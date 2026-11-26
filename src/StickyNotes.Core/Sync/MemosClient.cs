using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StickyNotes.Core.Sync;

public sealed record MemosSpace(string Name, string Title, string CurrentUserRole);
public sealed record MemosUser(string Name, string Username);
public sealed record MemoDto(string Name, string Creator, string Content, string State, string Visibility, string Space, DateTime UpdateTime, DateTime? CreateTime = null);

/// <summary>Memos REST yüzeyi. Null dönüşü = 404 (kayıt yok).</summary>
public interface IMemosApi
{
    Task<MemosUser> GetMeAsync(CancellationToken ct = default);
    Task<MemosUser?> GetUserAsync(string userName, CancellationToken ct = default);
    Task<MemosSpace?> GetSpaceAsync(string spaceName, CancellationToken ct = default);
    Task<List<MemosSpace>> ListSpacesAsync(CancellationToken ct = default);
    Task<List<MemoDto>> ListMemosAsync(string spaceName, string state, CancellationToken ct = default);
    Task<MemoDto?> GetMemoAsync(string memoName, CancellationToken ct = default);
    /// <summary>memoId sabit kimlik olarak gönderilir; aynı kimlik varsa <see cref="MemosAlreadyExistsException"/>.</summary>
    Task<MemoDto> CreateMemoAsync(string memoId, string spaceName, string visibility, string content, CancellationToken ct = default);
    Task<MemoDto> UpdateMemoAsync(string memoName, string content, string state, CancellationToken ct = default);
    /// <summary>Memoyu sunucudan kalıcı siler. Sunucuda zaten yoksa (404) başarı sayılır.</summary>
    Task DeleteMemoAsync(string memoName, CancellationToken ct = default);
}

public class MemosException : Exception
{
    public MemosException(string message) : base(message) { }
}

/// <summary>401: token geçersiz/iptal.</summary>
public sealed class MemosUnauthorizedException : MemosException
{
    public MemosUnauthorizedException() : base("Token geçersiz ya da iptal edilmiş. Ayarlar'dan yeni token girin.") { }
}

/// <summary>403: bu kayıt için yetki yok.</summary>
public sealed class MemosForbiddenException : MemosException
{
    public MemosForbiddenException() : base("Bu işlem için yetkiniz yok.") { }
}

/// <summary>409 / AlreadyExists: aynı memoId zaten var.</summary>
public sealed class MemosAlreadyExistsException : MemosException
{
    public MemosAlreadyExistsException() : base("Bu kimlikle memo zaten var.") { }
}

/// <summary>Bağlantı kesildi ya da zaman aşımı: yazılmamış sayılır, sonraki turda tekrar denenir.</summary>
public sealed class MemosOfflineException : MemosException
{
    public MemosOfflineException(Exception? inner = null) : base("Sunucuya ulaşılamadı (çevrimdışı ya da zaman aşımı).") { }
}

/// <summary>Sunucu 0.31 ya da üstü değil: iki kök adresi de yanıt vermedi.</summary>
public sealed class MemosVersionException : MemosException
{
    public MemosVersionException() : base("Memos 0.31 veya üstü gerekli.") { }
}

/// <summary>
/// Memos istemcisi. API kökü önce /api (26.x), olmazsa /api/v1 (0.31) olarak algılanır;
/// hangisi 200 dönerse o kullanılır ve <see cref="ApiRoot"/> ile önbelleğe alınabilir.
/// Token asla log ya da hata metnine yazılmaz.
/// </summary>
public sealed class MemosClient : IMemosApi
{
    private static readonly string[] Roots = { "/api", "/api/v1" };
    private readonly HttpClient _http;
    private readonly string _token;
    private string? _root;

    public MemosClient(HttpClient http, string baseUrl, string token, string? cachedApiRoot = null)
    {
        _http = http;
        BaseUrl = baseUrl.TrimEnd('/');
        _token = token;
        _root = Roots.Contains(cachedApiRoot) ? cachedApiRoot : null;
    }

    public string BaseUrl { get; }

    /// <summary>Algılanmış kök (/api ya da /api/v1); algılanmadıysa null.</summary>
    public string? ApiRoot => _root;

    /// <summary>Kökü algılar (ya da önbelleği doğrular). Hiçbiri çalışmazsa <see cref="MemosVersionException"/>.</summary>
    public async Task<string> DetectRootAsync(CancellationToken ct = default)
    {
        var candidates = _root is null ? Roots : new[] { _root };
        foreach (var root in candidates)
        {
            var (status, _) = await SendRawAsync(HttpMethod.Get, root + "/instance/profile", null, ct);
            if (status == HttpStatusCode.OK)
            {
                _root = root;
                return root;
            }
            if (status == HttpStatusCode.Unauthorized) throw new MemosUnauthorizedException();
        }
        _root = null;
        throw new MemosVersionException();
    }

    private async Task<string> RootAsync(CancellationToken ct) => _root ?? await DetectRootAsync(ct);

    public async Task<MemosUser> GetMeAsync(CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var doc = await GetJsonAsync(root + "/auth/me", ct);
        return ParseUser(doc.RootElement.GetProperty("user"));
    }

    public async Task<MemosUser?> GetUserAsync(string userName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var doc = await GetOptionalJsonAsync(root + "/" + userName, ct);
        return doc is null ? null : ParseUser(doc.RootElement);
    }

    public async Task<MemosSpace?> GetSpaceAsync(string spaceName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var doc = await GetOptionalJsonAsync(root + "/" + spaceName, ct);
        return doc is null ? null : ParseSpace(doc.RootElement);
    }

    public async Task<List<MemosSpace>> ListSpacesAsync(CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var result = new List<MemosSpace>();
        string? token = null;
        do
        {
            var doc = await GetJsonAsync(root + "/spaces?pageSize=100" + PageParam(token), ct);
            foreach (var s in doc.RootElement.GetProperty("spaces").EnumerateArray()) result.Add(ParseSpace(s));
            token = NextPage(doc);
        } while (token != null);
        return result;
    }

    public async Task<List<MemoDto>> ListMemosAsync(string spaceName, string state, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var filter = Uri.EscapeDataString($"space == \"{spaceName}\"");
        var result = new List<MemoDto>();
        string? token = null;
        do
        {
            var url = $"{root}/memos?pageSize=100&state={state}&filter={filter}{PageParam(token)}";
            var doc = await GetJsonAsync(url, ct);
            foreach (var m in doc.RootElement.GetProperty("memos").EnumerateArray()) result.Add(ParseMemo(m));
            token = NextPage(doc);
        } while (token != null);
        return result;
    }

    public async Task<MemoDto?> GetMemoAsync(string memoName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var doc = await GetOptionalJsonAsync(root + "/" + memoName, ct);
        return doc is null ? null : ParseMemo(doc.RootElement);
    }

    public async Task<MemoDto> CreateMemoAsync(string memoId, string spaceName, string visibility, string content, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var body = JsonSerializer.Serialize(new { content, visibility, space = spaceName });
        var doc = await SendJsonAsync(HttpMethod.Post, $"{root}/memos?memoId={Uri.EscapeDataString(memoId)}", body, ct)
                  ?? throw new MemosException("Memo oluşturma yanıtı boş.");
        return ParseMemo(doc.RootElement);
    }

    public async Task<MemoDto> UpdateMemoAsync(string memoName, string content, string state, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var body = JsonSerializer.Serialize(new { content, state });
        var doc = await SendJsonAsync(HttpMethod.Patch, $"{root}/{memoName}?updateMask=content,state", body, ct)
                  ?? throw new MemosException("Memo güncelleme yanıtı boş.");
        return ParseMemo(doc.RootElement);
    }

    public async Task DeleteMemoAsync(string memoName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var (status, body) = await SendRawAsync(HttpMethod.Delete, root + "/" + memoName, null, ct);
        if (status == HttpStatusCode.NotFound) return;
        ThrowFor(status, body);
    }

    // ---- HTTP yardımcıları ----

    private static string PageParam(string? token) =>
        token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token);

    private static string? NextPage(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("nextPageToken", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } s
            ? s
            : null;

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        var (status, body) = await SendRawAsync(HttpMethod.Get, path, null, ct);
        ThrowFor(status, body);
        return JsonDocument.Parse(body);
    }

    /// <summary>404 → null (kayıt yok); diğer hatalar istisna.</summary>
    private async Task<JsonDocument?> GetOptionalJsonAsync(string path, CancellationToken ct)
    {
        var (status, body) = await SendRawAsync(HttpMethod.Get, path, null, ct);
        if (status == HttpStatusCode.NotFound) return null;
        ThrowFor(status, body);
        return JsonDocument.Parse(body);
    }

    private async Task<JsonDocument?> SendJsonAsync(HttpMethod method, string path, string json, CancellationToken ct)
    {
        var (status, body) = await SendRawAsync(method, path, json, ct);
        ThrowFor(status, body);
        return JsonDocument.Parse(body);
    }

    private void ThrowFor(HttpStatusCode status, string body)
    {
        var code = (int)status;
        if (code is >= 200 and < 300) return;
        switch (status)
        {
            case HttpStatusCode.Unauthorized: throw new MemosUnauthorizedException();
            case HttpStatusCode.Forbidden: throw new MemosForbiddenException();
            case HttpStatusCode.NotFound: throw new MemosException("Kayıt bulunamadı (404).");
            case HttpStatusCode.Conflict: throw new MemosAlreadyExistsException();
        }
        throw new MemosException($"Memos isteği başarısız ({code}).");
    }

    private async Task<(HttpStatusCode Status, string Body)> SendRawAsync(HttpMethod method, string path, string? json, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, BaseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (resp.StatusCode, body);
        }
        catch (HttpRequestException ex) { throw new MemosOfflineException(ex); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new MemosOfflineException(ex); }
    }

    // ---- DTO ayrıştırma (0.31 ve 26.x aynı alan adlarını kullanır) ----

    private static MemosUser ParseUser(JsonElement e) => new(
        Str(e, "name"), Str(e, "username"));

    private static MemosSpace ParseSpace(JsonElement e) => new(
        Str(e, "name"), Str(e, "title"), Str(e, "currentUserRole"));

    private static MemoDto ParseMemo(JsonElement e) => new(
        Str(e, "name"),
        Str(e, "creator"),
        Str(e, "content"),
        Str(e, "state"),
        Str(e, "visibility"),
        Str(e, "space"),
        Time(e, "updateTime") ?? DateTime.UtcNow,
        Time(e, "createTime"));

    private static DateTime? Time(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? DateTime.Parse(v.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime()
            : null;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
