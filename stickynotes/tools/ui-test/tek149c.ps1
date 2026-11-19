# Senaryo 3: yeniden başlatınca daraltılmış durum, diyalog iptal/onay. (tek149b.ps1 sonrası aynı DATA_DIR ile)
Add-Type -AssemblyName System.Windows.Forms
$exe = $env:APP_EXE; $dd = $env:DATA_DIR
$ex = Get-AppWindows | ? { $_.Current.Name -like "*Explorer*" } | Select -First 1
function Dlg { $e = Get-AppWindows | ? { $_.Current.Name -like "*Explorer*" } | Select -First 1; if ($e -and (Find-Id $e "ConfirmYesButton")) { $e } }
if ($ex) {
  $d = Dlg; "dialog var: " + [bool]$d
  if (-not $d) { $li = (Find-Id $ex "NoteList"); Click-Id $ex "PurgeButton"; Start-Sleep 1; $d = Dlg; "dialog (yeniden): " + [bool]$d }
  Click-Id $d "ConfirmCancelButton"; Start-Sleep 1
  "iptal sonrasi: " + (Win-Rects)
}
# Çıkış: uygulamayı sonlandır (kayıt zaten debounce ile yazıldı), aynı data-dir ile yeniden aç
Get-Process StickyNotes | Stop-Process -Force; Start-Sleep 2
$p = Start-Process $exe -ArgumentList "--data-dir `"$dd`"" -PassThru; Start-Sleep 4
"yeniden aciliste: " + (Win-Rects)
Shot "15-yeniden-acilis-daraltilmis"
$w = Get-AppWindows | ? { $_.Current.Name -eq "Sticky Note" } | Select -First 1
Click-Id $w "CollapseButton"; Start-Sleep 1
"genisletildi: " + (Win-Rects)
Shot "16-genisletilmis"
# Çöpü boşalt onayı
[System.Windows.Forms.SendKeys]::SendWait("%x"); Start-Sleep 2
$ex = Get-AppWindows | ? { $_.Current.Name -like "*Explorer*" } | Select -First 1
$t = Find-Id $ex "TrashTab"; $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
Click-Id $ex "EmptyTrashButton"; Start-Sleep 1
$d = Dlg; "bosalt dialog: " + [bool]$d; Shot "17-onay-copu-bosalt"
Click-Id $d "ConfirmYesButton"; Start-Sleep 1
"sonra: " + (Win-Rects)
