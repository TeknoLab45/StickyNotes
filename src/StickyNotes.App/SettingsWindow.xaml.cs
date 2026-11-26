using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StickyNotes.Core;
using StickyNotes.Core.Sync;

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
        // Küçük ekranlarda pencere çalışma alanını aşmasın; içerik ScrollViewer ile kaydırılır
        MaxHeight = SystemParameters.WorkArea.Height;

        foreach (var f in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            FontFamilyCombo.Items.Add(f);
        foreach (var s in NoteFontStyle.All) FontStyleCombo.Items.Add(new Choice<string>(s, NoteFontStyle.Label(s)));
        foreach (var c in NoteColors.All) DefColorCombo.Items.Add(new Choice<string>(c, NoteTheme.Label(c)));
        foreach (var o in NoteOpacity.Levels) DefOpacityCombo.Items.Add(new Choice<double>(o, "%" + (int)Math.Round(o * 100)));

        MemosVisibilityCombo.Items.Add(new Choice<string>(MemosVisibility.Space, "Alan üyeleri (SPACE)"));
        MemosVisibilityCombo.Items.Add(new Choice<string>(MemosVisibility.Private, "Özel (PRIVATE)"));

        Load(_manager.Settings);
        LoadSync();
        _loading = false;

        // Kısayol kutusuna yeni birleşim basılabilsin diye, bu pencere aktifken global kısayollar askıya alınır
        Activated += (_, _) => _manager.SetHotkeysSuspended(true);
        Deactivated += (_, _) => _manager.SetHotkeysSuspended(false);
        Closed += (_, _) => _manager.SetHotkeysSuspended(false);

        if (Sync is { } sync)
        {
            sync.StatusChanged += RefreshSyncStatus;
            Closed += (_, _) => sync.StatusChanged -= RefreshSyncStatus;
        }
    }

    private static SyncService? Sync => (Application.Current as App)?.Sync;

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

    // ---------- Memos eşitlemesi ----------

    private void LoadSync()
    {
        var s = _manager.Repo.LoadSyncSettings();
        MemosUrlBox.Text = s.Url;
        MemosIntervalBox.Text = s.IntervalMin.ToString(CultureInfo.InvariantCulture);
        MemosEnabledCheck.IsChecked = s.Enabled;
        Select(MemosVisibilityCombo, s.Visibility);
        MemosSpaceCombo.Items.Clear();
        if (s.Space.Length > 0) MemosSpaceCombo.Items.Add(new Choice<string>(s.Space, s.SpaceTitle.Length > 0 ? s.SpaceTitle : s.Space));
        Select(MemosSpaceCombo, s.Space);
        MemosStatus.Text = s.TokenEnc.Length > 0 ? "Kayıtlı bir token var. Değiştirmek için yeni token girip Kaydet'e basın." : "Henüz token yok. 'Token al' ile Memos'ta oluşturun.";
        RefreshSyncStatus();
    }

    private void Memos_Status(string text) => MemosStatus.Text = text;

    /// <summary>Bağlantı ayarlarını kaydeder, zamanlayıcıyı yeniler ve durumu gösterir.</summary>
    private void SaveSync(Func<SyncSettings, SyncSettings> change)
    {
        var next = change(_manager.Repo.LoadSyncSettings()).Normalized();
        _manager.Repo.SaveSyncSettings(next);
        Sync?.Reconfigure();
        RefreshSyncStatus();
    }

    private void RefreshSyncStatus()
    {
        var s = _manager.Repo.LoadSyncSettings();
        var running = Sync?.IsRunning ?? false;
        MemosSpaceHint.Visibility = s.Space.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        MemosSyncNowButton.IsEnabled = !running && s.Enabled && s.Space.Length > 0;
        MemosResumeButton.Visibility = s.Paused ? Visibility.Visible : Visibility.Collapsed;
        MemosSyncState.Text = "Durum: " + (Sync?.StatusText() ?? "Memos: kapalı");
        if (Sync?.LastResult is { Status: not SyncStatus.Ok } r && r.Message.Length > 0)
            MemosSyncState.Text += " — " + r.Message;
    }

    private void MemosUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { MemosUrl_Commit(sender, e); e.Handled = true; }
    }

    private void MemosUrl_Commit(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var url = MemosUrlBox.Text.Trim().TrimEnd('/');
        var current = _manager.Repo.LoadSyncSettings();
        if (url == current.Url) return;
        var validUrl = url.Length == 0
                       || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"));
        if (!validUrl)
        {
            Memos_Status("Adres http:// ya da https:// ile başlamalı.");
            MemosUrlBox.Text = current.Url;
            return;
        }
        // Başka sunucu = başka Space'ler: alan seçimi ve eşitleme sıfırlanır, token korunur
        SaveSync(s => s with { Url = url, ApiRoot = "", Space = "", SpaceTitle = "", Enabled = false, Paused = false });
        LoadSync();
        Memos_Status(url.Length == 0 ? "Sunucu adresi temizlendi." : "Adres değişti: 'Bağlantıyı test et' ile bağlanın ve alan seçin.");
    }

    private void MemosTokenSave_Click(object sender, RoutedEventArgs e)
    {
        var token = MemosTokenBox.Password.Trim();
        if (token.Length == 0) { Memos_Status("Token boş."); return; }
        SaveSync(s => s with { TokenEnc = TokenStore.Protect(token), Paused = false });
        MemosTokenBox.Password = "";
        MemosTokenSaveButton.Content = "Kaydedildi ✓";
        Memos_Status("Token kaydedildi (bu Windows hesabına bağlı olarak şifrelendi). 'Bağlantıyı test et' ile doğrulayın.");
    }

    // Yeni token yazılınca "Kaydedildi ✓" ipucu sıfırlanır
    private void MemosToken_Changed(object sender, RoutedEventArgs e) => MemosTokenSaveButton.Content = "Kaydet";

    private void MemosGetToken_Click(object sender, RoutedEventArgs e)
    {
        var url = MemosUrlBox.Text.Trim().TrimEnd('/');
        if (url.Length == 0) { Memos_Status("Önce sunucu adresini girin."); return; }
        try
        {
            // Memos'un token ayarları sayfası (web): /setting#access-token (docs/SENKRON.md)
            Process.Start(new ProcessStartInfo(url + "/setting#access-token") { UseShellExecute = true });
            Memos_Status("Tarayıcıda açılan sayfada yeni bir token oluşturup kopyalayın, sonra buraya yapıştırın.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Token sayfası açılamadı", ex);
            Memos_Status("Tarayıcı açılamadı. Adresi elle açın: " + url + "/setting#access-token");
        }
    }

    private async void MemosTest_Click(object sender, RoutedEventArgs e)
    {
        var s = _manager.Repo.LoadSyncSettings();
        var token = TokenStore.Unprotect(s.TokenEnc);
        if (s.Url.Length == 0) { Memos_Status("Önce sunucu adresini girin."); return; }
        if (token == null) { Memos_Status("Önce token girin ve Kaydet'e basın."); return; }

        MemosTestButton.IsEnabled = false;
        Memos_Status("Bağlanılıyor…");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var client = new MemosClient(http, s.Url, token, s.ApiRoot);
            await client.DetectRootAsync();
            var me = await client.GetMeAsync();
            var spaces = await client.ListSpacesAsync();
            SaveSync(x => x with { ApiRoot = client.ApiRoot ?? x.ApiRoot });

            // Alan listesi: yalnızca üyesi olunan Space'ler; seçili olan korunur
            _loading = true;
            try
            {
                MemosSpaceCombo.Items.Clear();
                foreach (var sp in spaces) MemosSpaceCombo.Items.Add(new Choice<string>(sp.Name, sp.Title.Length > 0 ? sp.Title : sp.Name));
                Select(MemosSpaceCombo, s.Space);
            }
            finally { _loading = false; }

            Memos_Status($"Bağlandı: {me.Username}. Üye olunan {spaces.Count} alan var. Alan seçip 'Eşitleme açık' kutusunu işaretleyin.");
        }
        catch (MemosException ex)
        {
            Memos_Status(ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Error("Memos bağlantı testi", ex);
            Memos_Status("Beklenmeyen bir hata oluştu; ayrıntı günlükte.");
        }
        finally
        {
            MemosTestButton.IsEnabled = true;
            RefreshSyncStatus();
        }
    }

    private void MemosSpace_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var current = _manager.Repo.LoadSyncSettings();
        var newSpace = Get<string>(MemosSpaceCombo) ?? "";
        if (newSpace == current.Space) return;

        if (current.Space.Length > 0)
        {
            var old = _manager.Repo.ListSyncState().Count(r => r.Space == current.Space);
            if (old > 0 && MessageBox.Show(this,
                    $"Eski alandaki {old} not yerelde kalacak ama artık eşitlenmeyecek. Devam edilsin mi?",
                    "Sticky Notes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                _loading = true;
                try { Select(MemosSpaceCombo, current.Space); }
                finally { _loading = false; }
                return;
            }
        }
        var title = (MemosSpaceCombo.SelectedItem as Choice<string>)?.Text ?? "";
        SaveSync(s => s with { Space = newSpace, SpaceTitle = title, Paused = false });
        Memos_Status(newSpace.Length == 0 ? "Alan seçilmedi; eşitleme yapılmaz." : "Alan seçildi: " + title);
    }

    private void MemosVisibility_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MemosVisibilityCombo.SelectedItem is not Choice<string> c) return;
        SaveSync(s => s with { Visibility = c.Value });
    }

    private void MemosEnabled_Click(object sender, RoutedEventArgs e)
    {
        var on = MemosEnabledCheck.IsChecked == true;
        var current = _manager.Repo.LoadSyncSettings();
        if (on && current.Space.Length == 0)
        {
            MemosEnabledCheck.IsChecked = false;
            Memos_Status("Eşitleme için önce bir Space (alan) seçin.");
            return;
        }
        if (on && current.TokenEnc.Length == 0)
        {
            MemosEnabledCheck.IsChecked = false;
            Memos_Status("Eşitleme için önce token girin.");
            return;
        }
        SaveSync(s => s with { Enabled = on, Paused = false });
        if (on) _ = Sync?.RunNowAsync();
    }

    private void MemosInterval_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { MemosInterval_Commit(sender, e); e.Handled = true; }
    }

    private void MemosInterval_Commit(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var n = int.TryParse(MemosIntervalBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 2;
        SaveSync(s => s with { IntervalMin = n });
        MemosIntervalBox.Text = _manager.Repo.LoadSyncSettings().IntervalMin.ToString(CultureInfo.InvariantCulture);
    }

    private void MemosSyncNow_Click(object sender, RoutedEventArgs e) => _ = Sync?.RunNowAsync();

    private void MemosResume_Click(object sender, RoutedEventArgs e) => _ = Sync?.RunNowAsync(resume: true);
}
