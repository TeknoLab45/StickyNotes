using System.Globalization;

namespace StickyNotes.Core;

public static class NoteFontStyle
{
    public const string Normal = "normal";
    public const string Bold = "bold";
    public const string Italic = "italic";
    public static readonly string[] All = { Normal, Bold, Italic };
    public static string Normalize(string? s) => s != null && Array.IndexOf(All, s) >= 0 ? s : Normal;
    public static string Label(string s) => s switch { Bold => "Kalın", Italic => "İtalik", _ => "Normal" };
}

/// <summary>
/// Bu PC'ye özel ayarlar (settings tablosu; senkronizasyona girmez).
/// Yazı ayarları (FontFamily/FontSize/FontStyle) tüm notlara anında uygulanır; Default* yalnızca YENİ notlar içindir.
/// Kısayol metni "Alt+H" biçimindedir; boş = kapalı.
/// </summary>
public sealed record AppSettings
{
    public const double MinFontSize = 10, MaxFontSize = 24;
    public const double MinNoteSize = 140, MaxNoteSize = 2000;

    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 14;
    public string FontStyle { get; init; } = NoteFontStyle.Normal;

    public string DefaultColor { get; init; } = NoteColors.Default;
    public double DefaultOpacity { get; init; } = 1.0;
    public double DefaultWidth { get; init; } = 260;
    public double DefaultHeight { get; init; } = 260;

    public string HotkeyHideAll { get; init; } = "Alt+H";
    public string HotkeyShowAll { get; init; } = "Alt+S";
    public string HotkeyExplorer { get; init; } = "Alt+X";
    public string HotkeyNewNote { get; init; } = "Alt+N";

    /// <summary>Tüm alanları geçerli aralığa çeker.</summary>
    public AppSettings Normalized() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily.Trim(),
        FontSize = double.IsNaN(FontSize) ? 14 : Math.Round(Math.Min(MaxFontSize, Math.Max(MinFontSize, FontSize))),
        FontStyle = NoteFontStyle.Normalize(FontStyle),
        DefaultColor = NoteColors.Normalize(DefaultColor),
        DefaultOpacity = NoteOpacity.Normalize(DefaultOpacity),
        DefaultWidth = ClampSize(DefaultWidth, 260),
        DefaultHeight = ClampSize(DefaultHeight, 260),
        HotkeyHideAll = (HotkeyHideAll ?? "").Trim(),
        HotkeyShowAll = (HotkeyShowAll ?? "").Trim(),
        HotkeyExplorer = (HotkeyExplorer ?? "").Trim(),
        HotkeyNewNote = (HotkeyNewNote ?? "").Trim(),
    };

    private static double ClampSize(double v, double fallback) =>
        double.IsNaN(v) ? fallback : Math.Min(MaxNoteSize, Math.Max(MinNoteSize, v));

    // ---- key/value (settings tablosu) ----
    private static string D(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static double P(string? s, double fb) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fb;

    public Dictionary<string, string> ToPairs() => new()
    {
        ["font_family"] = FontFamily, ["font_size"] = D(FontSize), ["font_style"] = FontStyle,
        ["default_color"] = DefaultColor, ["default_opacity"] = D(DefaultOpacity),
        ["default_width"] = D(DefaultWidth), ["default_height"] = D(DefaultHeight),
        ["hotkey_hide_all"] = HotkeyHideAll, ["hotkey_show_all"] = HotkeyShowAll,
        ["hotkey_explorer"] = HotkeyExplorer, ["hotkey_new_note"] = HotkeyNewNote,
    };

    /// <summary>Eksik/bozuk anahtarlar varsayılana düşer.</summary>
    public static AppSettings FromPairs(IReadOnlyDictionary<string, string> kv)
    {
        var d = new AppSettings();
        string S(string k, string fb) => kv.TryGetValue(k, out var v) ? v : fb;
        return new AppSettings
        {
            FontFamily = S("font_family", d.FontFamily),
            FontSize = P(kv.GetValueOrDefault("font_size"), d.FontSize),
            FontStyle = S("font_style", d.FontStyle),
            DefaultColor = S("default_color", d.DefaultColor),
            DefaultOpacity = P(kv.GetValueOrDefault("default_opacity"), d.DefaultOpacity),
            DefaultWidth = P(kv.GetValueOrDefault("default_width"), d.DefaultWidth),
            DefaultHeight = P(kv.GetValueOrDefault("default_height"), d.DefaultHeight),
            HotkeyHideAll = S("hotkey_hide_all", d.HotkeyHideAll),
            HotkeyShowAll = S("hotkey_show_all", d.HotkeyShowAll),
            HotkeyExplorer = S("hotkey_explorer", d.HotkeyExplorer),
            HotkeyNewNote = S("hotkey_new_note", d.HotkeyNewNote),
        }.Normalized();
    }
}

/// <summary>Kenara yapışma: saf hesap (hepsi aynı birimde olmalı: piksel ya da DIP).</summary>
[Flags]
public enum SnapEdges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, Move = Left | Top | Right | Bottom }

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public static class WindowSnap
{
    /// <summary>
    /// <paramref name="r"/> notun GÖRÜNÜR dikdörtgeni (gölge payı hariç), <paramref name="work"/> monitör çalışma alanı.
    /// <see cref="SnapEdges.Move"/>: boyut sabit, konum çalışma alanına sığdırılır ve kenara ≤ eşik yaklaşınca oturur.
    /// Tek tek kenarlar (boyutlandırma): yalnızca o kenarlar hareket eder; çalışma alanı dışına çıkamaz;
    /// minWidth/minHeight korunur.
    /// </summary>
    public static RectD Snap(RectD r, RectD work, double threshold, SnapEdges edges = SnapEdges.Move,
        double minWidth = 0, double minHeight = 0)
    {
        if (edges == SnapEdges.None) return r;
        if (edges == SnapEdges.Move)
        {
            var w = Math.Min(r.Width, work.Width);
            var h = Math.Min(r.Height, work.Height);
            return new RectD(Axis(r.X, w, work.X, work.Right, threshold), Axis(r.Y, h, work.Y, work.Bottom, threshold), w, h);
        }

        double l = r.X, t = r.Y, rt = r.Right, b = r.Bottom;
        if (edges.HasFlag(SnapEdges.Left)) l = Edge(l, work.X, work.Right, threshold);
        if (edges.HasFlag(SnapEdges.Right)) rt = Edge(rt, work.X, work.Right, threshold);
        if (edges.HasFlag(SnapEdges.Top)) t = Edge(t, work.Y, work.Bottom, threshold);
        if (edges.HasFlag(SnapEdges.Bottom)) b = Edge(b, work.Y, work.Bottom, threshold);

        if (rt - l < minWidth)
        {
            if (edges.HasFlag(SnapEdges.Left) && !edges.HasFlag(SnapEdges.Right)) l = rt - minWidth;
            else rt = l + minWidth;
        }
        if (b - t < minHeight)
        {
            if (edges.HasFlag(SnapEdges.Top) && !edges.HasFlag(SnapEdges.Bottom)) t = b - minHeight;
            else b = t + minHeight;
        }
        return new RectD(l, t, rt - l, b - t);
    }

    private static double Axis(double pos, double size, double lo, double hi, double th)
    {
        pos = Math.Min(Math.Max(pos, lo), hi - size);
        if (Math.Abs(pos - lo) <= th) return lo;
        if (Math.Abs(pos + size - hi) <= th) return hi - size;
        return pos;
    }

    private static double Edge(double e, double lo, double hi, double th)
    {
        e = Math.Min(Math.Max(e, lo), hi);
        if (Math.Abs(e - lo) <= th) return lo;
        if (Math.Abs(e - hi) <= th) return hi;
        return e;
    }
}
