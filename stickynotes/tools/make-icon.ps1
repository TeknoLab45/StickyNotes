# Basit, kendi çizdiğimiz sticky note ikonunu (PNG gömülü .ico) üretir. Kullanım: make-icon.ps1 <çıktı.ico>
param([string]$Out = "src/StickyNotes.App/Assets/app.ico")
Add-Type -AssemblyName System.Drawing
$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)
$shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
$g.FillRectangle($shadow, 8, 9, 50, 50)
$yellow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 247, 214, 64))
$g.FillRectangle($yellow, 5, 5, 50, 50)
$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 185, 30))
$g.FillRectangle($dark, 5, 5, 50, 11)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 120, 95, 10)), 3
$g.DrawLine($pen, 13, 28, 47, 28)
$g.DrawLine($pen, 13, 38, 47, 38)
$g.DrawLine($pen, 13, 48, 35, 48)
$g.Dispose()
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$dir = Split-Path -Parent $Out
if ($dir) { New-Item -ItemType Directory -Force $dir | Out-Null }
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
$bw.Write([byte]$size); $bw.Write([byte]$size); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
$bw.Write($png)
$bw.Close(); $fs.Close()
Write-Host "ikon yazildi: $Out"
