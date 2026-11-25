$exe = $env:APP_EXE; $dd = $env:DATA_DIR
Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
Start-Process $exe -ArgumentList "--data-dir `"$dd`""
Start-Sleep 5
"windows:"; Win-Rects
Shot "smoke"
