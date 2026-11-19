Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Start-App
$n = @(Note-Windows)[0]
Click-Id $n "ColorButton"; Start-Sleep 1
$om = Find-Id $AE::RootElement "OpacityMenu"; $c = Center $om
MoveM $c[0] $c[1]; Start-Sleep -Milliseconds 300; MoveM ($c[0]+3) $c[1]; Start-Sleep 2
Shot "dbg3"
$all = $AE::RootElement.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)))
$all | % { $_.Current.Name + " | " + (Rect $_) }
Get-Process StickyNotes | Stop-Process -Force
