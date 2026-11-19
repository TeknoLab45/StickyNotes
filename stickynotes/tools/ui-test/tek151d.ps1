# TEK-151 senaryo D: yazi boyutu 14 -> 18 anlik uygulanma ve kalicilik
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
function Dump2 { & "$env:SEEDEXE" --dump $env:DATA_DIR | Where-Object { $_ -like "AYAR*" } }
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
$light = Start-Backdrop "#F1F0EC"; Start-Sleep 2
Start-App
$n = @(Note-Windows)[0]; $te = Find-Id $n "TextEditor"
Set-Editor $te "Alisveris listesi`nSut, yumurta, ekmek`nToplanti 15:00"; Start-Sleep 1
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$r = $n.Current.BoundingRectangle
Crop "yazi-14-once" ($r.X - 6) ($r.Y - 6) ($r.Width + 12) ($r.Height + 12)
$h14 = (Find-Id @(Note-Windows)[0] "TextEditor").Current.BoundingRectangle
Start-App "--show-tray-menu"; Start-Sleep 2
$mi = Find-Name "Ayarlar…"; $c = Center $mi; MClick $c[0] $c[1]; Start-Sleep 2
$st = Get-AppWindows | Where-Object { $_.Current.Name -like "*Ayarlar*" } | Select-Object -First 1
$sl = Find-Id $st "FontSizeSlider"; $sl.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(18); Start-Sleep 1
CropEl "ayarlar" $st 4 4
Click-Id $st "CloseSettingsButton"; Start-Sleep 1
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$n = @(Note-Windows)[0]; $r = $n.Current.BoundingRectangle
Crop "yazi-18-sonra" ($r.X - 6) ($r.Y - 6) ($r.Width + 12) ($r.Height + 12)
Dump2
Start-App; MClick 1100 700; MoveM 1270 790; Start-Sleep 1
$n = @(Note-Windows)[0]; $r = $n.Current.BoundingRectangle
Crop "yazi-18-restart" ($r.X - 6) ($r.Y - 6) ($r.Width + 12) ($r.Height + 12)
"restart sonrasi:"; Dump2
Stop-Process -Id $light.Id -Force
