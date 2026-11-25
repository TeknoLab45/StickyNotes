# TEK-150 senaryo E: 12 notla sürükleme ve yazma akıcılığı (SoftwareOnly + AllowsTransparency)
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Start-App
$ws = @(Note-Windows); "not sayisi: " + $ws.Count
$p = Get-Process StickyNotes
# sürükleme: not0 şeridi 500 px sağa/aşağı, 50 adım x 10 ms
$r = $ws[0].Current.BoundingRectangle
$cpu0 = $p.TotalProcessorTime.TotalMilliseconds; $sw = [Diagnostics.Stopwatch]::StartNew()
DragMouse ($r.X + 90) ($r.Y + 27) ($r.X + 90 + 420) ($r.Y + 27 + 300) 60 10
$sw.Stop(); $p.Refresh()
"SURUKLEME 60 adim: duvar=$([int]$sw.ElapsedMilliseconds) ms (ideal ~ 60*10+600=1200), uygulama CPU=$([int]($p.TotalProcessorTime.TotalMilliseconds - $cpu0)) ms"
"not0 yeni konum: " + (Rect (Note-Windows | Where-Object { $_.Current.BoundingRectangle.X -gt 300 -and $_.Current.BoundingRectangle.Y -gt 250 } | Select-Object -First 1))
# yazma: not 2 gövdesine 120 karakter
$ws = @(Note-Windows | Sort-Object { $_.Current.BoundingRectangle.Y }, { $_.Current.BoundingRectangle.X })
$tgt = $ws[3]; $te = Find-Id $tgt "TextEditor"; $c = Center $te; MClick $c[0] $c[1]; Start-Sleep -Milliseconds 500
$txt = "abcdefghij" * 12
$cpu0 = $p.TotalProcessorTime.TotalMilliseconds; $sw = [Diagnostics.Stopwatch]::StartNew()
[System.Windows.Forms.SendKeys]::SendWait($txt)
$ok = $false
for ($i = 0; $i -lt 100; $i++) { $v = $te.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value; if ($v.EndsWith($txt)) { $ok = $true; break }; Start-Sleep -Milliseconds 50 }
$sw.Stop(); $p.Refresh()
"YAZMA 120 karakter: tamam=$ok duvar=$([int]$sw.ElapsedMilliseconds) ms, uygulama CPU=$([int]($p.TotalProcessorTime.TotalMilliseconds - $cpu0)) ms"
Get-Process StickyNotes | Stop-Process -Force
