using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace StickyNotes.App;

/// <summary>Global kısayollar (RegisterHotKey). Başka program aynı kısayolu tutuyorsa çökmez; başarısızları döndürür.</summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();

    public HotkeyManager()
    {
        // Yalnızca mesaj alan (HWND_MESSAGE) görünmez pencere
        var p = new HwndSourceParameters("StickyNotesHotkeys") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue((int)wParam, out var a))
        {
            handled = true;
            try { a(); } catch (Exception ex) { AppLog.Error("Kısayol eylemi hatası", ex); }
        }
        return IntPtr.Zero;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
    }

    /// <summary>Kısayolu kaydeder. Boş metin = kapalı (true). Başarısızsa false ve neden.</summary>
    public bool Register(int id, string combo, Action action, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(combo)) return true;
        if (!TryParse(combo, out var mods, out var vk))
        {
            error = "geçersiz kısayol";
            return false;
        }
        if (!RegisterHotKey(_source.Handle, id, mods | MOD_NOREPEAT, vk))
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }
        _actions[id] = action;
        return true;
    }

    /// <summary>"Alt+H", "Ctrl+Shift+F5" biçimi. En az bir değiştirici (Ctrl/Alt/Shift/Win) zorunlu.</summary>
    public static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        Key? key = null;
        foreach (var raw in combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": mods |= MOD_WIN; break;
                default:
                    if (key != null) return false;
                    var name = raw.Length == 1 && char.IsDigit(raw[0]) ? "D" + raw : raw;
                    if (!Enum.TryParse<Key>(name, true, out var k) || k == Key.None) return false;
                    key = k;
                    break;
            }
        }
        if (key == null || mods == 0) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        return vk != 0;
    }

    /// <summary>Tuş vuruşundan kısayol metni üretir; yalnızca değiştirici basılıysa ya da değiştirici yoksa null.</summary>
    public static string? Format(ModifierKeys mods, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.None) return null;
        if ((mods & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows)) == 0) return null;
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        var ks = key.ToString();
        if (ks.Length == 2 && ks[0] == 'D' && char.IsDigit(ks[1])) ks = ks.Substring(1);
        parts.Add(ks);
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }
}
