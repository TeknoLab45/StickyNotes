# TEK-152 / Görev B: biçim (kalın/altı çizili/üstü çizili), sağ tık menüsü, kalıcılık
# Plan §7 maddeleri 2, 3, 4, 5, 8 (kısmen). Ortam: APP_EXE, DATA_DIR, SHOT_DIR, TOOLS_DIR (lib150.ps1 ile yüklenir).
Add-Type -AssemblyName System.Windows.Forms
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
Start-App

# --- Senaryo 2: Ctrl+B / Ctrl+U aç-kapa ---
$n = @(Note-Windows)[0]
$te = Find-Id $n "TextEditor"
Set-Editor $te "Toplantı yarın saat 10"
# "Toplantı " = 9 karakter; "yarın" = 5 karakter
[System.Windows.Forms.SendKeys]::SendWait("^{HOME}")
1..9 | ForEach-Object { [System.Windows.Forms.SendKeys]::SendWait("{RIGHT}") }
1..5 | ForEach-Object { [System.Windows.Forms.SendKeys]::SendWait("+{RIGHT}") }
[System.Windows.Forms.SendKeys]::SendWait("^b"); Start-Sleep 1
Shot "tek152-01-kalin"
# ikinci Ctrl+B kalını kaldırmalı (ekran görüntüsünde doğrula)
[System.Windows.Forms.SendKeys]::SendWait("^b"); Start-Sleep 1
Shot "tek152-01b-kalin-kalkti"
[System.Windows.Forms.SendKeys]::SendWait("^u"); Start-Sleep 1
Shot "tek152-02-alti-cizili"

# --- Senaryo 3: sağ tık menüsü, yalnızca 6 madde + ayraç ---
$c = Center $te; MClick $c[0] $c[1] -Right; Start-Sleep 1
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
$items = $AE::RootElement.FindAll($TS::Descendants, $cond) | ForEach-Object { $_.Current.Name }
"menu maddeleri: " + ($items -join " | ")
Shot "tek152-03-sag-tik-menu"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1

# --- Senaryo 4: seçim yokken biçim maddeleri devre dışı ---
[System.Windows.Forms.SendKeys]::SendWait("^{END}"); Start-Sleep 1
$c = Center $te; MClick $c[0] $c[1] -Right; Start-Sleep 1
$bold = $AE::RootElement.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "MenuBold")))
"seçim yokken MenuBold etkin mi: " + $bold.Current.IsEnabled + " (beklenen: False)"
Shot "tek152-04-secimsiz-menu"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep 1

# --- Senaryo 5: kapat-aç, biçim kalıcı mı ---
Start-App
$n = @(Note-Windows)[0]
Shot "tek152-05-yeniden-acilis"
"dump:"; dotnet run --project (Join-Path $env:TOOLS_DIR "..\seed") -- --dump $env:DATA_DIR
