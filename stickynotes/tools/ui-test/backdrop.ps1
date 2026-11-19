# Test arka planı: tam ekran, tek renkli, topmost olmayan form. Kullanım: powershell -STA -File backdrop.ps1 "#F2F2F0"
param([string]$Color = "#F2F2F0")
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$f = New-Object Windows.Forms.Form
$f.FormBorderStyle = 'None'; $f.StartPosition = 'Manual'; $f.Bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
$f.BackColor = [Drawing.ColorTranslator]::FromHtml($Color); $f.ShowInTaskbar = $false
[Windows.Forms.Application]::Run($f)
