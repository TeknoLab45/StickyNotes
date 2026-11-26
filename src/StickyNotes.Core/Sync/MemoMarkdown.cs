using System.Text;
using System.Text.RegularExpressions;

namespace StickyNotes.Core.Sync;

/// <summary>
/// Not içeriği ↔ Memos markdown'ı (saf, WPF'siz).
/// Başlık → ilk satır "# Başlık"; memonun ilk satırı "# ..." ise yerel başlık olur, değilse not başlıksızdır.
/// Gövde Markdown kaynağıdır ve aynen taşınır (listeler, görevler dahil).
/// Kalın → **x**, üstü çizili → ~~x~~, altı çizili sunucuya gitmez.
/// Bilinen kısıt: kullanıcının metninde kendiliğinden geçen "**x**" ya da "~~x~~" kalın/üstü çizili sanılır.
/// </summary>
public static partial class MemoMarkdown
{
    private const string BoldType = MarkTypes.Bold;
    private const string StrikeType = MarkTypes.Strike;

    [GeneratedRegex(@"^# (\S(?:.*\S)?)$")]
    private static partial Regex TitleLine();

    /// <summary>Satır başı: girinti ve varsa liste/görev/başlık işareti. Biçim işaretleri bunun içine yazılmaz.</summary>
    [GeneratedRegex(@"^\s*(?:(?:[-*+]|\d{1,9}[.)])\s+(?:\[[ xX]\](?:\s+|$))?|#{1,6}\s+)?")]
    private static partial Regex LinePrefix();

    /// <summary>Yerel notu (tür + content JSON) markdown'a çevirir.</summary>
    public static string FromNote(string type, string content)
    {
        var body = type == NoteType.Checklist
            ? ChecklistToMarkdown(NoteContent.ParseItems(content))
            : TextToMarkdown(NoteContent.ParseText(content), NoteContent.ParseMarks(content));
        var title = NoteContent.ParseTitle(content);
        if (title.Length == 0) return body;
        return body.Length == 0 ? "# " + title : "# " + title + "\n" + body;
    }

    /// <summary>Eski checklist notunun Markdown görev listesi: "- [ ] / - [x]", girinti = 2 boşluk × indent.</summary>
    private static string ChecklistToMarkdown(IEnumerable<ChecklistItem> items) =>
        string.Join("\n", items.Select(i => new string(' ', i.Indent * 2) + (i.Done ? "- [x] " : "- [ ] ") + i.Text));

    /// <summary>Memos markdown'ını (tür + content JSON) yerel nota çevirir. Sonuç her zaman metin notudur.</summary>
    public static (string Type, string Content) ToNote(string markdown)
    {
        var text = RichText.NormalizeNewlines(markdown);
        if (text.Trim().Length == 0)
            return (NoteType.Text, NoteContent.CreateText(""));

        var lines = text.Split('\n');
        var title = "";
        var heading = TitleLine().Match(lines[0]);
        if (heading.Success)
        {
            title = heading.Groups[1].Value;
            lines = lines[1..];
        }

        var plain = new StringBuilder();
        var marks = new List<Mark>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) plain.Append('\n');
            var baseOffset = plain.Length;
            var (lineText, lineMarks) = ParseLine(lines[i]);
            // İşaretler aynen geri üretilemiyorsa (ör. "2 ** 3 ** 4") satır olduğu gibi metin kalır.
            if (lineMarks.Count > 0 && LineToMarkdown(lineText, 0, lineText.Length, lineMarks) != lines[i])
                (lineText, lineMarks) = (lines[i], new List<Mark>());
            plain.Append(lineText);
            marks.AddRange(lineMarks.Select(m => m with { Start = m.Start + baseOffset, End = m.End + baseOffset }));
        }
        return (NoteType.Text, NoteContent.CreateText(plain.ToString(), marks, title));
    }

    private static string TextToMarkdown(string text, IReadOnlyList<Mark> marks)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == '\n')
            {
                lines.Add(LineToMarkdown(text, start, i, marks));
                start = i + 1;
            }
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Tek satır. Açık işaretler bir yığında tutulur; istenmeyen en alttaki işarete kadar kapatılır
    /// ve eksik olanlar açılır. Böylece aynı işaret art arda "****" gibi çakışmaz.
    /// </summary>
    private static string LineToMarkdown(string text, int lineStart, int lineEnd, IReadOnlyList<Mark> marks)
    {
        var line = text.Substring(lineStart, lineEnd - lineStart);
        var bold = Visible(line, lineStart, marks, BoldType);
        var strike = Visible(line, lineStart, marks, StrikeType);
        var sb = new StringBuilder();
        var stack = new List<char>();

        void CloseFrom(int k)
        {
            // Kapanış işareti boşluktan sonra gelirse Markdown biçimi tanımaz: sondaki boşlukların önüne yazılır.
            var at = sb.Length;
            while (at > 0 && char.IsWhiteSpace(sb[at - 1])) at--;
            for (var j = stack.Count - 1; j >= k; j--)
            {
                var m = Marker(stack[j]);
                sb.Insert(at, m);
                at += m.Length;
            }
            stack.RemoveRange(k, stack.Count - k);
        }

        for (var i = 0; i < line.Length; i++)
        {
            var want = new HashSet<char>();
            if (bold[i]) want.Add('b');
            if (strike[i]) want.Add('s');

            var k = stack.FindIndex(c => !want.Contains(c));
            if (k >= 0) CloseFrom(k);
            // Açılış işareti boşluktan önce yazılmaz: ilk boşluk olmayan karaktere ertelenir.
            if (!char.IsWhiteSpace(line[i]))
            {
                foreach (var c in new[] { 'b', 's' })
                {
                    if (want.Contains(c) && !stack.Contains(c))
                    {
                        sb.Append(Marker(c));
                        stack.Add(c);
                    }
                }
            }
            sb.Append(line[i]);
        }
        if (stack.Count > 0) CloseFrom(0);
        return sb.ToString();
    }

    /// <summary>Satırın hangi karakterleri bu biçimde yazılır: satır başı işareti (liste vb.) ve aralık uçlarındaki boşluklar hariç.</summary>
    private static bool[] Visible(string line, int lineStart, IReadOnlyList<Mark> marks, string type)
    {
        var on = new bool[line.Length];
        var i = LinePrefix().Match(line).Length;
        while (i < line.Length)
        {
            if (!RichText.TypesAt(marks, lineStart + i).Contains(type)) { i++; continue; }
            var end = i;
            while (end < line.Length && RichText.TypesAt(marks, lineStart + end).Contains(type)) end++;
            var a = i;
            var b = end;
            while (a < b && char.IsWhiteSpace(line[a])) a++;
            while (b > a && char.IsWhiteSpace(line[b - 1])) b--;
            for (var j = a; j < b; j++) on[j] = true;
            i = end;
        }
        return on;
    }

    private static string Marker(char c) => c == 'b' ? "**" : "~~";

    /// <summary>Bir satırdaki ** ve ~~ işaretlerini biçime çevirir; işaret yalnızca satırda eşleşen bir karşılığı varsa işlenir.</summary>
    private static (string Text, List<Mark> Marks) ParseLine(string line)
    {
        var sb = new StringBuilder();
        var marks = new List<Mark>();
        var bold = false;
        var strike = false;
        var boldStart = 0;
        var strikeStart = 0;

        var i = 0;
        while (i < line.Length)
        {
            if (IsMarker(line, i, "**") && (bold || line.IndexOf("**", i + 2, StringComparison.Ordinal) >= 0))
            {
                if (bold) marks.Add(new Mark(BoldType, boldStart, sb.Length));
                else boldStart = sb.Length;
                bold = !bold;
                i += 2;
            }
            else if (IsMarker(line, i, "~~") && (strike || line.IndexOf("~~", i + 2, StringComparison.Ordinal) >= 0))
            {
                if (strike) marks.Add(new Mark(StrikeType, strikeStart, sb.Length));
                else strikeStart = sb.Length;
                strike = !strike;
                i += 2;
            }
            else
            {
                sb.Append(line[i]);
                i++;
            }
        }
        if (bold) marks.Add(new Mark(BoldType, boldStart, sb.Length));
        if (strike) marks.Add(new Mark(StrikeType, strikeStart, sb.Length));
        return (sb.ToString(), marks);
    }

    private static bool IsMarker(string s, int i, string marker) =>
        i + 1 < s.Length && s[i] == marker[0] && s[i + 1] == marker[1];

    /// <summary>Yerel içerik için karşılaştırma parmak izi. Çöp durumu yalnızca düzenlenebilir notlarda sayılır.</summary>
    public static string Fingerprint(string markdown, bool trashed)
    {
        var body = RichText.NormalizeNewlines(markdown).TrimEnd();
        var raw = Encoding.UTF8.GetBytes(body + "\u0001" + (trashed ? "1" : "0"));
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(raw));
    }
}
