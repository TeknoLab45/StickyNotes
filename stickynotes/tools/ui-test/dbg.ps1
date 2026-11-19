Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out int[] r);' -Name W -Namespace Win32 -ErrorAction SilentlyContinue
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class WR { [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 public static string Get(IntPtr h){ RECT r; GetWindowRect(h, out r); return r.L+","+r.T+" "+r.R+","+r.B; } }
"@
function WRect($el) { [WR]::Get([IntPtr]$el.Current.NativeWindowHandle) }
Start-App
$n = @(Note-Windows)[0]; "start: UIA " + (Rect $n) + " win " + (WRect $n)
$r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy ($sx - 1500) ($sy - 900) 30 15
$n = @(Note-Windows)[0]; "disari: UIA " + (Rect $n) + " win " + (WRect $n)
$r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy ($sx + 9) ($sy + 200) 20 15
$n = @(Note-Windows)[0]; "sol9: UIA " + (Rect $n) + " win " + (WRect $n)
Get-Process StickyNotes | Stop-Process -Force
