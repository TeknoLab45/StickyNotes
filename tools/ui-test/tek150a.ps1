# TEK-150 senaryo A: opaklık, şerit, gölge, checklist, kaydırma çubuğu, kenar boyutlandırma
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
$dark = Start-Backdrop "#1E1F24"
Start-Sleep 2
Start-App
$ws = @(Note-Windows)
"notlar: " + $ws.Count; $ws | ForEach-Object { Rect $_ }
$away = { MoveM 1270 790 }
# --- koyu zeminde %100/%80/%60 (fare uzakta, hiçbir not odakta değil) ---
MClick 1100 700; & $away; Start-Sleep 1
Crop "01-notlar-koyu-zemin" 0 0 1080 470
# piksel testi: not 5 (gri, %60) gövde pikseli: fare dışarıdayken ve üzerindeyken
$n5 = $ws[5]; $r5 = $n5.Current.BoundingRectangle
$px = [int]($r5.X + 40); $py = [int]($r5.Y + $r5.Height - 40)
$before = Px $px $py
MoveM ($r5.X + 120) ($r5.Y + 150); Start-Sleep -Milliseconds 600
$hover = Px $px $py
MoveM 1270 790; Start-Sleep -Milliseconds 600
$after = Px $px $py
"OPAKLIK piksel (gri not %60, koyu zemin): uzakta=$before | farenin altinda=$hover | ayrilinca=$after"
Crop "01b-fare-ustunde-100" ($r5.X - 4) ($r5.Y - 4) ($r5.Width + 8) ($r5.Height + 8)
MoveM 1270 790
# --- açık zemin ---
Stop-Process -Id $dark.Id -Force
$light = Start-Backdrop "#F1F0EC"; Start-Sleep 2
MClick 1100 700; & $away; Start-Sleep 1
Crop "02-notlar-acik-zemin" 0 0 1080 470
$ws = @(Note-Windows)
CropEl "03-golge-yakin" $ws[3] 6 6      # gölge payı dahil tek not
# --- şerit hover: sil ve + ---
$del = Find-Id $ws[0] "DeleteButton"; $c = Center $del; MoveM $c[0] $c[1]; Start-Sleep -Milliseconds 700
CropEl "04-serit-hover-sil" $ws[0] 4 4
$plus = Find-Id $ws[0] "NewNoteButton"; $c = Center $plus; MoveM $c[0] $c[1]; Start-Sleep -Milliseconds 700
CropEl "05-serit-hover-arti" $ws[0] 4 4
# --- renk + opaklık menüsü, %70 seç ---
$cb = Find-Id $ws[0] "ColorButton"; $c = Center $cb; MClick $c[0] $c[1]; Start-Sleep 1
CropEl "06-renk-opaklik-menusu" $ws[0] 6 6 0 330
$item = Find-Name "%70"
if ($item) { $c = Center $item; MClick $c[0] $c[1]; "opaklik %70 secildi" } else { "HATA: %70 menu ogesi bulunamadi" }
Start-Sleep 1.5
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$r0 = $ws[0].Current.BoundingRectangle
"not0 yellow %70 acik zeminde: " + (Px ([int]($r0.X + 150)) ([int]($r0.Y + 200)))
# --- checklist ve kaydırma çubuğu ---
CropEl "07-checklist" $ws[1] 6 6
$long = $ws[2]; $lr = $long.Current.BoundingRectangle
MoveM ($lr.X + $lr.Width - 30) ($lr.Y + 120); Start-Sleep -Milliseconds 700
CropEl "08-kaydirma-cubugu" $long 6 6
MoveM 1270 790
# --- boyutlandırma: kenardan (gölge payı) ve sağ-alt köşe tutamacından ---
function Find-At($r) { Note-Windows | Where-Object { [Math]::Abs($_.Current.BoundingRectangle.X - $r.X) -lt 2 -and [Math]::Abs($_.Current.BoundingRectangle.Y - $r.Y) -lt 2 } | Select-Object -First 1 }
$t = $ws[7]; $r = $t.Current.BoundingRectangle; $b0 = Rect $t
DragMouse ($r.Right - 6) ($r.Y + 110) ($r.Right - 6 - 40) ($r.Y + 110)
"SAG KENAR (golge payi) boyutlandirma: once=$b0 sonra=$(Rect (Find-At $r))"
$t = $ws[6]; $r = $t.Current.BoundingRectangle; $b0 = Rect $t
DragMouse ($r.X + 100) ($r.Bottom - 6) ($r.X + 100) ($r.Bottom - 6 + 30)
"ALT KENAR (golge payi) boyutlandirma: once=$b0 sonra=$(Rect (Find-At $r))"
$r = (Find-At $r).Current.BoundingRectangle; $b0 = Rect (Find-At $r)
DragMouse ($r.Right - 18) ($r.Bottom - 18) ($r.Right - 18 + 30) ($r.Bottom - 18 + 25)
"KOSE TUTAMACI boyutlandirma: once=$b0 sonra=$(Rect (Find-At $r))"
Stop-Process -Id $light.Id -Force -ErrorAction SilentlyContinue
Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
