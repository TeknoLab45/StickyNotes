namespace StickyNotes.Core;

public static class NoteType
{
    public const string Text = "text";
    public const string Checklist = "checklist";
}

public static class NoteColors
{
    public static readonly string[] All = { "yellow", "green", "pink", "purple", "blue", "gray", "white" };
    public const string Default = "yellow";
    public static string Normalize(string? c) =>
        c != null && Array.IndexOf(All, c) >= 0 ? c : Default;
}

/// <summary>Sync-uyumlu not kaydı. Pencere durumu ayrı tutulur (NoteWindowState).</summary>
public sealed record Note(
    string Id,
    string Type,
    string Content,
    string Color,
    DateTime EditedAt,
    DateTime? DeletedAt,
    DateTime? PurgedAt,
    double Opacity = 1.0)
{
    public bool IsDeleted => DeletedAt != null;

    /// <summary>Oluşturma zamanı: Id UUIDv7 ise içindeki ms damgası, değilse EditedAt.</summary>
    public DateTime CreatedAt => NoteIds.CreatedAtUtc(Id) ?? EditedAt;
}

public static class NoteIds
{
    /// <summary>UUIDv7 kimliğinden oluşturma zamanını (UTC) çıkarır; v7 değilse null.</summary>
    public static DateTime? CreatedAtUtc(string id)
    {
        var hex = id.Replace("-", "");
        if (hex.Length != 32 || hex[12] != '7') return null;
        try
        {
            var ms = Convert.ToInt64(hex.Substring(0, 12), 16);
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
        catch (FormatException) { return null; }
    }
}

public static class NoteOpacity
{
    /// <summary>%100'den %20'ye 10'ar adım.</summary>
    public static readonly double[] Levels = { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2 };
    public const double Min = 0.2;
    public static double Normalize(double v) => double.IsNaN(v) ? 1.0 : Math.Min(1.0, Math.Max(Min, v));
}

public static class DateText
{
    /// <summary>Göreli tarih: "bugün 18:05", "dün 09:12", aksi halde "dd.MM.yyyy HH:mm". nowLocal yerel saat.</summary>
    public static string Relative(DateTime utc, DateTime nowLocal)
    {
        var l = utc.ToLocalTime();
        if (l.Date == nowLocal.Date) return "bugün " + l.ToString("HH:mm");
        if (l.Date == nowLocal.Date.AddDays(-1)) return "dün " + l.ToString("HH:mm");
        return l.ToString("dd.MM.yyyy HH:mm");
    }
}

/// <summary>Pencere konum/boyut durumu. Collapsed: yalnızca başlık satırı görünür; ExpandedHeight: genişletilince dönülecek yükseklik (0 = bilinmiyor).</summary>
public sealed record NoteWindowState(string NoteId, double X, double Y, double Width, double Height, bool IsOpen, bool Collapsed = false, double ExpandedHeight = 0);

public sealed record ChecklistItem(string Id, string Text, bool Done, int Indent = 0);
