Invoke-Expression ([IO.File]::ReadAllText("$env:TOOLS_DIR\lib150.ps1"))
Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
$hold = Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$env:TOOLS_DIR\holdkey.ps1`"" -PassThru -WindowStyle Hidden; Start-Sleep 4
Start-Process $env:APP_EXE -ArgumentList "--data-dir `"$env:DATA_DIR`"" | Out-Null
foreach ($i in 1..8) { Start-Sleep -Milliseconds 700; Crop "balon$i" 780 560 500 240 }
Stop-Process -Id $hold.Id -Force
Get-Process StickyNotes | Stop-Process -Force
