# TEK-151 senaryo A: kenara yapışma, yeniden başlatma, Ayarlar (yazı boyutu), varsayılanlar, %20 opaklık
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
$wa = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
"calisma alani: $($wa.Left),$($wa.Top) $($wa.Width)x$($wa.Height)"
function Vis($el) { $r = $el.Current.BoundingRectangle; [pscustomobject]@{ L=[int]($r.X+12); T=[int]($r.Y+12); R=[int]($r.X+$r.Width-12); B=[int]($r.Y+$r.Height-12) } }
function Dump { & dotnet run --project "$env:TOOLS_DIR\..\seed" -c Release --no-launch-profile -- --dump $env:DATA_DIR 2>$null }
function Dump2 { & "$env:SEEDEXE" --dump $env:DATA_DIR }
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
$light = Start-Backdrop "#F1F0EC"; Start-Sleep 2
Start-App
$ws = @(Note-Windows); "baslangic not sayisi: " + $ws.Count
$n = $ws[0]; "ilk konum: " + (Vis $n | Out-String).Trim()
# 1) sürükle: sağ-alt köşeye, kenardan ~8 px önce bırak
$r = $n.Current.BoundingRectangle
$sx = $r.X + 150; $sy = $r.Y + 27
$tx = $wa.Right - 8 - 260 + 150 - 0; $ty = $wa.Bottom - 8 - 260 + 27
# görünür sağ = (tx-150+12+... ) -> hedef: görünür sağ kenar wa.Right-8
$dx = ($wa.Right - 8) - ($r.X + $r.Width - 12); $dy = ($wa.Bottom - 8) - ($r.Y + $r.Height - 12)
DragMouse $sx $sy ($sx + $dx) ($sy + $dy)
$n = @(Note-Windows)[0]; $v = Vis $n
"SAG-ALT (8px kala birakildi): gorunur R=$($v.R) (calisma alani sagi=$($wa.Right)) B=$($v.B) (calisma alani alti=$($wa.Bottom))"
Crop "snap-sag-alt" ($wa.Right - 340) ($wa.Bottom - 340) 340 340
# 2) ekran dışına sürükle (sol-üst, çok dışarı)
$r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy ($sx - 1500) ($sy - 900) 30 15
$n = @(Note-Windows)[0]; $v = Vis $n
"DISARI (sol-ust): gorunur L=$($v.L) T=$($v.T) (beklenen $($wa.Left),$($wa.Top))"
# 3) sol kenardan 9 px, üstten 200
$r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy ($sx + 9) ($sy + 200) 20 15
$n = @(Note-Windows)[0]; $v = Vis $n
"SOL (9px): gorunur L=$($v.L) T=$($v.T)  (beklenen L=0)"
# 3b) sınırdan uzak bir yere (yapışmamalı)
$r = $n.Current.BoundingRectangle; $sx = $r.X + 150; $sy = $r.Y + 27
DragMouse $sx $sy ($sx + 400) ($sy + 50) 20 15
$n = @(Note-Windows)[0]; $v = Vis $n; "ORTA (yapismamali): L=$($v.L) T=$($v.T) R=$($v.R) B=$($v.B)"
# 4) boyutlandır: sağ-alt tutamak ile ekran dışına doğru
$g = Find-Id $n "ResizeGrip"; $c = Center $g
DragMouse $c[0] $c[1] ($wa.Right + 300) ($wa.Bottom + 300) 30 15
$n = @(Note-Windows)[0]; $v = Vis $n
"BOYUTLANDIR (disari cekildi): R=$($v.R) (<= $($wa.Right)) B=$($v.B) (<= $($wa.Bottom))"
# Not 4: sağ kenara yapışmış konuma getir (görünür sağ= 1280-8, sürükleyip yapıştır)
$r = $n.Current.BoundingRectangle; $sx = $r.X + 100; $sy = $r.Y + 27
DragMouse $sx $sy ($sx + 600) ($sy + 0) 30 15
$n = @(Note-Windows)[0]; $before = Vis $n; "RESTART ONCESI: L=$($before.L) T=$($before.T) R=$($before.R) B=$($before.B)"
Start-App
$n = @(Note-Windows)[0]; $after = Vis $n; "RESTART SONRASI: L=$($after.L) T=$($after.T) R=$($after.R) B=$($after.B)"
"KAYMA yok mu: " + (($before.L -eq $after.L) -and ($before.T -eq $after.T) -and ($before.R -eq $after.R) -and ($before.B -eq $after.B))
# 5) Ayarlar penceresi (tray menüsünden)
Start-App "--show-tray-menu"; Start-Sleep 2
Crop "tray-menu" 980 380 300 380
$mi = Find-Name "Ayarlar…"; "Ayarlar... ogesi: " + [bool]$mi
$c = Center $mi; MClick $c[0] $c[1]; Start-Sleep 2
$st = Get-AppWindows | Where-Object { $_.Current.Name -like "*Ayarlar*" } | Select-Object -First 1
"Ayarlar penceresi: " + [bool]$st
CropEl "ayarlar" $st 4 4
$sl = Find-Id $st "FontSizeSlider"
$rv = $sl.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern); $rv.SetValue(18); Start-Sleep 1
"yazi boyutu slider: " + $rv.Current.Value
"notlar acik: " + @(Note-Windows).Count
$te = Find-Id (@(Note-Windows)[0]) "TextEditor"
Set-Editor $te "Merhaba yazi boyutu 18"; Start-Sleep 1
Crop "yazi-18" 0 0 700 700
Dump2
