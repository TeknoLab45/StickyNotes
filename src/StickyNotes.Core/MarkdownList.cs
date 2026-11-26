using System.Globalization;
using System.Text.RegularExpressions;

namespace StickyNotes.Core;

/// <summary>Markdown liste satırı: işaretin uzunluğu, Enter'da yeni satıra yazılacak işaret, maddenin boş olup olmadığı.</summary>
public sealed record ListLine(int PrefixLength, string NextPrefix, bool IsEmpty);

/// <summary>Editördeki liste davranışının saf mantığı: "- ", "* ", "+ ", "1. ", "1) " ve görev "- [ ] ".</summary>
public static partial class MarkdownList
{
    public const string Indent = "  ";

    [GeneratedRegex(@"^([ \t]*)([-*+]|\d{1,9}[.)])( +)(\[[ xX]\](?: +|$))?")]
    private static partial Regex Prefix();

    /// <summary>Satır liste maddesi değilse null.</summary>
    public static ListLine? Parse(string line)
    {
        var m = Prefix().Match(line);
        if (!m.Success) return null;
        var marker = m.Groups[2].Value;
        if (char.IsDigit(marker[0]))
            marker = (long.Parse(marker[..^1], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) + marker[^1];
        var next = m.Groups[1].Value + marker + " " + (m.Groups[4].Success ? "[ ] " : "");
        return new ListLine(m.Length, next, string.IsNullOrWhiteSpace(line[m.Length..]));
    }

    /// <summary>Shift+Tab'da satır başından silinecek karakter sayısı (bir sekme ya da en çok iki boşluk).</summary>
    public static int OutdentLength(string line)
    {
        if (line.StartsWith('\t')) return 1;
        var n = 0;
        while (n < Indent.Length && n < line.Length && line[n] == ' ') n++;
        return n;
    }
}
