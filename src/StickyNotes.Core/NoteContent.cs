using System.Globalization;
using System.Text.Json;

namespace StickyNotes.Core;

/// <summary>
/// content JSON biçimi: {"v":1,"title":"...","text":"..."}; title isteğe bağlı (boşsa yazılmaz).
/// Metin Markdown kaynağıdır. İsteğe bağlı biçim: "marks":[{"t":"b|u|s","s":başlangıç,"e":bitiş}] — s/e text içindeki
/// UTF-16 kod birimi ofseti, [s,e) yarı açık. marks yoksa alan yazılmaz (eski biçimle bayt bayt aynı).
/// Eski checklist biçimi {"v":1,"items":[...]} yalnızca okunur (depo açılışında metne çevrilir).
/// </summary>
public static class NoteContent
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string CreateText(string text, IEnumerable<Mark>? marks = null, string? title = null)
    {
        var root = new Dictionary<string, object> { ["v"] = 1 };
        var t = NormalizeTitle(title);
        if (t.Length > 0) root["title"] = t;
        root["text"] = text;
        var list = marks is null ? new List<Mark>() : RichText.Normalize(marks, text.Length);
        if (list.Count > 0) root["marks"] = list.Select(m => new { t = m.Type, s = m.Start, e = m.End });
        return JsonSerializer.Serialize(root, Opts);
    }

    /// <summary>Başlık tek satırdır: satır sonları boşluk olur, uçlar kırpılır.</summary>
    public static string NormalizeTitle(string? title) =>
        title is null ? "" : RichText.NormalizeNewlines(title).Replace('\n', ' ').Trim();

    /// <summary>Notun kayıtlı başlığı; yoksa ya da içerik bozuksa "".</summary>
    public static string ParseTitle(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                return NormalizeTitle(t.GetString());
        }
        catch (JsonException) { }
        return "";
    }

    /// <summary>Ekranda gösterilen başlık: kayıtlı başlık, yoksa oluşturma tarihi (yerel saat).</summary>
    public static string DisplayTitle(string? title, DateTime createdUtc)
    {
        var t = NormalizeTitle(title);
        return t.Length > 0 ? t : createdUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Metin notundaki biçim aralıkları. Alan yoksa, bozuksa ya da kök nesne değilse boş liste; asla exception fırlatmaz.
    /// Sonuç metin uzunluğuna göre normalize edilir.
    /// </summary>
    public static List<Mark> ParseMarks(string content)
    {
        var list = new List<Mark>();
        var textLength = 0;
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return list;

            if (root.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                textLength = tx.GetString()?.Length ?? 0;

            if (root.TryGetProperty("marks", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    if (!e.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.String) continue;
                    if (!TryInt(e, "s", out var start) || !TryInt(e, "e", out var end)) continue;
                    list.Add(new Mark(t.GetString() ?? "", start, end));
                }
            }
        }
        catch (JsonException) { return new List<Mark>(); }
        return RichText.Normalize(list, textLength);
    }

    private static bool TryInt(JsonElement obj, string name, out int value)
    {
        value = 0;
        return obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out value);
    }

    public static string CreateChecklist(IEnumerable<ChecklistItem> items) =>
        JsonSerializer.Serialize(new
        {
            v = 1,
            items = items.Select(i => new { id = i.Id, text = i.Text, done = i.Done, indent = i.Indent })
        }, Opts);

    public static string Empty(string type) =>
        type == NoteType.Checklist
            ? CreateChecklist(new[] { NewItem("") })
            : CreateText("");

    public static ChecklistItem NewItem(string text, bool done = false, int indent = 0) =>
        new(Guid.CreateVersion7().ToString(), text, done, indent);

    public static string ParseText(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString() ?? "";
            if (root.TryGetProperty("items", out _))
                return ItemsToText(ParseItems(content));
        }
        catch (JsonException) { }
        return "";
    }

    public static List<ChecklistItem> ParseItems(string content)
    {
        var list = new List<ChecklistItem>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var id = e.TryGetProperty("id", out var i) ? i.GetString() : null;
                    var text = e.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    var done = e.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True;
                    var indent = e.TryGetProperty("indent", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0;
                    list.Add(new ChecklistItem(id ?? Guid.CreateVersion7().ToString(), text, done, indent));
                }
            }
            else if (root.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
            {
                return TextToItems(tx.GetString() ?? "");
            }
        }
        catch (JsonException) { }
        return list;
    }

    /// <summary>Metni satır satır checklist maddesine çevirir (boş metin = bir boş madde).</summary>
    public static List<ChecklistItem> TextToItems(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines.Select(l => NewItem(l)).ToList();
    }

    public static string ItemsToText(IEnumerable<ChecklistItem> items) =>
        string.Join("\n", items.Select(i => i.Text));

    /// <summary>Arama için düz metin (başlık + içerik).</summary>
    public static string PlainText(string type, string content)
    {
        var body = type == NoteType.Checklist ? ItemsToText(ParseItems(content)) : ParseText(content);
        var title = ParseTitle(content);
        return title.Length > 0 ? title + "\n" + body : body;
    }

    /// <summary>Explorer önizleme satırı: içeriğin dolu satırları.</summary>
    public static string Preview(string content) =>
        string.Join(" · ", RichText.NormalizeNewlines(ParseText(content)).Split('\n')
            .Select(l => l.Trim()).Where(l => l.Length > 0));
}
