Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Start-App
[System.Windows.Forms.SendKeys]::SendWait("%x"); Start-Sleep 2
$ex = Get-AppWindows | Where-Object { $_.Current.Name -like "*Explorer*" } | Select-Object -First 1
$t = Find-Id $ex "TrashTab"; $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
$li = $ex.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
$li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep 1
Click-Id $ex "PurgeButton"; Start-Sleep 3
Win-Rects
Shot "dlgfull"
