# TEK-150 senaryo D: opaklık kalıcılığı (yeniden başlatma), konum kayması, tray "Tüm notları opak yap"
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
$dark = Start-Backdrop "#1E1F24"; Start-Sleep 2
Start-App
$ws = @(Note-Windows)
$rects1 = ($ws | ForEach-Object { Rect $_ }) -join " ; "
$cb = Find-Id $ws[0] "ColorButton"; $c = Center $cb; MClick $c[0] $c[1]; Start-Sleep 1
$item = Find-Name "%70"; $c = Center $item; MClick $c[0] $c[1]; Start-Sleep 2
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$r0 = $ws[0].Current.BoundingRectangle
"1.oturum not0 %70 piksel (uzakta): " + (Px ([int]($r0.X + 150)) ([int]($r0.Y + 200)))
Get-Process StickyNotes | Stop-Process -Force; Start-Sleep 1
Start-App
$ws = @(Note-Windows)
$rects2 = ($ws | ForEach-Object { Rect $_ }) -join " ; "
"KONUM once : $rects1"
"KONUM sonra: $rects2"
"konum ayni mi: " + ($rects1 -eq $rects2)
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$r0 = $ws[0].Current.BoundingRectangle
"2.oturum (yeniden baslatma) not0 piksel (uzakta): " + (Px ([int]($r0.X + 150)) ([int]($r0.Y + 200)))
MoveM ($r0.X + 150) ($r0.Y + 150); Start-Sleep -Milliseconds 700
"2.oturum not0 piksel (fare ustunde): " + (Px ([int]($r0.X + 150)) ([int]($r0.Y + 200)))
MoveM 1270 790
# tray: Tüm notları opak yap
Start-App "--show-tray-menu"; Start-Sleep 2
$it = Find-Name "Tüm notları opak yap"; $c = Center $it; MClick $c[0] $c[1]; Start-Sleep 2
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$ws = @(Note-Windows); $r5 = $ws[5].Current.BoundingRectangle
"tray sonrasi gri not (eskiden %60) piksel: " + (Px ([int]($r5.X + 40)) ([int]($r5.Y + $r5.Height - 40))) + "  (221,221,221 = opak)"
Get-Process StickyNotes | Stop-Process -Force
Stop-Process -Id $dark.Id -Force -ErrorAction SilentlyContinue
