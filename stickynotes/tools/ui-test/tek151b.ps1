# TEK-151 senaryo B: boyutlandirma siniri, varsayilanlar, %20 opaklik (+ yeniden baslatma), kisayollar
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
$wa = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
function Vis($el) { $r = $el.Current.BoundingRectangle; [pscustomobject]@{ L=[int]($r.X+12); T=[int]($r.Y+12); R=[int]($r.X+$r.Width-12); B=[int]($r.Y+$r.Height-12) } }
function Dump2 { & "$env:SEEDEXE" --dump $env:DATA_DIR }
function Pick($combo, $name) {
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 600
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $it = $combo.FindFirst($TS::Descendants, $cond)
    if (-not $it) { $it = $AE::RootElement.FindFirst($TS::Descendants, $cond) }
    $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
}
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
$light = Start-Backdrop "#F1F0EC"; Start-Sleep 2
Start-App
$n = @(Note-Windows)[0]
# --- 1) boyutlandirma: sag kenar (golge payindan) ekran disina cekilir
$v = Vis $n; $mid = [int](($v.T + $v.B) / 2)
DragMouse ($v.R + 6) $mid ($wa.Right + 400) $mid 30 15
$v = Vis @(Note-Windows)[0]; "SAG KENAR boyutlandir (disari): R=$($v.R) (<= $($wa.Right)) W=$($v.R - $v.L)"
# --- sag-alt tutamac
$v = Vis @(Note-Windows)[0]
DragMouse ($v.R - 8) ($v.B - 8) ($wa.Right + 400) ($wa.Bottom + 400) 30 15
$v = Vis @(Note-Windows)[0]; "SAG-ALT TUTAMAC (disari): R=$($v.R) B=$($v.B) (<= $($wa.Right),$($wa.Bottom))  boyut $($v.R-$v.L)x$($v.B-$v.T)"
Crop "resize-kenar" ($wa.Right - 700) 0 700 $wa.Bottom
$v = Vis @(Note-Windows)[0]; DragMouse ($v.R - 8) ($v.B - 8) ($v.L + 260) ($v.T + 260) 30 15
$v = Vis @(Note-Windows)[0]; "KUCULTME: boyut $($v.R-$v.L)x$($v.B-$v.T)"
# --- 2) Ayarlar'dan varsayilanlar
Start-App "--show-tray-menu"; Start-Sleep 2
$mi = Find-Name "Ayarlar…"; $c = Center $mi; MClick $c[0] $c[1]; Start-Sleep 2
$st = Get-AppWindows | Where-Object { $_.Current.Name -like "*Ayarlar*" } | Select-Object -First 1
Pick (Find-Id $st "DefColorCombo") "Yeşil"
Pick (Find-Id $st "DefOpacityCombo") "%50"
Pick (Find-Id $st "DefTypeCombo") "Checklist"
$w = Find-Id $st "DefWidthBox"; $w.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("300")
$h = Find-Id $st "DefHeightBox"; $h.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("200")
$h.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait("{TAB}"); Start-Sleep 1
Pick (Find-Id $st "FontStyleCombo") "İtalik"
CropEl "ayarlar-varsayilan" $st 4 4
Click-Id $st "CloseSettingsButton"; Start-Sleep 1
# yeni not (Ctrl+N notta)
$n = @(Note-Windows)[0]; $te = Find-Id $n "TextEditor"; $c = Center $te; MClick $c[0] $c[1]; Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("^n"); Start-Sleep 2
$all = @(Note-Windows); "not sayisi: " + $all.Count
$new = $all | Sort-Object { $_.Current.BoundingRectangle.Width } -Descending | Select-Object -First 1
"YENI NOT boyut (gorunur): " + (Vis $new | ForEach-Object { "$($_.R-$_.L)x$($_.B-$_.T)" }) + " (beklenen 300x200)"
CropEl "yeni-not-varsayilan" $new 14 14
Dump2
# --- 3) %20 opaklik: ilk notta menu
$n = @(Note-Windows)[0]
Click-Id $n "ColorButton"; Start-Sleep 1
$om = Find-Name "OpacityMenu"; if (-not $om) { $om = Find-Id $AE::RootElement "OpacityMenu" }
"Opaklik alt menusu bulundu: " + [bool]$om
$c = Center $om; MClick $c[0] $c[1]; Start-Sleep 1
Crop "opaklik-alt-menu" 0 0 700 700
$i20 = $AE::RootElement.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, "%20")))
"%20 ogesi: " + [bool]$i20
$i20.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1
Stop-Process -Id $light.Id -Force; $dark = Start-Backdrop "#1E1F24"; Start-Sleep 2
MClick 1270 790; MoveM 1270 790; Start-Sleep 1
Crop "opaklik-20" 0 0 700 700
Start-Sleep 1; Dump2
Start-App
MoveM 1270 790; Start-Sleep 1
"RESTART sonrasi:"; Dump2
Crop "opaklik-20-restart" 0 0 700 700
Stop-Process -Id $dark.Id -Force
