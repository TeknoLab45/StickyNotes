# Baska program gibi davranir: Alt+H global kisayolunu tutar (test icin)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Windows.Forms;
public class HK : Form { [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint vk);
 public HK(){ ShowInTaskbar=false; WindowState=FormWindowState.Minimized; Opacity=0; Load += (s,e)=>{ Console.WriteLine(RegisterHotKey(Handle,1,1,0x48)); }; } }
"@ -ReferencedAssemblies System.Windows.Forms
[Windows.Forms.Application]::Run((New-Object HK))
