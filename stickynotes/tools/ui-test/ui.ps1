Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function Get-AppWindows {
    $p = Get-Process StickyNotes -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { return @() }
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $p.Id)
    $AE::RootElement.FindAll($TS::Children, $cond) | ForEach-Object { $_ }
}
function Find-Id($root, $id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $root.FindFirst($TS::Descendants, $c)
}
function Click-Id($root, $id) {
    $e = Find-Id $root $id
    if (-not $e) { throw "yok: $id" }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Shot($name) {
    $b = [Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [Drawing.Point]::Empty, $b.Size)
    $dir = if ($env:SHOT_DIR) { $env:SHOT_DIR } else { [IO.Path]::GetTempPath() }
    $bmp.Save((Join-Path $dir "$name.png"))
    $g.Dispose(); $bmp.Dispose()
}
# RichTextBox ValuePattern sunmaz: ortasına tıkla, tümünü seç, klavyeden yaz (satırlar {ENTER})
function Set-Editor($el, $text) {
    $r = $el.Current.BoundingRectangle
    MClick ([int]($r.X + $r.Width / 2)) ([int]($r.Y + $r.Height / 2)); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("^a{DEL}")
    $lines = $text -split "\r?\n" | ForEach-Object { $_ -replace '([+^%~(){}\[\]])', '{$1}' }
    [System.Windows.Forms.SendKeys]::SendWait(($lines -join "{ENTER}"))
}
function Win-Rects { Get-AppWindows | ForEach-Object { "{0} | {1} | {2}" -f $_.Current.Name, $_.Current.BoundingRectangle, $_.Current.NativeWindowHandle } }
Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(int f,int dx,int dy,int d,int e);' -Name Mouse -Namespace Win32
function MClick($x,$y,[switch]$Right,[switch]$Double){
  [Win32.Mouse]::SetCursorPos($x,$y); Start-Sleep -Milliseconds 150
  $dn = if($Right){0x8}else{0x2}; $up = if($Right){0x10}else{0x4}
  $n = if($Double){2}else{1}
  1..$n | ForEach-Object { [Win32.Mouse]::mouse_event($dn,0,0,0,0); [Win32.Mouse]::mouse_event($up,0,0,0,0); Start-Sleep -Milliseconds 80 }
}
