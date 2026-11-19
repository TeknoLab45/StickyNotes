using System.Text.Json;

namespace StickyNotes.Core;

/// <summary>
/// content JSON biçimi: {"v":1,"text":"..."} (text) ya da {"v":1,"items":[{id,text,done,indent}]} (checklist).
/// Metin notunda isteğe bağlı biçim: "marks":[{"t":"b|u|s","s":başlangıç,"e":bitiş}] — s/e metindeki
/// UTF-16 kod birimi ofseti, [s,e) yarı açık. marks yoksa alan yazılmaz (eski biçimle bayt bayt aynı).
/// </summary>
public static class NoteContent
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string CreateText(string text, IEnumerable<Mark>? marks = null)
    {
        var list = marks is null ? new List<Mark>() : RichText.Normalize(marks, text.Length);
        if (list.Count == 0)
            return JsonSerializer.Serialize(new { v = 1, text }, Opts);
        return JsonSerializer.Serialize(new
        {
            v = 1,
            text,
            marks = list.Select(m => new { t = m.Type, s = m.Start, e = m.End })
        }, Opts);
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

    /// <summary>text -> checklist dönüşümü (content JSON üzerinden).</summary>
    public static string ToChecklist(string content) =>
        CreateChecklist(TextToItems(ParseText(content)));

    /// <summary>checklist -> text dönüşümü (content JSON üzerinden).</summary>
    public static string ToText(string content) =>
        CreateText(ItemsToText(ParseItems(content)));

    /// <summary>Arama için düz metin.</summary>
    public static string PlainText(string type, string content) =>
        type == NoteType.Checklist ? ItemsToText(ParseItems(content)) : ParseText(content);

    /// <summary>Checklist ilerlemesi: metni dolu maddelerden kaçı işaretli.</summary>
    public static (int Done, int Total) ChecklistProgress(string content)
    {
        var items = ParseItems(content).Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();
        return (items.Count(i => i.Done), items.Count);
    }

    /// <summary>Explorer önizleme satırı: checklist = "2/5 tamam"; metin = ilk satırdan sonraki dolu satırlar (boşsa "").</summary>
    public static string Preview(string type, string content)
    {
        if (type == NoteType.Checklist)
        {
            var (d, t) = ChecklistProgress(content);
            return t == 0 ? "" : $"{d}/{t} tamam";
        }
        var lines = PlainText(type, content).Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1);
        return string.Join(" · ", lines);
    }

    /// <summary>Explorer listesi için ilk dolu satır.</summary>
    public static string FirstLine(string type, string content)
    {
        var text = PlainText(type, content);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
        return "(boş not)";
    }
}
