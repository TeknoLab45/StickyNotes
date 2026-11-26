# TEK-467: kalın modu, F2 başlık, Markdown listeleri, düz metin yapıştırma, liste modunun kaldırılması.
# Ortam: APP_EXE, DATA_DIR, SHOT_DIR, TOOLS_DIR. Çalıştırma: ui.ps1 + lib150.ps1 + bu dosya (UTF-8) birlikte yüklenir.
Remove-Item $env:DATA_DIR -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $env:SHOT_DIR | Out-Null
Start-App

$script:fail = 0
function Note-Content {
    $n = @(Note-Windows)[0]
    Start-Sleep -Milliseconds 1200   # kayıt gecikmesi 500 ms
    $out = & dotnet run --no-build --project (Join-Path $env:TOOLS_DIR "..\seed") -- --content $env:DATA_DIR
    ($out -join "`n") | ConvertFrom-Json
}
function Check($name, $ok, $detail) {
    if ($ok) { "GECTI  $name" } else { $script:fail++; "KALDI  $name :: $detail" }
}
function Keys($k) { [System.Windows.Forms.SendKeys]::SendWait($k); Start-Sleep -Milliseconds 250 }
function Marks-Text($c) { @($c.marks | Where-Object { $_ } | ForEach-Object { "$($_.t):" + $c.text.Substring($_.s, $_.e - $_.s) }) -join "|" }
function Focus-Editor {
    $n = @(Note-Windows)[0]
    $te = Find-Id $n "TextEditor"
    $c = Center $te; MClick $c[0] $c[1]; Start-Sleep -Milliseconds 300
}
function Reset-Editor { Focus-Editor; Keys "^a"; Keys "{DEL}" }

$n = @(Note-Windows)[0]
Check "liste modu dugmesi yok" (-not (Find-Id $n "TypeButton")) "TypeButton var"

# 1) Secim yokken Ctrl+B: yazarken kalin ac/kapat
Reset-Editor
Keys "bir "; Keys "^b"; Keys "iki"; Keys "^b"; Keys " uc"
$c = Note-Content
Check "Ctrl+B yazarken kalin ac/kapat" ($c.text -eq "bir iki uc" -and (Marks-Text $c) -eq "b:iki") "$($c.text) [$(Marks-Text $c)]"
Shot "tek467-01-kalin-modu"
# secili metinde eski davranis
Keys "^{HOME}"; Keys "+{RIGHT}+{RIGHT}+{RIGHT}"; Keys "^b"
$c = Note-Content
Check "Ctrl+B secili metinde" ((Marks-Text $c) -eq "b:bir|b:iki") (Marks-Text $c)

# 3) Markdown listesi
Reset-Editor
Keys "- sut"; Keys "{ENTER}"; Keys "ekmek"; Keys "{ENTER}"; Keys "{TAB}"; Keys "kepekli"; Keys "{ENTER}"; Keys "+{TAB}"; Keys "yumurta"; Keys "{ENTER}"; Keys "{ENTER}"; Keys "son"
$c = Note-Content
$exp = "- sut`n- ekmek`n  - kepekli`n- yumurta`nson"
Check "liste devam/girinti/cikis" ($c.text -eq $exp) ($c.text -replace "`n", "\n")
Shot "tek467-02-liste"
Reset-Editor
Keys "1. bir"; Keys "{ENTER}"; Keys "iki"; Keys "{ENTER}"; Keys "{ENTER}"; Keys "- {[} {]} gorev"; Keys "{ENTER}"; Keys "yeni"
$c = Note-Content
$exp = "1. bir`n2. iki`n- [ ] gorev`n- [ ] yeni"
Check "numarali ve gorev listesi" ($c.text -eq $exp) ($c.text -replace "`n", "\n")
# kalin modu liste satirinda surer
Reset-Editor
Keys "- "; Keys "^b"; Keys "a"; Keys "{ENTER}"; Keys "b"
$c = Note-Content
Check "kalin modu yeni maddede surer" ($c.text -eq "- a`n- b" -and (Marks-Text $c) -match "b:a" -and (Marks-Text $c) -match "b$") "$($c.text -replace "`n", '\n') [$(Marks-Text $c)]"
Keys "^b"
# Tab liste disinda sekme yazar
Reset-Editor
Keys "{TAB}x"
$c = Note-Content
Check "liste disinda Tab sekme" ($c.text -eq "`tx") ($c.text -replace "`t", "\t")

# 4) Duz metin yapistirma
Reset-Editor
Set-Clipboard -Value "yapisan`r`n- satir"
Keys "^b"; Keys "K "; Keys "^+v"
$c = Note-Content
Check "Ctrl+Shift+V duz metin" ($c.text -eq "K yapisan`n- satir" -and (Marks-Text $c) -notmatch "yapisan") "$($c.text -replace "`n", '\n') [$(Marks-Text $c)]"
Reset-Editor
$te = Find-Id (@(Note-Windows)[0]) "TextEditor"
$p = Center $te; MClick $p[0] $p[1] -Right; Start-Sleep 1
$item = $AE::RootElement.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "MenuPastePlain")))
Check "sag tik menusunde duz metin yapistir" ([bool]$item) "madde yok"
Shot "tek467-03-sag-tik"
if ($item) { $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1 }
$c = Note-Content
Check "menuden duz metin yapistirma" ($c.text -eq "yapisan`n- satir") ($c.text -replace "`n", "\n")

# 2) F2 baslik
$n = @(Note-Windows)[0]
$dateTitle = (Find-Id $n "StripTitle").Current.Name
Check "basliksiz notta tarih" ($dateTitle -match "^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$") $dateTitle
Focus-Editor
Keys "{F2}"; Shot "tek467-04-baslik-duzenle"; Keys "Alisveris"; Keys "{ENTER}"
$c = Note-Content
$strip = (Find-Id (@(Note-Windows)[0]) "StripTitle").Current.Name
Check "F2 + Enter baslik kaydeder" ($c.title -eq "Alisveris" -and $strip -eq "Alisveris") "title=$($c.title) strip=$strip"
Keys "{F2}"; Keys "vazgec"; Keys "{ESC}"
$c = Note-Content
Check "Esc vazgecer" ($c.title -eq "Alisveris") "title=$($c.title)"
Check "baslik duzenleme govdeyi bozmaz" ($c.text -eq "yapisan`n- satir") ($c.text -replace "`n", "\n")
Shot "tek467-05-baslik"

# kalicilik
Start-App
$n = @(Note-Windows)[0]
$strip = (Find-Id $n "StripTitle").Current.Name
Check "yeniden acilista baslik" ($strip -eq "Alisveris") $strip
Shot "tek467-06-yeniden-acilis"
# bos baslik -> tarihe doner
Focus-Editor
Keys "{F2}"; Keys "{DEL}"; Keys "{ENTER}"
$c = Note-Content
Check "bos baslik tarihe doner" (-not $c.title) "title=$($c.title)"

Get-Process StickyNotes -ErrorAction SilentlyContinue | Stop-Process -Force
"KALAN HATA: $script:fail"
