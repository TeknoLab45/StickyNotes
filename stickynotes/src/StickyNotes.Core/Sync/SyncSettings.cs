namespace StickyNotes.Core.Sync;

public static class MemosVisibility
{
    public const string Space = "SPACE";
    public const string Private = "PRIVATE";
    public static string Normalize(string? v) => v == Private ? Private : Space;
}

/// <summary>
/// Memos eşitleme ayarları. Bu PC'ye özel (settings tablosu; senkronizasyona girmez).
/// Token DPAPI ile App katmanında şifrelenir; Core yalnızca şifreli metni taşır (memos_token_enc).
/// </summary>
public sealed record SyncSettings
{
    public bool Enabled { get; init; }
    /// <summary>Sunucu adresi, örn. http://192.168.1.10:5230 (sonunda / yok).</summary>
    public string Url { get; init; } = "";
    /// <summary>Space kimliği: "spaces/&lt;id&gt;". Boş = eşitleme yapılmaz.</summary>
    public string Space { get; init; } = "";
    public string SpaceTitle { get; init; } = "";
    public string Visibility { get; init; } = MemosVisibility.Space;
    public int IntervalMin { get; init; } = 2;
    public bool Paused { get; init; }
    /// <summary>Önbelleğe alınmış API kökü: "/api" ya da "/api/v1"; boş = henüz algılanmadı.</summary>
    public string ApiRoot { get; init; } = "";
    /// <summary>DPAPI ile şifrelenmiş token (base64). Düz token asla burada tutulmaz.</summary>
    public string TokenEnc { get; init; } = "";

    public SyncSettings Normalized() => this with
    {
        Url = (Url ?? "").Trim().TrimEnd('/'),
        Space = (Space ?? "").Trim(),
        SpaceTitle = (SpaceTitle ?? "").Trim(),
        Visibility = MemosVisibility.Normalize(Visibility),
        IntervalMin = Math.Clamp(IntervalMin, 1, 60),
        ApiRoot = ApiRoot == "/api" || ApiRoot == "/api/v1" ? ApiRoot : "",
        TokenEnc = TokenEnc ?? "",
    };

    public Dictionary<string, string> ToPairs() => new()
    {
        ["memos_enabled"] = Enabled ? "1" : "0",
        ["memos_url"] = Url,
        ["memos_space"] = Space,
        ["memos_space_title"] = SpaceTitle,
        ["memos_visibility"] = Visibility,
        ["memos_interval_min"] = IntervalMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["memos_paused"] = Paused ? "1" : "0",
        ["memos_api_root"] = ApiRoot,
        ["memos_token_enc"] = TokenEnc,
    };

    public static SyncSettings FromPairs(IReadOnlyDictionary<string, string> kv)
    {
        string S(string k) => kv.TryGetValue(k, out var v) ? v : "";
        var interval = int.TryParse(S("memos_interval_min"), out var n) ? n : 2;
        return new SyncSettings
        {
            Enabled = S("memos_enabled") == "1",
            Url = S("memos_url"),
            Space = S("memos_space"),
            SpaceTitle = S("memos_space_title"),
            Visibility = S("memos_visibility"),
            IntervalMin = interval,
            Paused = S("memos_paused") == "1",
            ApiRoot = S("memos_api_root"),
            TokenEnc = S("memos_token_enc"),
        }.Normalized();
    }
}
