# Sticky Notes — istemci (Windows)

## Kullanım
- Başlatınca tray'de ikon belirir (gizli simgeler okunun altında olabilir). İlk açılışta boş bir not açılır.
- **Tray**: çift tık = yeni not. Sağ tık menüsü (temalı): Yeni not, Tüm notları göster, Tüm notları gizle, Explorer, Ayarlar…, Tüm notları opak yap, Çöp kutusu, Çıkış.
- **Not penceresi**: çerçevesiz, saydam pencere + yumuşak gölge, her zaman üstte; üst şeritten sürüklenir. Boyutlandırma: görünür kenarın hemen dışındaki 12 px gölge payından ya da sağ-alt tutamaçtan.
- **Biçim (metin notları)**: seçili yazıya **Ctrl+B** kalın, **Ctrl+U** altı çizili; **üstü çizili** yalnızca sağ tık menüsünde (kısayolu yok). Sağ tık menüsü: Kalın, Altı çizili, Üstü çizili, Kes, Kopyala, Yapıştır. Seçim yoksa biçim maddeleri devre dışı. Biçim not içinde saklanır (`docs/BICIMLENDIRME.md`).
  Kısıtlar: yalnızca seçili yazıya biçim (seçimsiz yazmaya devam yok); yapıştırma her zaman düz metin; checklist'e dönüşümde biçim düşer; italik/renk/boyut/liste/hizalama yok (Ctrl+I, Ctrl+E, Ctrl+], Ctrl+Shift+L bir şey yapmaz). Biçimi bilmeyen eski sürümle düzenlenen not biçimini kaybeder (metin korunur); tüm PC'ler aynı sürüme güncellenmeli.
  Şerit (soluk başlık = ilk satır): renk+opaklık menüsü · metin↔checklist · **+ yeni not** · daralt/genişlet · çöpe at · gizle.
- **Opaklık**: renk düğmesinin menüsünde "Opaklık ▸" alt menüsü: %100'den %20'ye 10'ar adım; her notun ayarı ayrı ve kalıcıdır (`notes.opacity`). Fare notun üzerindeyken ya da not odaktayken %100, ayrılınca seçili değere döner. Tray: "Tüm notları opak yap". Ctrl+F: Explorer arama kutusuna gider.
- **Daralt/genişlet**: şeride çift tık ya da şeritteki ok düğmesi. Daraltılınca yalnızca başlık satırı (notun ilk satırı) kalır; tekrar tıklayınca eski boyuta döner. Durum SQLite'a (`window_state.collapsed`, `expanded_height`) yazılır, yeniden açılışta korunur.
- **Tema**: tüm pencereler `src/StickyNotes.App/Themes/Theme.xaml` içindeki ortak stilleri kullanır (tek tema; seçici yok). Kalıcı sil / Çöpü boşalt onayları temalı özel diyalogdur (Enter/Esc = Vazgeç).
- **Checklist**: Enter = yeni madde (imleçten sonrasını taşır), boş maddede Backspace = maddeyi siler, ↑/↓ maddeler arası gezinir, kutucuk = işaretle (üstü çizilir).
- **Explorer** (Alt+X): Notlar / Çöp sekmeleri, arama kutusu (düz metinde, büyük/küçük harf duyarsız; Ctrl+F), sekme rozetlerinde sayılar ("Notlar 6 · Çöp 3"), sıralama (son düzenlenen / oluşturma), renk filtresi noktaları. Satır = renk, ilk satır, soluk önizleme (checklist: "2/5 tamam"), göreli tarih ("bugün 18:05").
  Çift tık notu açar. Çöpte: Geri al, Kalıcı sil, Çöpü boşalt (onay sorar).
- **Kısayollar** — global (başka program öndeyken de çalışır; Ayarlar'dan değiştirilir/kapatılır): `Alt+H` tüm notları gizle (kalıcı, `is_open=0`), `Alt+S` tümünü göster (tek tek gizlenmişler dahil), `Alt+X` Explorer, `Alt+N` yeni not.
  Başka program aynı kısayolu tutuyorsa uygulama çökmez; `app.log`'a yazar ve tray'de tek seferlik uyarı gösterir. Not penceresi odaktayken ayrıca Ctrl+N yeni not, Ctrl+F Explorer araması.
- **Kenara yapışma**: sürükleme ve boyutlandırmada görünür not kenarı çalışma alanı kenarına ≤ 12 px (DPI ile ölçeklenir) yaklaşınca oturur; not çalışma alanı dışına çıkamaz. Açılışta konum geri yüklenirken de uygulanır. (Windows'un kendi Aero Snap'i bu pencerelerde kapalıdır.)
- **Ayarlar** (tray → Ayarlar…, değişiklikler anında uygulanır, bu PC'ye özeldir — SQLite `settings` tablosu, senkronizasyona girmez):
  yazı tipi / boyutu (10–24) / stili (normal-kalın-italik) → TÜM notlara anında; renk, opaklık, boyut (varsayılan 260×260), tür (metin/checklist) → yalnızca YENİ notlar için varsayılan; 4 global kısayol.
  Not menüsünde (renk düğmesi) "Bu notu varsayılan yap" o notun renk/opaklık/boyut/türünü varsayılan yapar.
- Pencere konumu/boyutu/açık-gizli durumu ve içerik kaydedilir; yeniden açılışta geri gelir.

## Memos eşitleme (TEK-168)
Ayarlar → **Memos eşitleme** (bu PC'ye özel; `settings` tablosundaki `memos_*` anahtarları):
1. **Sunucu adresi**: örn. `http://192.168.1.10:5230`.
2. **Token al**: tarayıcıda Memos'un `/setting#access-token` sayfasını açar; orada token oluşturup kopyalayın (`memos_pat_…`), sonra **Token** kutusuna yapıştırıp **Kaydet**'e basın. Token DPAPI ile şifrelenir (bu Windows hesabına bağlı); düz metin olarak saklanmaz, günlüğe yazılmaz.
3. **Bağlantıyı test et**: sunucu kökünü algılar (Memos 26.x `/api`, 0.31 `/api/v1`), kimliği doğrular, üyesi olunan Space'leri listeler. Memos **0.31 veya üstü** gerekir.
4. **Alan (Space)** seçin. Space seçilmeden "Eşitleme açık" işaretlenmez. **Görünürlük**: varsayılan "Alan üyeleri (SPACE)", isteğe bağlı "Özel (PRIVATE)". **Eşitleme aralığı**: 1–60 dk (varsayılan 2).
- Eşitleme tetikleyicileri: uygulama açılışı, aralık, **Şimdi eşitle** (ayarlarda ve tray'de), kayıttan 5 sn sonra. "Yine de eşitle" yalnızca eşitleme duraklatıldıysa görünür.
- Duraklatma: token geçersizse (401), Space bulunamazsa, ya da sunucuda çok sayıda memo "kayıp" görünürse eşitleme durur; tray'de tek seferlik uyarı çıkar. Düzeltince "Yine de eşitle" ile devam edin.

**Eşitlenenler ve kurallar**
- Seçilen Space'teki tüm memolar (yazarı kim olursa olsun) yerel nota gelir. Yerelde yazdığınız notlar Space'e gönderilir.
- Başka kullanıcının notu **salt okunur** açılır: metin, checklist, biçim, yapıştırma ve tür değişikliği kapalıdır; renk, opaklık, konum, daralt ve çöpe atma (yalnızca bu PC'den gizler) açıktır. Şeritte kilit simgesi görünür.
- İki tarafta da değişen not çakışırsa yerel sürüm korunur, sunucu sürümü "(çakışma kopyası)" adıyla yeni not olur.
- Kendi notunuzu çöpe atarsanız sunucuda **arşivlenir** (silinmez); sunucuda arşivlenen not yerelde çöpe gider. Başkasının salt okunur notunu çöpe atmak yalnızca bu PC'de gizler.
- Çevrimdışıyken değişiklikler yerelde kalır; tray "Çevrimdışı · N değişiklik bekliyor" yazar, bağlantı gelince tek turda gönderilir.
- **Space değiştirilirse** eski Space'teki notlar yerelde kalır ama artık eşitlenmez (onay sorulur).
- **Uyarı:** bir Space Memos'ta silinirse içindeki memolar sunucuda kalıcı olarak silinir. Eşitleme bu durumu algılayıp duraklatır, yerel notları silmez.

Düzenlediğiniz (odakta ya da kaydedilmemiş yazısı olan) nota gelen uzak değişiklik, not bırakılana kadar ertelenir; bir sonraki turda uygulanır. Explorer'da salt okunur notlar kilit simgesiyle, eski Space'e bağlı notlar "bağlantısız" etiketiyle görünür. Uzaktan yeni gelen notlar için pencere açılmaz (Explorer'dan açılır). Ayrıntı ve Docker doğrulama bulguları: `docs/SENKRON.md`.

## Veri ve log
- Varsayılan: `%LOCALAPPDATA%\StickyNotes\notes.db`, log: `%LOCALAPPDATA%\StickyNotes\logs\app.log`.
- `StickyNotes.exe --data-dir <yol>` ile değiştirilir (db ve log o klasörde).
- Tek örnek: aynı data-dir için ikinci başlatma sessizce çıkar.

## Derleme, test, publish (kaynak kök dizininde)
```
dotnet build
dotnet test
dotnet publish src/StickyNotes.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-x64
dotnet publish src/StickyNotes.App -c Release -r win-arm64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-arm64
# küçük sürüm (hedefte .NET 10 Desktop Runtime gerekir):
dotnet publish src/StickyNotes.App -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o dist/fdd-x64
```
Linux'ta yalnızca derleme kontrolü (WPF çalışmaz): `dotnet build src/StickyNotes.App -p:EnableWindowsTargeting=true`.
Gereksinim: .NET SDK 10 (net10.0-windows). İkonu yeniden üretmek için `tools/make-icon.ps1`.

## Test verisi ve elle UI testi
`dotnet run --project tools/seed -- <data-dir> [adet] [opaque]` gerçekçi test notları yazar (`--dump <data-dir>` kayıtlı opaklıkları listeler). `StickyNotes.exe --show-tray-menu` tray menüsünü ekranda açar. Memos: `tools/ui-test/tek166.ps1` (Docker Memos ve `MEMOS_TEST_URL`/`MEMOS_TEST_TOKEN`/`MEMOS_TEST_SPACE` gerekir; Ayarlar'ı tray'den açın). Senaryolar: `tools/ui-test/tek150a/b/d/e.ps1`, `tek151a/b/c/d.ps1`, biçim için `tek152.ps1` (+`lib150.ps1`, `backdrop.ps1`, `holdkey.ps1` = başka programın kısayolu tutmasını taklit eder). RichTextBox'a yazmak için `Set-Editor` (ui.ps1) kullanılır. `--dump` tür, pencere durumu ve ayarları da yazar.

`tools/ui-test/ui.ps1` UI Automation + SendKeys yardımcılarını içerir (ekran görüntüleri `SHOT_DIR` ortam değişkenindeki klasöre, yoksa temp klasörüne yazılır) (`run.sh <senaryo.ps1>` ile yüklenir).
