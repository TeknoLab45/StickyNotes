namespace StickyNotes.Core;

/// <summary>Biçim türleri: b = kalın, u = altı çizili, s = üstü çizili.</summary>
public static class MarkTypes
{
    public const string Bold = "b";
    public const string Underline = "u";
    public const string Strike = "s";
    public static readonly string[] All = { Bold, Underline, Strike };
}

/// <summary>Notun metninde [Start, End) UTF-16 kod birimi aralığına uygulanan biçim.</summary>
public sealed record Mark(string Type, int Start, int End);

/// <summary>Saf biçim mantığı (WPF'ten bağımsız, Linux'ta test edilir).</summary>
public static class RichText
{
    /// <summary>Satır sonlarını yalnızca \n olacak şekilde normalize eder.</summary>
    public static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>
    /// Bilinmeyen türleri atar, aralıkları metin uzunluğuna kırpar, boşları atar,
    /// aynı türde çakışan/bitişik aralıkları birleştirir ve (tür, başlangıç) sırasına koyar.
    /// </summary>
    public static List<Mark> Normalize(IEnumerable<Mark> marks, int textLength)
    {
        var clipped = marks
            .Where(m => MarkTypes.All.Contains(m.Type))
            .Select(m => m with { Start = Math.Max(0, m.Start), End = Math.Min(textLength, m.End) })
            .Where(m => m.Start < m.End);

        var result = new List<Mark>();
        foreach (var group in clipped.GroupBy(m => m.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Mark? current = null;
            foreach (var m in group.OrderBy(m => m.Start))
            {
                if (current is { } c && m.Start <= c.End)
                    current = c with { End = Math.Max(c.End, m.End) };
                else
                {
                    if (current is not null) result.Add(current);
                    current = m;
                }
            }
            if (current is not null) result.Add(current);
        }
        return result;
    }

    /// <summary>[start,end) tamamen o türle kaplı mı. Boş aralık için false.</summary>
    public static bool IsFullyMarked(IReadOnlyList<Mark> marks, string type, int start, int end)
    {
        if (start >= end) return false;
        return Normalize(marks, int.MaxValue)
            .Any(m => m.Type == type && m.Start <= start && m.End >= end);
    }

    /// <summary>
    /// Aralık tamamen o türle kaplıysa türü aralıktan çıkarır (gerekirse ikiye böler),
    /// değilse tüm aralığa ekler. Boş aralık girdiyi değiştirmez (yalnızca normalize edilir).
    /// </summary>
    public static List<Mark> Toggle(IReadOnlyList<Mark> marks, string type, int start, int end, int textLength)
    {
        var s = Math.Clamp(start, 0, textLength);
        var e = Math.Clamp(end, 0, textLength);
        var list = Normalize(marks, textLength);
        if (s >= e) return list;

        if (IsFullyMarked(list, type, s, e))
        {
            var kept = new List<Mark>();
            foreach (var m in list)
            {
                if (m.Type != type || m.End <= s || m.Start >= e)
                {
                    kept.Add(m);
                    continue;
                }
                if (m.Start < s) kept.Add(m with { End = s });
                if (m.End > e) kept.Add(m with { Start = e });
            }
            return Normalize(kept, textLength);
        }

        list.Add(new Mark(type, s, e));
        return Normalize(list, textLength);
    }

    /// <summary>index karakterini kaplayan türler (bitiş hariç).</summary>
    public static HashSet<string> TypesAt(IReadOnlyList<Mark> marks, int index) =>
        marks.Where(m => MarkTypes.All.Contains(m.Type) && m.Start <= index && index < m.End)
            .Select(m => m.Type)
            .ToHashSet();
}
