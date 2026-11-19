# TEK-150 ortak yardımcılar (ui.ps1'den sonra yüklenir). Ortam: APP_EXE, DATA_DIR, SHOT_DIR, TOOLS_DIR
function Crop($name, $x, $y, $w, $h) {
    $b = [Windows.Forms.SystemInformation]::VirtualScreen
    $x = [Math]::Max($b.X, [int]$x); $y = [Math]::Max($b.Y, [int]$y)
    $w = [Math]::Min([int]$w, $b.Right - $x); $h = [Math]::Min([int]$h, $b.Bottom - $y)
    $bmp = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object Drawing.Size $w, $h))
    $bmp.Save((Join-Path $env:SHOT_DIR "$name.png")); $g.Dispose(); $bmp.Dispose()
}
function CropEl($name, $el, $padX = 0, $padY = 0, $extraW = 0, $extraH = 0) {
    $r = $el.Current.BoundingRectangle
    Crop $name ($r.X - $padX) ($r.Y - $padY) ($r.Width + 2 * $padX + $extraW) ($r.Height + 2 * $padY + $extraH)
}
function Px($x, $y) {
    $bmp = New-Object Drawing.Bitmap 1, 1
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object Drawing.Size 1, 1))
    $c = $bmp.GetPixel(0, 0); $g.Dispose(); $bmp.Dispose(); "{0},{1},{2}" -f $c.R, $c.G, $c.B
}
function Start-Backdrop($color) {
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$env:TOOLS_DIR\backdrop.ps1`" `"$color`"" -PassThru -WindowStyle Hidden
}
function Start-App($extra = "") {
    Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 1
    Start-Process $env:APP_EXE -ArgumentList "--data-dir `"$env:DATA_DIR`" $extra" | Out-Null
    Start-Sleep 5
}
function Note-Windows { Get-AppWindows | Where-Object { $_.Current.Name -eq "Sticky Note" } | Sort-Object { $_.Current.BoundingRectangle.Y }, { $_.Current.BoundingRectangle.X } }
function Find-Name($name) {
    foreach ($w in Get-AppWindows) {
        $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
        $e = $w.FindFirst($TS::Descendants, $c)
        if ($e) { return $e }
    }
    $null
}
function Center($el) { $r = $el.Current.BoundingRectangle; @([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)) }
function MoveM($x, $y) { [Win32.Mouse]::SetCursorPos([int]$x, [int]$y) | Out-Null }
function Rect($el) { $r = $el.Current.BoundingRectangle; "{0:0},{1:0} {2:0}x{3:0}" -f $r.X, $r.Y, $r.Width, $r.Height }
Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern void mouse_event(int f,int dx,int dy,int d,int e);' -Name Mouse2 -Namespace Win32
function DragMouse($x1, $y1, $x2, $y2, $steps = 20, $delay = 15) {
    MoveM $x1 $y1; Start-Sleep -Milliseconds 200
    [Win32.Mouse2]::mouse_event(0x2, 0, 0, 0, 0)
    for ($i = 1; $i -le $steps; $i++) { MoveM ($x1 + ($x2 - $x1) * $i / $steps) ($y1 + ($y2 - $y1) * $i / $steps); Start-Sleep -Milliseconds $delay }
    Start-Sleep -Milliseconds 200
    [Win32.Mouse2]::mouse_event(0x4, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 400
}
