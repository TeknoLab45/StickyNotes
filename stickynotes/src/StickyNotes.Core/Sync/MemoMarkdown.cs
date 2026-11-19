using System.Text;
using System.Text.RegularExpressions;

namespace StickyNotes.Core.Sync;

/// <summary>
/// Not içeriği ↔ Memos markdown'ı (saf, WPF'siz).
/// Metin: kalın → **x**, üstü çizili → ~~x~~, altı çizili sunucuya gitmez (düz metin).
/// Checklist: "- [ ] madde" / "- [x] madde", girinti = 2 boşluk × indent.
/// Yabancı markdown (başlık, bağlantı, liste vb.) düz metin olarak korunur.
/// Bilinen kısıt: kullanıcının metninde kendiliğinden geçen "**" ya da "~~" çifti kalın/üstü çizili sanılır.
/// </summary>
public static partial class MemoMarkdown
{
    private const string BoldType = MarkTypes.Bold;
    private const string StrikeType = MarkTypes.Strike;

    [GeneratedRegex(@"^(\s*)- \[([ xX])\] ?(.*)$")]
    private static partial Regex ChecklistLine();

    /// <summary>Yerel notu (tür + content JSON) markdown'a çevirir.</summary>
    public static string FromNote(string type, string content)
    {
        if (type == NoteType.Checklist)
        {
            var sb = new StringBuilder();
            foreach (var item in NoteContent.ParseItems(content))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(new string(' ', item.Indent * 2));
                sb.Append(item.Done ? "- [x] " : "- [ ] ");
                sb.Append(item.Text);
            }
            return sb.ToString();
        }
        return TextToMarkdown(NoteContent.ParseText(content), NoteContent.ParseMarks(content));
    }

    /// <summary>Memos markdown'ını (tür + content JSON) yerel nota çevirir. Checklist yalnızca her satır madde ise.</summary>
    public static (string Type, string Content) ToNote(string markdown)
    {
        var text = RichText.NormalizeNewlines(markdown);
        if (text.Trim().Length == 0)
            return (NoteType.Text, NoteContent.CreateText(""));

        var lines = text.Split('\n');
        if (lines.All(l => ChecklistLine().IsMatch(l)))
        {
            var items = lines.Select(l =>
            {
                var m = ChecklistLine().Match(l);
                var indent = m.Groups[1].Value.Length / 2;
                return NoteContent.NewItem(m.Groups[3].Value, m.Groups[2].Value != " ", indent);
            }).ToList();
            return (NoteType.Checklist, NoteContent.CreateChecklist(items));
        }

        var plain = new StringBuilder();
        var marks = new List<Mark>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) plain.Append('\n');
            var baseOffset = plain.Length;
            var (lineText, lineMarks) = ParseLine(lines[i]);
            plain.Append(lineText);
            marks.AddRange(lineMarks.Select(m => m with { Start = m.Start + baseOffset, End = m.End + baseOffset }));
        }
        return (NoteType.Text, NoteContent.CreateText(plain.ToString(), marks));
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
        var sb = new StringBuilder();
        var stack = new List<char>();
        for (var i = lineStart; i < lineEnd; i++)
        {
            var types = RichText.TypesAt(marks, i);
            var want = new HashSet<char>();
            if (types.Contains(BoldType)) want.Add('b');
            if (types.Contains(StrikeType)) want.Add('s');

            var k = stack.FindIndex(c => !want.Contains(c));
            if (k >= 0)
            {
                for (var j = stack.Count - 1; j >= k; j--) sb.Append(Marker(stack[j]));
                stack.RemoveRange(k, stack.Count - k);
            }
            foreach (var c in new[] { 'b', 's' })
            {
                if (want.Contains(c) && !stack.Contains(c))
                {
                    sb.Append(Marker(c));
                    stack.Add(c);
                }
            }
            sb.Append(text[i]);
        }
        for (var j = stack.Count - 1; j >= 0; j--) sb.Append(Marker(stack[j]));
        return sb.ToString();
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
