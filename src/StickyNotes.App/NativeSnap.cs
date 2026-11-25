using System;
using System.Runtime.InteropServices;
using StickyNotes.Core;

namespace StickyNotes.App;

/// <summary>
/// WM_MOVING / WM_SIZING yakalama ve monitör çalışma alanı. Hesabın kendisi Core'daki WindowSnap.Snap'tedir (saf, testli);
/// burası yalnızca piksel dikdörtgenleri ve monitör bilgisini ona bağlar.
/// </summary>
internal static class NativeSnap
{
    public const int WM_SIZING = 0x0214, WM_MOVING = 0x0216;
    /// <summary>Kenara yapışma eşiği (DIP; DPI ile ölçeklenir).</summary>
    public const double ThresholdDip = 12;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT lprc, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>Verilen piksel dikdörtgenine en yakın monitörün çalışma alanı (piksel).</summary>
    public static RectD WorkAreaPx(RectD px)
    {
        var r = new RECT { Left = (int)px.X, Top = (int)px.Y, Right = (int)px.Right, Bottom = (int)px.Bottom };
        var mon = MonitorFromRect(ref r, 2 /*MONITOR_DEFAULTTONEAREST*/);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
            return new RectD(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
        var a = System.Windows.SystemParameters.WorkArea;
        return new RectD(a.Left, a.Top, a.Width, a.Height);
    }

    private static SnapEdges EdgesFromWmsz(int w) => w switch
    {
        1 => SnapEdges.Left,
        2 => SnapEdges.Right,
        3 => SnapEdges.Top,
        4 => SnapEdges.Left | SnapEdges.Top,
        5 => SnapEdges.Right | SnapEdges.Top,
        6 => SnapEdges.Bottom,
        7 => SnapEdges.Left | SnapEdges.Bottom,
        8 => SnapEdges.Right | SnapEdges.Bottom,
        _ => SnapEdges.None
    };

    /// <summary>
    /// WM_MOVING/WM_SIZING için lParam RECT'ini (pencere, piksel) görünür dikdörtgene göre yapıştırıp geri yazar.
    /// padDip: her kenardaki gölge payı. scale: DPI ölçeği. minWDip/minHDip: görünür asgari boyut (DIP).
    /// </summary>
    public static bool Handle(int msg, IntPtr wParam, IntPtr lParam, double scale, double padDip, double minWDip, double minHDip)
    {
        if (msg != WM_MOVING && msg != WM_SIZING) return false;
        var rc = Marshal.PtrToStructure<RECT>(lParam);
        var pad = padDip * scale;
        var visible = new RectD(rc.Left + pad, rc.Top + pad, rc.Right - rc.Left - 2 * pad, rc.Bottom - rc.Top - 2 * pad);
        var work = WorkAreaPx(visible);
        var edges = msg == WM_MOVING ? SnapEdges.Move : EdgesFromWmsz((int)wParam);
        var s = WindowSnap.Snap(visible, work, ThresholdDip * scale, edges, minWDip * scale, minHDip * scale);
        rc.Left = (int)Math.Round(s.X - pad);
        rc.Top = (int)Math.Round(s.Y - pad);
        rc.Right = (int)Math.Round(s.Right + pad);
        rc.Bottom = (int)Math.Round(s.Bottom + pad);
        Marshal.StructureToPtr(rc, lParam, false);
        return true;
    }

    public const int WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232, WM_WINDOWPOSCHANGING = 0x0046;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
    private const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>Sürükleme döngüsünde WM_WINDOWPOSCHANGING: yalnızca konum değişiyorsa (boyut sabit) Move yapıştırması uygular.</summary>
    public static void HandlePos(IntPtr lParam, double scale, double padDip)
    {
        var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
        if ((wp.flags & SWP_NOMOVE) != 0) return;
        var pad = padDip * scale;
        var visible = new RectD(wp.x + pad, wp.y + pad, wp.cx - 2 * pad, wp.cy - 2 * pad);
        var s = WindowSnap.Snap(visible, WorkAreaPx(visible), ThresholdDip * scale);
        wp.x = (int)Math.Round(s.X - pad);
        wp.y = (int)Math.Round(s.Y - pad);
        Marshal.StructureToPtr(wp, lParam, false);
    }

    /// <summary>Taşıma/boyutlandırma bitince pencerenin gerçek konumunu bir kez daha yapıştırır (idempotent).</summary>
    public static void FixUp(IntPtr hwnd, double scale, double padDip)
    {
        if (!GetWindowRect(hwnd, out var rc)) return;
        var pad = padDip * scale;
        var visible = new RectD(rc.Left + pad, rc.Top + pad, rc.Right - rc.Left - 2 * pad, rc.Bottom - rc.Top - 2 * pad);
        var s = WindowSnap.Snap(visible, WorkAreaPx(visible), ThresholdDip * scale);
        int x = (int)Math.Round(s.X - pad), y = (int)Math.Round(s.Y - pad);
        int w = (int)Math.Round(s.Width + 2 * pad), h = (int)Math.Round(s.Height + 2 * pad);
        if (x != rc.Left || y != rc.Top || w != rc.Right - rc.Left || h != rc.Bottom - rc.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>Konum geri yüklerken (DIP, görünür dikdörtgen) aynı yardımcıyı kullanır.</summary>
    public static RectD SnapDip(RectD visibleDip, double scale)
    {
        var px = new RectD(visibleDip.X * scale, visibleDip.Y * scale, visibleDip.Width * scale, visibleDip.Height * scale);
        var s = WindowSnap.Snap(px, WorkAreaPx(px), ThresholdDip * scale);
        return new RectD(s.X / scale, s.Y / scale, s.Width / scale, s.Height / scale);
    }
}
