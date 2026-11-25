# TEK-149 elle test senaryosu: tema, diyalog, daralt/genişlet. Parametre: $env:APP_EXE, $env:DATA_DIR, $env:SHOT_DIR
Add-Type -AssemblyName System.Windows.Forms
$exe = $env:APP_EXE; $dd = $env:DATA_DIR
Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
Remove-Item $dd -Recurse -Force -ErrorAction SilentlyContinue
$p = Start-Process $exe -ArgumentList "--data-dir `"$dd`"" -PassThru
Start-Sleep 4
$w = Get-AppWindows | Select -First 1
"windows: " + (Win-Rects)
# metin yaz
$body = Find-Id $w "TextEditor"
Set-Editor $body "Alışveriş listesi`r`nSüt`r`nEkmek`r`nYumurta"
Start-Sleep 1
Shot "01-not-metin"
# checklist
Click-Id $w "TypeButton"; Start-Sleep 1
Shot "02-checklist"
# renk menüsü
Click-Id $w "ColorButton"; Start-Sleep 1
Shot "03-renk-menusu"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1
"done"
