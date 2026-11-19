using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using StickyNotes.Core;

namespace StickyNotes.App;

/// <summary>Not pencereleri, kayıt ve Explorer koordinasyonu.</summary>
public sealed class NoteManager
{
    private readonly Dictionary<string, NoteWindow> _windows = new();
    private ExplorerWindow? _explorer;
    private int _cascade;

    private SettingsWindow? _settingsWindow;

    public NoteRepository Repo { get; }

    /// <summary>Bu PC'ye özel ayarlar (settings tablosu).</summary>
    public AppSettings Settings { get; private set; }

    public event Action? NotesChanged;
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>Ayarlar penceresi aktifken global kısayollar askıdadır (yeni birleşim yakalanabilsin).</summary>
    public bool HotkeysSuspended { get; private set; }
    public event Action? HotkeysSuspendedChanged;

    public void SetHotkeysSuspended(bool value)
    {
        if (HotkeysSuspended == value) return;
        HotkeysSuspended = value;
        HotkeysSuspendedChanged?.Invoke();
    }

    public NoteManager(NoteRepository repo)
    {
        Repo = repo;
        Settings = repo.LoadSettings();
    }

    /// <summary>Ayarı değiştirir, kaydeder; yazı ayarları açık tüm notlara anında uygulanır.</summary>
    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        var s = change(Settings).Normalized();
        if (s == Settings) return;
        Settings = s;
        try { Repo.SaveSettings(s); } catch (Exception ex) { AppLog.Error("Ayarlar kaydedilemedi", ex); }
        foreach (var w in _windows.Values) w.ApplySettings(s);
        SettingsChanged?.Invoke(s);
    }

    /// <summary>Notun rengi/opaklığı/boyutu/türü yeni notlar için varsayılan olur.</summary>
    public void MakeDefault(NoteWindow w)
    {
        UpdateSettings(s => s with
        {
            DefaultColor = w.Vm.Color,
            DefaultOpacity = w.Vm.NoteOpacity,
            DefaultType = w.Vm.Type,
            DefaultWidth = w.VisibleWidth,
            DefaultHeight = w.ExpandedHeight,
        });
        AppLog.Info("Varsayılan not ayarları güncellendi (not " + w.NoteId + ")");
    }

    public void ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            if (_settingsWindow.WindowState == System.Windows.WindowState.Minimized)
                _settingsWindow.WindowState = System.Windows.WindowState.Normal;
            _settingsWindow.Activate();
        }
    }

    /// <summary>Alt+H: tüm notları gizle; her not is_open=0 kaydedilir (tray'deki "gizle" ile aynı).</summary>
    public void HideAll()
    {
        foreach (var w in _windows.Values.ToList())
            if (w.IsVisible) w.HideNote();
        AppLog.Info("Tüm notlar gizlendi");
    }

    public void NotifyChanged() => NotesChanged?.Invoke();

    /// <summary>Açılışta: açık olarak kaydedilen notları göster. Hiç not yoksa bir tane oluştur.</summary>
    public void Startup()
    {
        var notes = Repo.ListActive();
        if (notes.Count == 0)
        {
            NewNote();
            return;
        }
        foreach (var n in notes)
        {
            var ws = Repo.GetWindow(n.Id);
            if (ws == null || ws.IsOpen) OpenNote(n.Id);
        }
        AppLog.Info($"Başlangıç: {notes.Count} not, {_windows.Values.Count(w => w.IsVisible)} pencere açık");
    }

    public void NewNote()
    {
        var d = Settings;
        var note = Repo.Create(d.DefaultType, d.DefaultColor);
        if (d.DefaultOpacity < 1.0) { Repo.SetOpacity(note.Id, d.DefaultOpacity); note = Repo.Get(note.Id) ?? note; }
        AppLog.Info("Yeni not " + note.Id);
        var w = OpenNote(note.Id);
        w?.Activate();
        w?.FocusBody();
        NotifyChanged();
    }

    /// <summary>Notu aç/göster; pencere yoksa oluşturur.</summary>
    public NoteWindow? OpenNote(string id)
    {
        if (_windows.TryGetValue(id, out var existing))
        {
            if (!existing.IsVisible) existing.Show();
            existing.Activate();
            existing.Flush();
            NotifyChanged();
            return existing;
        }
        var note = Repo.Get(id);
        if (note == null || note.IsDeleted) return null;
        var w = new NoteWindow(this, new NoteViewModel(note));
        w.ApplySettings(Settings);
        w.Restore(Repo.GetWindow(id), _cascade++, Settings);
        _windows[id] = w;
        w.Show();
        w.Flush();
        NotifyChanged();
        return w;
    }

    public void ShowAll()
    {
        foreach (var n in Repo.ListActive()) OpenNote(n.Id);
    }

    public void SaveNote(NoteViewModel vm, NoteWindow w)
    {
        try
        {
            var content = vm.BuildContent();
            var sig = vm.Type + "|" + vm.Color + "|" + content;
            if (sig != vm.SavedSignature)
            {
                Repo.Update(vm.Id, vm.Type, content, vm.Color);
                vm.SavedSignature = sig;
            }
            if (Math.Abs(vm.NoteOpacity - vm.SavedOpacity) > 1e-9)
            {
                Repo.SetOpacity(vm.Id, vm.NoteOpacity);
                vm.SavedOpacity = vm.NoteOpacity;
            }
            var visible = w.IsVisible;
            // Gizliyken Left/Top geçerli kalır; boyut/konum her durumda yazılır.
            // Görünür dikdörtgen kaydedilir (gölge payı hariç) — yeniden açılışta kayma olmaz.
            Repo.SaveWindow(new NoteWindowState(vm.Id, w.VisibleLeft, w.VisibleTop, w.VisibleWidth, w.VisibleHeight,
                visible, w.IsCollapsed, w.ExpandedHeight));
            NotifyChanged();
        }
        catch (Exception ex) { AppLog.Error("Not kaydedilemedi " + vm.Id, ex); }
    }

    /// <summary>Tray: tüm notları %100 opak yap (açık pencereler ve DB).</summary>
    public void MakeAllOpaque()
    {
        foreach (var w in _windows.Values) { w.Vm.NoteOpacity = 1.0; w.Flush(); }
        var n = Repo.SetAllOpacity(1.0);
        AppLog.Info($"Tüm notlar opak yapıldı ({n} satır)");
        NotifyChanged();
    }

    public void DeleteNote(string id)
    {
        if (_windows.TryGetValue(id, out var w))
        {
            w.Flush();
            w.CloseForReal();
            _windows.Remove(id);
        }
        Repo.SoftDelete(id);
        AppLog.Info("Çöpe atıldı " + id);
        NotifyChanged();
    }

    public void RestoreNote(string id)
    {
        if (Repo.Restore(id))
        {
            AppLog.Info("Geri alındı " + id);
            OpenNote(id);
        }
        NotifyChanged();
    }

    public void PurgeNote(string id)
    {
        Repo.Purge(id);
        AppLog.Info("Kalıcı silindi " + id);
        NotifyChanged();
    }

    public void EmptyTrash()
    {
        var n = Repo.EmptyTrash();
        AppLog.Info($"Çöp boşaltıldı ({n})");
        NotifyChanged();
    }

    public void ShowExplorer(bool focusSearch = false)
    {
        if (_explorer == null)
        {
            _explorer = new ExplorerWindow(this);
            _explorer.Closed += (_, _) => _explorer = null;
            _explorer.Show();
        }
        else
        {
            if (_explorer.WindowState == System.Windows.WindowState.Minimized)
                _explorer.WindowState = System.Windows.WindowState.Normal;
            _explorer.Activate();
        }
        if (focusSearch) _explorer.FocusSearch();
    }

    /// <summary>Çıkış: bekleyen kayıtları yaz, pencereleri kapat.</summary>
    public void Shutdown()
    {
        foreach (var w in _windows.Values.ToList())
        {
            try { w.Flush(); w.CloseForReal(); } catch (Exception ex) { AppLog.Error("Kapatma hatası", ex); }
        }
        _windows.Clear();
        _explorer?.Close();
        _settingsWindow?.Close();
    }
}
