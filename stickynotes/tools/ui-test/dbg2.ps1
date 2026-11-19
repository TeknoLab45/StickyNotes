Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class WR { [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 public static string Get(IntPtr h){ RECT r; GetWindowRect(h, out r); return r.L+","+r.T+" "+r.R+","+r.B; } }
"@
function WRect($el) { [WR]::Get([IntPtr]$el.Current.NativeWindowHandle) }
Start-App
$n = @(Note-Windows)[0]; $r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
# strip y offset 27 -> cursor y 27+? ; hedef: gorunur top 5 => pencere Y=-7 => cursor y = -7+27 = 20 
DragMouse $sx $sy ($sx) 20 20 15
"top5: win " + (WRect (@(Note-Windows)[0]))
$n = @(Note-Windows)[0]; $r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy $sx 0 20 15
"cursor y=0: win " + (WRect (@(Note-Windows)[0]))
$n = @(Note-Windows)[0]; $r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy $sx ($wa=700) 20 15
"alt: win " + (WRect (@(Note-Windows)[0]))
Get-Process StickyNotes | Stop-Process -Force
