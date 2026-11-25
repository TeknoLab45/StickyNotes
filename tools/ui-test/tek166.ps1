# TEK-166 / G3: Memos bağlantısı, Space seçimi, eşitleme, salt okunur not, tray "Şimdi eşitle".
# Ortam: APP_EXE, DATA_DIR, SHOT_DIR, TOOLS_DIR (lib150.ps1 ile yüklenir).
# MEMOS_TEST_URL (örn. http://127.0.0.1:5230), MEMOS_TEST_TOKEN (memos_pat_…), MEMOS_TEST_SPACE (spaces/<id>),
# MEMOS_TEST_TOKEN2 (ikinci kullanıcı; salt okunur senaryosu için, isteğe bağlı).
# Token hiçbir çıktıya yazılmaz. Sunucuda Docker Memos gerekir (docs/SENKRON.md).
Add-Type -AssemblyName System.Windows.Forms
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
Start-App

$url = $env:MEMOS_TEST_URL.TrimEnd('/')
$headers = @{ Authorization = "Bearer $($env:MEMOS_TEST_TOKEN)" }

# Sunucu kökü: 26.x /api, 0.31 /api/v1 (MemosClient ile aynı sıra)
$root = $null
foreach ($r in @("/api", "/api/v1")) {
    try { Invoke-RestMethod -Uri "$url$r/instance/profile" -Headers $headers -TimeoutSec 10 | Out-Null; $root = $r; break } catch {}
}
"api kökü: $root"

function Set-Text($el, $text) {
    $el.Focus() | Out-Null
    [System.Windows.Forms.SendKeys]::SendWait("^a"); [System.Windows.Forms.SendKeys]::SendWait($text)
}
function Memos-Status { (Find-Id $script:settings "MemosStatus").Current.Name }

# --- Senaryo 1: Ayarlar → Memos bölümü, adres + token + bağlantı testi ---
# Ayarlar penceresini tray menüsünden ("Ayarlar…") açın; betik 60 sn boyunca pencereyi bekler.
$n = @(Note-Windows)[0]
$te = Find-Id $n "TextEditor"
Set-Editor $te "Araç plakaları"        # yerel not (eşlenmemiş → yüklenince sunucuya gider)
$settings = $null
for ($i = 0; $i -lt 60 -and -not $settings; $i++) {
    Start-Sleep 1
    $settings = Get-AppWindows | Where-Object { $_.Current.Name -like "*Ayarlar*" } | Select-Object -First 1
}
if (-not $settings) { throw "Ayarlar penceresi açılmadı (tray → Ayarlar…)" }

$urlBox = Find-Id $settings "MemosUrlBox"
Set-Text $urlBox $url
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}"); Start-Sleep 1
$tokenBox = Find-Id $settings "MemosTokenBox"
Set-Text $tokenBox $env:MEMOS_TEST_TOKEN
Click-Id $settings "MemosTokenSaveButton"; Start-Sleep 1
Click-Id $settings "MemosTestButton"; Start-Sleep 4
"bağlantı sonucu: " + (Memos-Status)
Shot "tek166-01-baglanti"

# --- Senaryo 2: Space seçimi ve eşitlemeyi açma (yalnızca MEMOS_TEST_SPACE) ---
$spaceCombo = Find-Id $settings "MemosSpaceCombo"
$spaceCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() 2>$null
Start-Sleep 1
Shot "tek166-02-space-listesi"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Click-Id $settings "MemosEnabledCheck"; Start-Sleep 6   # ilk tur: açılış + Şimdi eşitle
"eşitleme sonrası durum: " + (Find-Id $settings "MemosStatus").Current.Name
Shot "tek166-03-ilk-esitleme"

# --- Senaryo 3: not → web (sunucuda memo oluştu mu) ---
if ($root) {
    $list = Invoke-RestMethod -Uri "$url$root/memos?pageSize=50" -Headers $headers -TimeoutSec 15
    $found = @($list.memos | Where-Object { $_.content -like "*Araç plakaları*" }).Count
    "web tarafında 'Araç plakaları' memo sayısı: $found (beklenen: >=1)"
}

# --- Senaryo 4: web → not (sunucuda yeni memo yerelde görünür; salt okunur kilit yok, çünkü kendi notumuz) ---
if ($root) {
    $body = @{ content = "Yedek parça listesi"; visibility = "PRIVATE"; space = $env:MEMOS_TEST_SPACE } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$url$root/memos?memoId=tek166webnote1" -Headers $headers -Body $body -ContentType "application/json" | Out-Null
    Click-Id $settings "MemosSyncNowButton"; Start-Sleep 5
    "web → not: 'Yedek parça' yerelde var mı, Explorer'dan kontrol edin"
    Shot "tek166-04-web-to-not"
}

# --- Senaryo 5: salt okunur (ikinci kullanıcının notu) ---
# Not: ikinci kullanıcı (MEMOS_TEST_TOKEN2) ile sunucuda not oluşturulmalı; uygulama o notu kilitli açmalı.
if ($root -and $env:MEMOS_TEST_TOKEN2) {
    $h2 = @{ Authorization = "Bearer $($env:MEMOS_TEST_TOKEN2)" }
    $body = @{ content = "Ortak not (salt okunur beklenir)"; visibility = "SPACE"; space = $env:MEMOS_TEST_SPACE } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$url$root/memos?memoId=tek166shared1" -Headers $h2 -Body $body -ContentType "application/json" | Out-Null
    Click-Id $settings "MemosSyncNowButton"; Start-Sleep 5
    $ro = Get-AppWindows | Where-Object { $_.Current.Name -eq "Sticky Note" } |
          ForEach-Object { Find-Id $_ "ReadOnlyLock" } | Where-Object { $_ -and $_.Current.IsOffscreen -eq $false }
    "salt okunur kilit görünen pencere sayısı: " + @($ro).Count + " (beklenen: >=1)"
    Shot "tek166-05-salt-okunur"
}

# --- Senaryo 6: çevrimdışı (sunucu kapalıyken not ekle, sonra eşitle) → yalnızca elle ---
"çevrimdışı senaryosu: sunucuyu durdurup bir not ekleyin, tray'de 'Çevrimdışı · N değişiklik bekliyor' yazmalı; sunucuyu açıp 'Şimdi eşitle'."
Shot "tek166-06-cevrimdisi-bekleme"

"tamamlandı: ekran görüntüleri $env:SHOT_DIR"
