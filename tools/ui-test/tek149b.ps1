# Senaryo 2: daralt/genişlet, Explorer, onay diyaloğu, yeniden başlatma
Add-Type -AssemblyName System.Windows.Forms
$exe = $env:APP_EXE; $dd = $env:DATA_DIR
Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
Remove-Item $dd -Recurse -Force -ErrorAction SilentlyContinue
$p = Start-Process $exe -ArgumentList "--data-dir `"$dd`"" -PassThru
Start-Sleep 4
$w = Get-AppWindows | Select -First 1
$w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern) | Out-Null
Set-Editor (Find-Id $w "TextEditor") "Toplantı notları`r`nGündem maddeleri"
Start-Sleep 1
Shot "10-renk-menusu-font"
Click-Id $w "ColorButton"; Start-Sleep 1; Shot "10b-renk-menu"; [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1
"before: " + (Win-Rects)
# daralt (buton)
Click-Id $w "CollapseButton"; Start-Sleep 1
"collapsed: " + (Win-Rects); Shot "11-daraltilmis"
# genişlet (çift tık şeritte)
$r = $w.Current.BoundingRectangle
MClick ([int]($r.X + $r.Width/2)) ([int]($r.Y + 14)) -Double; Start-Sleep 1
"expanded by dblclick: " + (Win-Rects)
# tekrar daralt çift tıkla
MClick ([int]($r.X + $r.Width/2)) ([int]($r.Y + 14)) -Double; Start-Sleep 1
"collapsed by dblclick: " + (Win-Rects)
Start-Sleep 2
# yeni, ikinci not ekle (Ctrl+N), onu çöpe at
[System.Windows.Forms.SendKeys]::SendWait("^n"); Start-Sleep 2
"after ctrl+n: " + (Win-Rects)
$ws = Get-AppWindows
$n2 = $ws | ? { $_.Current.BoundingRectangle.Height -gt 100 } | Select -First 1
Set-Editor (Find-Id $n2 "TextEditor") "Silinecek not"
Start-Sleep 1
Click-Id $n2 "DeleteButton"; Start-Sleep 1
"sqlite wait"; Start-Sleep 2
# Explorer
[System.Windows.Forms.SendKeys]::SendWait("%x"); Start-Sleep 2
"windows: " + (Win-Rects)
Shot "12-explorer-notlar"
$ex = Get-AppWindows | ? { $_.Current.Name -like "*Explorer*" } | Select -First 1
$ex.SetFocus()
$t = Find-Id $ex "TrashTab"; $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
Shot "13-explorer-cop"
$li = (Find-Id $ex "NoteList").FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
$li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
Click-Id $ex "PurgeButton"; Start-Sleep 1
Shot "14-onay-dialog-kalici-sil"
"windows: " + (Win-Rects)
