# TEK-151 senaryo C: %20 gorunum, tray menusu, global kisayollar (Notepad onde), kisayol cakismasi
Add-Type -MemberDefinition (@"
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
"@) -Name FG -Namespace Win32
function FgTitle { $sb = New-Object System.Text.StringBuilder 256; [Win32.FG]::GetWindowText([Win32.FG]::GetForegroundWindow(), $sb, 256) | Out-Null; $sb.ToString() }
Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
function Dump2 { & "$env:SEEDEXE" --dump $env:DATA_DIR | Where-Object { $_ -notlike "AYAR*" } }
function VisCount { @(Note-Windows | Where-Object { -not $_.Current.IsOffscreen }).Count }
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
$dark = Start-Backdrop "#1E1F24"; Start-Sleep 2
Start-App
$n = @(Note-Windows)[0]; Click-Id $n "ColorButton"; Start-Sleep 1
$om = Find-Id $AE::RootElement "OpacityMenu"; $c = Center $om; MClick $c[0] $c[1]; Start-Sleep 1
$i = $AE::RootElement.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, "%20"))); $i.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1
Start-App
MClick 1100 700; MoveM 1270 790; Start-Sleep 1
Crop "opaklik-20-kayitli" 0 0 700 700
Start-App "--show-tray-menu"; Start-Sleep 2
Crop "tray-menu" 960 300 320 460
Get-AppWindows | Where-Object { $_.Current.Name -like "*Ayarlar*" } | Out-Null
Start-App
# --- Notepad onde
$np = Start-Process (Join-Path $env:SystemRoot "System32\notepad.exe") -PassThru; Start-Sleep 3
"baslangic gorunur not: " + (VisCount) + "; on plandaki pencere: " + (FgTitle)
[Windows.Forms.SendKeys]::SendWait("%h"); Start-Sleep 2
"Alt+H sonrasi gorunur not: " + (VisCount)
Dump2
[Windows.Forms.SendKeys]::SendWait("%s"); Start-Sleep 2
"Alt+S sonrasi gorunur not: " + (VisCount)
Dump2
$before = @(Note-Windows).Count
[Windows.Forms.SendKeys]::SendWait("%n"); Start-Sleep 2
"Alt+N: not sayisi $before -> " + @(Note-Windows).Count
[Windows.Forms.SendKeys]::SendWait("%x"); Start-Sleep 2
"Alt+X: Explorer acik mi: " + [bool](Get-AppWindows | Where-Object { $_.Current.Name -like "*Explorer*" })
Shot "kisayol-explorer"
Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue
# --- cakisma: baska program Alt+H'yi tutuyor
Get-Process StickyNotes | Stop-Process -Force; Start-Sleep 1
$hold = Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$env:TOOLS_DIR\holdkey.ps1`"" -PassThru -WindowStyle Hidden; Start-Sleep 4
$logPath = Join-Path $env:DATA_DIR "logs\app.log"; $mark = (Get-Content $logPath).Count
Start-App; Start-Sleep 1
Shot "kisayol-uyari-balon"
Crop "balon" 880 560 400 240
"uygulama calisiyor mu (cakisma): " + [bool](Get-Process StickyNotes -ErrorAction SilentlyContinue)
"notlar acik: " + (VisCount)
"log (cakisma):"; Get-Content $logPath | Select-Object -Skip $mark | Where-Object { $_ -match "Kısayol|ERROR|Başlıyor" }
$np = Start-Process (Join-Path $env:SystemRoot "System32\notepad.exe") -PassThru; Start-Sleep 3
[Windows.Forms.SendKeys]::SendWait("%h"); Start-Sleep 2
"Alt+H (baska programda): gorunur not = " + (VisCount) + " (gizleme OLMAMALI; kisayol baskasinda)"
[Windows.Forms.SendKeys]::SendWait("%n"); Start-Sleep 2
"Alt+N (cakismayan) hala calisiyor: not sayisi " + @(Note-Windows).Count
Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $hold.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $dark.Id -Force -ErrorAction SilentlyContinue
