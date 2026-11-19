using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StickyNotes.Core;

namespace StickyNotes.App;

/// <summary>Ayarlar: değişiklikler anında uygulanır ve SQLite'ta (settings tablosu) saklanır.</summary>
public partial class SettingsWindow : Window
{
    private readonly NoteManager _manager;
    private bool _loading = true;

    public SettingsWindow(NoteManager manager)
    {
        _manager = manager;
        InitializeComponent();

        foreach (var f in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            FontFamilyCombo.Items.Add(f);
        foreach (var s in NoteFontStyle.All) FontStyleCombo.Items.Add(new Choice<string>(s, NoteFontStyle.Label(s)));
        foreach (var c in NoteColors.All) DefColorCombo.Items.Add(new Choice<string>(c, NoteTheme.Label(c)));
        foreach (var o in NoteOpacity.Levels) DefOpacityCombo.Items.Add(new Choice<double>(o, "%" + (int)Math.Round(o * 100)));
        DefTypeCombo.Items.Add(new Choice<string>(NoteType.Text, "Metin"));
        DefTypeCombo.Items.Add(new Choice<string>(NoteType.Checklist, "Checklist"));

        Load(_manager.Settings);
        _loading = false;

        // Kısayol kutusuna yeni birleşim basılabilsin diye, bu pencere aktifken global kısayollar askıya alınır
        Activated += (_, _) => _manager.SetHotkeysSuspended(true);
        Deactivated += (_, _) => _manager.SetHotkeysSuspended(false);
        Closed += (_, _) => _manager.SetHotkeysSuspended(false);
    }

    private sealed record Choice<T>(T Value, string Text) { public override string ToString() => Text; }

    private void Load(AppSettings s)
    {
        FontFamilyCombo.SelectedItem = FontFamilyCombo.Items.Cast<string>()
            .FirstOrDefault(f => string.Equals(f, s.FontFamily, StringComparison.OrdinalIgnoreCase));
        FontSizeSlider.Value = s.FontSize;
        FontSizeText.Text = s.FontSize.ToString("0", CultureInfo.CurrentCulture);
        Select(FontStyleCombo, s.FontStyle);
        Select(DefColorCombo, s.DefaultColor);
        Select(DefOpacityCombo, NoteOpacity.Levels.OrderBy(l => Math.Abs(l - s.DefaultOpacity)).First());
        Select(DefTypeCombo, s.DefaultType);
        DefWidthBox.Text = s.DefaultWidth.ToString("0", CultureInfo.InvariantCulture);
        DefHeightBox.Text = s.DefaultHeight.ToString("0", CultureInfo.InvariantCulture);
        HkHideBox.Text = s.HotkeyHideAll;
        HkShowBox.Text = s.HotkeyShowAll;
        HkExplorerBox.Text = s.HotkeyExplorer;
        HkNewBox.Text = s.HotkeyNewNote;
    }

    private static void Select<T>(ComboBox cb, T value) =>
        cb.SelectedItem = cb.Items.OfType<Choice<T>>().FirstOrDefault(c => Equals(c.Value, value));

    private static T? Get<T>(ComboBox cb) => cb.SelectedItem is Choice<T> c ? c.Value : default;

    private void Font_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _manager.UpdateSettings(s => s with
        {
            FontFamily = FontFamilyCombo.SelectedItem as string ?? s.FontFamily,
            FontStyle = Get<string>(FontStyleCombo) ?? s.FontStyle,
        });
    }

    private void FontSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeText != null) FontSizeText.Text = e.NewValue.ToString("0", CultureInfo.CurrentCulture);
        if (_loading) return;
        _manager.UpdateSettings(s => s with { FontSize = e.NewValue });
    }

    private void Defaults_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _manager.UpdateSettings(s => s with
        {
            DefaultColor = Get<string>(DefColorCombo) ?? s.DefaultColor,
            DefaultOpacity = DefOpacityCombo.SelectedItem is Choice<double> o ? o.Value : s.DefaultOpacity,
            DefaultType = Get<string>(DefTypeCombo) ?? s.DefaultType,
        });
    }

    private void Size_Commit(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _manager.UpdateSettings(s => s with
        {
            DefaultWidth = Parse(DefWidthBox.Text, s.DefaultWidth),
            DefaultHeight = Parse(DefHeightBox.Text, s.DefaultHeight),
        });
        // geçerli aralığa çekilmiş değeri göster
        DefWidthBox.Text = _manager.Settings.DefaultWidth.ToString("0", CultureInfo.InvariantCulture);
        DefHeightBox.Text = _manager.Settings.DefaultHeight.ToString("0", CultureInfo.InvariantCulture);
    }

    private static double Parse(string t, double fb) =>
        double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fb;

    private void Size_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Size_Commit(sender, e); e.Handled = true; }
    }

    private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape) { e.Handled = false; return; }
        string? combo;
        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None) combo = "";
        else combo = HotkeyManager.Format(Keyboard.Modifiers, key);
        if (combo == null) return;   // yalnızca değiştirici ya da değiştiricisiz tuş: bekle
        tb.Text = combo;
        var which = tb.Tag as string;
        _manager.UpdateSettings(s => which switch
        {
            "hide" => s with { HotkeyHideAll = combo },
            "show" => s with { HotkeyShowAll = combo },
            "explorer" => s with { HotkeyExplorer = combo },
            _ => s with { HotkeyNewNote = combo },
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
