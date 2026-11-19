# TEK-150 senaryo B: Explorer (önizleme, rozet, sıralama, renk filtresi, Ctrl+F), onay diyaloğu, tray menüsü
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
$light = Start-Backdrop "#F1F0EC"; Start-Sleep 2
Start-App
$ws = @(Note-Windows)
# Ctrl+F not penceresinden: Explorer açılır, arama kutusu odakta mı?
$c = Center (Find-Id $ws[0] "TextEditor"); MClick $c[0] $c[1]; Start-Sleep -Milliseconds 500
[System.Windows.Forms.SendKeys]::SendWait("^f"); Start-Sleep 2
$ex = Get-AppWindows | Where-Object { $_.Current.Name -like "*Explorer*" } | Select-Object -First 1
"Explorer acildi mi (Ctrl+F nottan): " + [bool]$ex
"odaktaki oge: id=" + $AE::FocusedElement.Current.AutomationId
"baslik: " + $ex.Current.Name
CropEl "10-explorer-notlar" $ex 14 14
Click-Id $ex "SortButton"; Start-Sleep 1
CropEl "11-explorer-siralama-menusu" $ex 14 14 0 40
$it = Find-Name "Oluşturma"; $c = Center $it; MClick $c[0] $c[1]; Start-Sleep 1
CropEl "12-explorer-olusturma-sirasi" $ex 14 14
$dot = Find-Id $ex "FilterDot_green"; $c = Center $dot; MClick $c[0] $c[1]; Start-Sleep 1
CropEl "13-explorer-renk-filtresi" $ex 14 14
$c = Center $dot; MClick $c[0] $c[1]; Start-Sleep 1
$li = $ex.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
$c = Center $li; MClick $c[0] $c[1]; Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("^f"); Start-Sleep -Milliseconds 600
"Ctrl+F (Explorer icinde) odak: " + $AE::FocusedElement.Current.AutomationId
[System.Windows.Forms.SendKeys]::SendWait("kitap"); Start-Sleep 1
CropEl "14-explorer-arama-ctrl-f" $ex 14 14
[System.Windows.Forms.SendKeys]::SendWait("^a{DEL}"); Start-Sleep -Milliseconds 500
$t = Find-Id $ex "TrashTab"; $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
CropEl "15-explorer-cop" $ex 14 14
$li = $ex.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
$li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
Click-Id $ex "PurgeButton"; Start-Sleep 2
$er = $ex.Current.BoundingRectangle
Crop "16-onay-dialog" ($er.X + $er.Width/2 - 250) ($er.Y + $er.Height/2 - 130) 500 270
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1
Click-Id $ex "CloseExplorerButton"; Start-Sleep 1
Start-App "--show-tray-menu"
Start-Sleep 2
Crop "17-tray-menu" 980 400 300 360
"tray ogesi bulundu mu: " + [bool](Find-Name "Tüm notları opak yap")
Stop-Process -Id $light.Id -Force -ErrorAction SilentlyContinue
