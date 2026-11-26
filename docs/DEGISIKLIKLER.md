# Değişiklikler (en yeni üstte)

## 2026-11-26 — TEK-460 — Lead: kaynak kod incelemesi ve kurulum planı

- Kaynak kod incelendi: KRİTİK bulgu yok. Build 0 hata, test 145 geçti / 1 atlandı. Kurulum için tek önemli bulgu var: mutex adı kullanıcıya göre değişiyor.
- `docs/PLAN_KURULUM_INNO.md` eklendi: Inno Setup ile kullanıcı başına kurulum, sürüm 0.3.0, `CHANGELOG.md`, setup exe'yi Release'e tek asset olarak yükleyen CI, merge ve `v0.3.0` tag'i. Kod değişmedi.

## 2026-11-25 — TEK-444 — Coder: uygulama repo köküne alındı

- `stickynotes/` içeriği (`src/`, `tests/`, `tools/`, `StickyNotes.slnx`, `StickyNotes.exe`) repo köküne taşındı; uygulama dokümanları `docs/` altında birleşti (`CLIENT.md`, `BICIMLENDIRME.md`, `SENKRON.md`). `stickynotes/README.md` kök `README.md`'ye katıldı.
- İki değişiklik günlüğü tek dosyada (`docs/DEGISIKLIKLER.md`, adda Türkçe karakter yok) birleştirildi; eski deneme reposuna ait kayıtlar (TEK-161, TEK-163 kök kayıtları) ve eski `.gitignore` satırları (node_modules, __pycache__, .venv) silindi.
- Release iş akışı artık kökte çalışır (`working-directory` kaldırıldı). Kod değişmedi; exe yeniden derlenmedi.
- Aşağıdaki eski kayıtlarda geçen `stickynotes/...` yolları taşıma öncesi konumdur.

## 2026-11-25 — TEK-439 — Coder: tag ile GitHub Actions release

- `.github/workflows/release.yml` eklendi: `v*` tag'inde test + win-x64 tek dosya exe publish + Release asset'i. Kullanım: `stickynotes/docs/CLIENT.md` "Sürüm çıkarma". Mevcut `stickynotes/StickyNotes.exe` değişmedi.

## 2026-11-25 — TEK-438 — Coder: belge güncellemesi

- Kök `README.md` StickyNotes reposu olarak yeniden yazıldı (eski `lab-denemeler` deneme kuralları kaldırıldı; geçmiş notu kaldı).
- `CLAUDE.md` eklendi (yalnızca repo bilgisi). `docs/README.md` indeksi eklendi.
- `stickynotes/README.md` durumu güncellendi (145 test geçti/1 atlandı, exe eski sürüm, repo adı). Kod, klasör yapısı, exe değişmedi.

## 2026-11-25 — TEK-432 — CoderAgent: Memos'tan silme yalnızca kalıcı silmede

**Ne / neden:** Programda çöpe atılan not Memos'ta arşivleniyor, web listesinden kayboluyordu. Artık çöpe atma sunucuya hiçbir şey göndermez; memo yalnızca "Kalıcı sil" / "Çöpü boşalt" ile sunucudan silinir (`DELETE`), başarısızsa yeniden denenir. Kurallar: `docs/CLIENT.md`, ayrıntı: `docs/SENKRON.md`.

**Dosyalar (`stickynotes/`):**
- `src/StickyNotes.Core/Sync/SyncEngine.cs`, `Sync/MemosClient.cs` (`DeleteMemoAsync`), `NoteRepository.cs` (`sync_state.delete_pending`).
- `src/StickyNotes.App/ExplorerWindow.xaml.cs` (onay metinleri), `SyncService.cs` (çevrimdışı bekleyen sayısı silmeleri de sayar).
- `tests/StickyNotes.Core.Tests/`: `SyncEngineTests.cs`, `SyncScenarioTests.cs`, `SyncFakes.cs`, `MemosClientTests.cs`, `MemosLiveTests.cs`.

**Doğrulama (Linux, .NET SDK 10):**
- `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true` → 0 uyarı, 0 hata.
- `dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true` → 145 geçti, 1 atlandı (`MemosLiveTests`, env yoksa).
- Geçici Docker Memos 0.31.0 ve 26.11.1: `MemosLiveTests` (çöp → NORMAL, kalıcı silme → DELETE) geçti.
- **Yapılmadı:** WPF onay pencereleri Windows'ta görsel olarak denenmedi; exe derlenmedi.
- **main'e merge (TEK-435, CoderAgent, 2026-11-25):** dal `agent/coderagent/e838a2b686df` kullanıcı izniyle `main`'e `--no-ff` merge edildi; merge sonrası build 0 hata, test 145 geçti / 1 atlandı. Exe derlenmedi.

## 2026-11-19 — TEK-170 — CoderAgent: Ayarlar küçük ekran düzeltmesi, main'e merge

**Ne:** Ayarlar penceresi 800 px yüksekliğindeki ekranda artık çalışma alanını aşmıyor: `ScrollViewer` üzerindeki sabit `MaxHeight="720"` kaldırıldı, pencerenin `MaxHeight`'i `SystemParameters.WorkArea.Height` yapıldı, içerik kaydırılıyor (Memos bölümünün alt kısmı, Space seçimi ve durum satırı erişilebilir). Token kaydedilince "Kaydet" düğmesi "Kaydedildi ✓" olur; yeni bir token yazılınca "Kaydet"e döner.

**Dosyalar (`stickynotes/`):**
- `src/StickyNotes.App/SettingsWindow.xaml`: `ScrollViewer` `MaxHeight` kaldırıldı; `MemosTokenBox` `PasswordChanged="MemosToken_Changed"`; `MemosTokenSaveButton` `x:Name` eklendi.
- `src/StickyNotes.App/SettingsWindow.xaml.cs`: kurucuda `MaxHeight = SystemParameters.WorkArea.Height`; `MemosTokenSave_Click` düğme etiketini günceller; `MemosToken_Changed` yeni.
- `docs/DEĞİŞİKLİKLER.md`: bu madde.

**Doğrulama (bu makine: Linux, `/home/dietpi/.dotnet/dotnet`, 10.0.403):**
- `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true` → 0 uyarı, 0 hata.
- `dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true` → 126 geçti, 1 atlandı (`MemosLiveTests`, Docker Memos yok).
- **Yapılmadı:** WPF penceresi Windows'ta açılıp 800 px ekranda görsel doğrulanmadı; yeni exe derlenmedi.

**Kısıtlar / açık:**
- Bu düzeltme TEK-169'daki `StickyNotes.exe`'ye dahil değil. Düzeltmeli exe için WinVMAgent'ın yeniden publish etmesi gerekir.

## 2026-11-19 — TEK-168 — CoderAgent: Memos arayüzü, salt okunur not, tray, token (G3 kodu, sürüm 0.2.0)

**Ne:** StickyNotes'a Memos bağlantısı eklendi: Ayarlar'da Memos bölümü (adres, token, bağlantı testi, Space seçimi, görünürlük, aralık, "Şimdi eşitle"/"Yine de eşitle"), token'ın DPAPI ile şifrelenmesi, eşitlemeyi çalıştıran zamanlayıcı ve kayıttan 5 sn sonra tetikleme, başka kullanıcıların notlarında salt okunur mod (kilit simgesi), tray'de "Memos" durum satırı ve "Şimdi eşitle", tek seferlik tray uyarıları. Sürüm 0.1.0 → 0.2.0. Exe derlemesi/yayını bu issue'da yok (WinVMAgent işi).

**Dosyalar (`stickynotes/`):**
- `src/StickyNotes.App/TokenStore.cs` (yeni): DPAPI (CurrentUser) şifreleme; düz token hiçbir yere yazılmaz.
- `src/StickyNotes.App/SyncService.cs` (yeni): zamanlayıcı, 5 sn debounce, tur kilidi, tur sırasında gelen tetiklere tek tekrar, algılanan API kökü ve duraklatma durumunun kalıcı kaydı, tray durum metni, tek seferlik uyarılar.
- `src/StickyNotes.App/SettingsWindow.xaml(.cs)`: "Memos eşitleme" bölümü; Space değişiminde onay (eski Space'teki not sayısı); pencere artık `ScrollViewer` içinde (`MaxHeight` 720).
- `src/StickyNotes.App/NoteViewModel.cs`: `IsReadOnly`, `ReadOnlyAuthor`, `SetAccess`, `ReloadFrom`; salt okunurda `ToggleType` yapılmaz.
- Düzeltme (GERİ, ReviewerAgent): `SyncEngine` odakta/kaydedilmemiş notlara uzak değişikliği uygulamaz (`isBusy`, `Deferred` sayacı; 2 yeni Core testi); `onRemoteApplied` ile açık pencere uzak içeriği anında alır. `resume` bayrağı kuyrukta korunur. Beklenmeyen hata sonrası durum metni tutarlı. Explorer'da kilit ve "bağlantısız" işareti eklendi (`ExplorerWindow.xaml(.cs)`). `CLIENT.md` çöp kuralı düzeltildi.
- `src/StickyNotes.App/NoteWindow.xaml(.cs)`: salt okunurda editör `IsReadOnly`, checklist kutucukları ve metinleri kilitli, biçim kısayolları kapalı; şeritte kilit simgesi; `ReloadFromNote`.
- `src/StickyNotes.App/NoteManager.cs`: `FlushAll`, `RefreshAfterSync` (uzakta arşivlenen notun penceresi kapanır, odakta olmayan pencere uzak içeriği alır), açılışta salt okunur durumu uygulama.
- `src/StickyNotes.App/App.xaml.cs`: `SyncService` kurulumu, tray menüsüne "Memos: …" satırı ve "Şimdi eşitle", çıkışta zamanlayıcı durdurma.
- `src/StickyNotes.App/StickyNotes.App.csproj`: `<Version>0.2.0</Version>`.
- `tools/ui-test/tek166.ps1` (yeni): Windows UI senaryoları (bağlantı, Space, ilk eşitleme, web↔not, salt okunur, çevrimdışı beklemesi).
- `docs/CLIENT.md`, `docs/SENKRON.md`, `README.md`, `docs/DEĞİŞİKLİKLER.md`.

**Doğrulama (bu makine: Linux, aarch64):**
- `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true` → 0 hata, 0 uyarı (App dahil; WPF çalıştırılmadı).
- `dotnet test tests/StickyNotes.Core.Tests` → 124 geçti, 1 atlandı (`MemosLiveTests`, Docker Memos yok). Core'a dokunulmadı.
- **Yapılmadı:** Windows'ta çalıştırma, `tek166.ps1` koşusu, ekran görüntüleri, Docker Memos (0.31 / 26.11.1) ile uçtan uca test, win-x64 publish, `StickyNotes.exe` güncellemesi.

**Kısıtlar / açık:**
- Düzenlenen not (odakta ya da kaydedilmemiş yazı) o turda güncellenmez; uzak değişiklik bir sonraki turda uygulanır. Tur sırasında uygulanan uzak içerik açık pencereye aynı anda yazılır.
- Sunucuda yeni gelen notlar için pencere açılmaz; Explorer'dan açılır.
- Sync kapalıyken de salt okunur işaret kalır (not, eşlendiği için yerelde düzenlenmez).

## 2026-11-19 — TEK-167 — CoderAgent: Memos eşitlemesi Core (G1 + G2)

**Ne:** Memos (0.31 ve 26.x) ile Space tabanlı iki yönlü eşitlemenin WPF'siz Core katmanı. Markdown dönüşümü, Memos istemcisi (kök algılama, PAT, hata türleri), eşleme tablosu `sync_state`, eşitleme turu (`SyncEngine`) ve ayarlar (`SyncSettings`). Arayüz, token saklama ve exe bu issue'da yok (G3–G4).

**Dosyalar (`stickynotes/`):**
- `src/StickyNotes.Core/Sync/MemoMarkdown.cs` (yeni), `MemosClient.cs` (yeni), `SyncEngine.cs` (yeni), `SyncSettings.cs` (yeni)
- `src/StickyNotes.Core/NoteRepository.cs`: `sync_state` tablosu, `LoadSyncSettings`/`SaveSyncSettings`, `ListSyncState`, `MarkSynced`, `InsertRemote`, `ApplyRemote`, `ListForSync`
- `tests/StickyNotes.Core.Tests/`: `MemoMarkdownTests.cs`, `MemosClientTests.cs`, `SyncEngineTests.cs`, `SyncFakes.cs`, `MemosLiveTests.cs` (yeni)
- `docs/SENKRON.md` (yeni), `docs/README.md` (indeks)

**Doğrulama:**
- `dotnet test tests/StickyNotes.Core.Tests` → 124 geçti, 1 atlandı (80 mevcut + 45 yeni). Canlı test (`MemosLiveTests`) ortam değişkenleri yoksa Skipped görünür.
- Canlı test, Docker'daki iki sunucuda geçti (GERİ turu sonrası yeniden çalıştırıldı): `neosmemo/memos:0.31.0` (port 5231) ve `neosmemo/memos:26.11.1` (port 5232). Bulgular `docs/SENKRON.md` içinde.

**Kısıtlar / açık:** Token ayarları sayfasının web yolu doğrulanmadı (G3). Space değiştirme, iki PC aynı UUID, uzun çevrimdışı ve DB yeniden kurulumu otomatik test edilmedi. Ayrıntı `docs/SENKRON.md`.

## 2026-11-18 — TEK-155 — CoderAgent: RichTextBox, Ctrl+B/Ctrl+U, sağ tık menüsü, düz metin yapıştırma (Görev B)

**Ne:** Metin notları `TextBox`'tan `RichTextBox`'a geçti. Seçili yazıya Ctrl+B kalın, Ctrl+U altı çizili, üstü çizili yalnızca sağ tık menüsünde. Sağ tık menüsü kısa (Kalın, Altı çizili, Üstü çizili, ayraç, Kes, Kopyala, Yapıştır). Yapıştırma yalnızca düz metin. Biçim `marks` olarak saklanır (TEK-154 Core). Eski düz metin notlar biçimsiz açılır; yalnızca açmak `edited_at`'i değiştirmez.

**Dosyalar (`src/StickyNotes`):**
- `src/StickyNotes.App/RichTextMapper.cs` (yeni): FlowDocument ↔ (metin, biçim aralıkları), ofset eşleme.
- `src/StickyNotes.App/NoteViewModel.cs`: `Marks`, `SetRich`, `BuildContent` marks'ı yazar, checklist dönüşümünde biçim düşer.
- `src/StickyNotes.App/NoteWindow.xaml`, `NoteWindow.xaml.cs`: RichTextBox, bağlam menüsü, komut bağları, yapıştırma, `ToggleMark`.
- `src/StickyNotes.App/Themes/Theme.xaml`: RichTextBox stili; MenuItem'a kısayol sütunu ve devre dışı görünümü.
- `tools/ui-test/ui.ps1` (`Set-Editor` eklendi), `tek149.ps1`, `tek149b.ps1`, `tek151a.ps1`, `tek151d.ps1` (ValuePattern yerine Set-Editor), `tek152.ps1` (yeni).
- `docs/BICIMLENDIRME.md` (WPF bölümü), `docs/CLIENT.md` (Not penceresi, derleme, test), `docs/README.md`.

**Doğrulama:**
- `dotnet test tests/StickyNotes.Core.Tests` → 80/80 geçti (Linux).
- `dotnet build src/StickyNotes.App -p:EnableWindowsTargeting=true` → 0 hata, 0 uyarı (yalnızca derleme; WPF çalışmadı).
- Windows'ta çalıştırılmadı: plan §7 elle testleri, `tek152.ps1`, ekran görüntüleri, win-x64 exe yayını.

**Kısıtlar / açık:** ofset eşlemesi, Ctrl+Z, yapıştırma `DataObject` ataması ve `IsDocumentEnabled="False"` davranışı Windows'ta doğrulanmalı. Seçimsiz biçim yok. Eski `TextBox` sürümüyle düzenlenen not biçimini kaybeder. Kaynak zip'te git geçmişi yok; bu değişiklik yerel repo içinde `agent/tek-155` dalında commit'lendi, push edilmedi.

## 2026-11-18 — TEK-154 — CoderAgent: biçim Core modeli + içerik JSON + birim testler (Görev A)

**Ne:** Metin notlarında kalın / altı çizili / üstü çizili biçim için WPF'ten bağımsız Core katmanı: aralık modeli, normalizasyon, aç/kapa, içerik JSON'una isteğe bağlı `marks` alanı. Kullanıcı arayüzü (RichTextBox, Ctrl+B/Ctrl+U, sağ tık menüsü) bu issue'da yok (Görev B).

**Dosyalar (`src/StickyNotes`):**
- `src/StickyNotes.Core/RichText.cs` (yeni): `MarkTypes`, `Mark`, `RichText` (`Normalize`, `Toggle`, `IsFullyMarked`, `TypesAt`, `NormalizeNewlines`).
- `src/StickyNotes.Core/NoteContent.cs`: `CreateText(text, marks = null)`, `ParseMarks(content)`; sınıf yorumuna `marks` biçimi.
- `tests/StickyNotes.Core.Tests/RichTextTests.cs` (yeni, 32 test).
- `docs/BICIMLENDIRME.md` (yeni): kayıt biçimi, geriye uyum, sync kuralı, Core API.
- `docs/README.md`: indeks satırı.

**Doğrulama:** `dotnet build tests/StickyNotes.Core.Tests` → 0 hata, 0 uyarı. `dotnet test tests/StickyNotes.Core.Tests` → 80 geçti (önceki 48 + 32 yeni).

**Geriye uyum:** `marks` boşsa `CreateText` çıktısı eski biçimle bayt bayt aynı (`{"v":1,"text":"..."}`). Eski içerikte `ParseMarks` boş liste döner; bozuk `marks` exception üretmez.

**NU1903:** Core csproj zaten `SQLitePCLRaw.lib.e_sqlite3 2.1.14` kullanıyor; açık not kapandı.

**Kısıtlar:** App projesine dokunulmadı (Linux'ta derlenmez). Biçim ofsetleri `\n` ile normalize edilmiş metne göre hesaplanır; `\r\n` dönüşümü WPF görevinde yapılacak. Checklist'e dönüşümde biçim düşer (beklenen).

## 2026-11-18 — TEK-151 — WinVMAgent: kenara yapışma + varsayılan ayarlar + global kısayollar + %20 opaklık

**Ne:** Notlar ekran çalışma alanının kenarına yapışır ve dışına çıkamaz; Ayarlar penceresi (ortak yazı ayarları + yeni notlar için varsayılanlar + kısayollar);
global kısayollar Alt+H/Alt+S/Alt+X/Alt+N; opaklık %100→%20 (10'ar adım, "Opaklık ▸" alt menüsü). Sunucu/sync/push yok. Kod yazma istisnası yalnızca bu issue için (Müdür).

**Core (`src/StickyNotes.Core`)**
- `AppSettings.cs`: `AppSettings` (yazı: `FontFamily`/`FontSize` 10–24/`FontStyle` normal-bold-italic; yeni not varsayılanları: renk, opaklık, 260×260 boyut, tür; 4 kısayol metni — boş = kapalı),
  `NoteFontStyle`, `WindowSnap.Snap(RectD, work, threshold, SnapEdges, minW, minH)` (saf hesap: `SnapEdges.Move` = sürükleme, tek tek kenarlar = boyutlandırma), `RectD`.
- `NoteRepository`: yeni `settings(key TEXT PRIMARY KEY, value TEXT)` tablosu (`CREATE TABLE IF NOT EXISTS` → eski DB'ler açılışta yükselir), `LoadSettings()` / `SaveSettings()`.
  Ayarlar bu PC'ye özeldir, `notes` ile ilgisi yok → senkronizasyona girmez, `edited_at` değişmez.
- `NoteOpacity.Min` 0.3 → **0.2**, `Levels` = 1.0 … 0.2 (9 seviye).
- Yeni testler (`tests/.../SettingsAndSnapTests.cs`, 18 adet): settings kaydet-oku (yeniden açılışta da), varsayılanlar, aralığa çekme, `settings` tablosu olmayan eski DB, `Min = 0.2`, `Snap()` için 12 durum.

**App (`src/StickyNotes.App`)**
- `NativeSnap.cs`: `WM_MOVING`/`WM_SIZING` yakalanır; görünür dikdörtgen = pencere − 12 px gölge payı (×DPI ölçeği); monitör `MonitorFromRect(NEAREST)` + `rcWork`; eşik 12 px × DPI.
  Ek olarak sürükleme döngüsünde `WM_WINDOWPOSCHANGING` ve `WM_EXITSIZEMOVE` (`FixUp`): işletim sistemi sürüklerken pencerenin üstünü çalışma alanına sıkıştırdığı için (gölge payı yüzünden görünür üst kenar 12 px aşağıda kalıyordu) son söz bizde.
  `WS_MAXIMIZEBOX` kaldırıldı → Windows'un kendi Aero Snap'i (kenara sürükleyince yarım/çeyrek ekran) devre dışı.
  `NoteWindow.Restore()` aynı yardımcıyı (`SnapDip`) kullanır: açılışta çalışma alanı içine alınır/yapışır (yeniden başlatınca kayma yok).
- `SettingsWindow` (tray → "Ayarlar…"): değişiklikler ANINDA uygulanır ve SQLite'a yazılır (Kaydet düğmesi yok, "Kapat" var). Yazı ayarları (`NoteManager.UpdateSettings` → `NoteWindow.ApplySettings`) açık TÜM notlara anında yansır.
  Kısayol kutusuna tıklayıp birleşime basılır; Backspace/Delete = kapat. Ayarlar penceresi aktifken global kısayollar askıya alınır (aynı birleşim kutuya yakalanabilsin diye).
- Not menüsü (renk düğmesi): renkler · **Opaklık (%x) ▸** alt menü (%100…%20; `Theme.xaml` MenuItem şablonuna alt menü Popup'ı + ok eklendi) · **Bu notu varsayılan yap** (renk, opaklık, tür, boyut → yeni not varsayılanı).
- `HotkeyManager`: `RegisterHotKey` (mesaj-penceresi). Başka program aynı kısayolu tutuyorsa çökmez; `app.log`'a `Kısayol kaydedilemedi: …` yazar ve tray balonu gösterir (oturum başına aynı kısayol için tek seferlik).
  Alt+H = `NoteManager.HideAll` (her not `is_open=0`, tray'deki "gizle" ile aynı), Alt+S = `ShowAll` (tek tek gizlenmişler dahil tüm aktif notları açar), Alt+X Explorer, Alt+N yeni not.
- Tray: "Tüm notları gizle" ve "Ayarlar…" eklendi.
- `tools/seed --dump <data-dir>` artık tür, pencere durumu ve ayarları da yazar.

**Test**
- `dotnet build` (0 uyarı), `dotnet test` (48 geçti). Gerçek exe senaryoları: `tools/ui-test/tek151a.ps1` (yapışma, yeniden başlatma, Ayarlar, yazı boyutu), `tek151b.ps1` (boyutlandırma sınırı, varsayılanlar, %20), `tek151c.ps1` (kısayollar Notepad önde, çakışma; `holdkey.ps1` başka programı taklit eder), `tek151d.ps1` (yazı 14→18).
  Çalıştırma: `APP_EXE`, `DATA_DIR`, `SHOT_DIR`, `TOOLS_DIR` (+ `SEEDEXE`=publish edilmiş `tools/seed`) ortam değişkenlerini verip `tools/ui-test/run.sh tek151a.ps1`.

**Bilinen kısıtlar / açık kalanlar**
- Çoklu monitör ve farklı DPI VM'de denenemedi (tek 1280×800, %100). Kod `MonitorFromRect` + DPI ölçeği kullanır, `Snap()` ikinci monitör ofsetiyle birim testlidir.
- Yapışma yalnızca çalışma alanı (görev çubuğu hariç) kenarlarına; notlar arası yapışma yok.
- Kısayol çakışması balonu bu VM'de ekran görüntüsüyle doğrulanamadı (balon yakalanamadı); log satırı ve uygulamanın çökmemesi doğrulandı.
- arm64 exe derlendi, bu makine x64 olduğu için çalıştırılamadı.
- Kısayol metni `Alt+H` biçimi; en az bir değiştirici zorunlu.

## 2026-11-17 — TEK-150 — WinVMAgent: UI cilası 2 + not başına opaklık

**Ne:** Not başına opaklık (%100/90/80/70/60), not penceresi cilası (gölge, soluk başlık, "+" düğmesi, büyük şerit ikonları, kaydırma çubuğu),
Explorer (önizleme, "2/5 tamam", sekme rozetleri, sıralama, renk filtresi, göreli tarih, Ctrl+F), gölgeli onay diyaloğu, temalı tray menüsü, NU1903 kapatıldı.
Sunucu/sync/git/push yok. Kod yazma istisnası yalnızca bu issue için (Müdür).

**Opaklık**
- `notes.opacity REAL NOT NULL DEFAULT 1.0`; eski DB'ler `PRAGMA table_info(notes)` + `ALTER TABLE` ile açılışta yükseltilir (`collapsed` ile aynı yol).
  `NoteRepository.SetOpacity/SetAllOpacity`; `edited_at` DEĞİŞMEZ (görünüm tercihi; sync'e girerse karar verilmeli).
- Not penceresi `AllowsTransparency=True`; `Window.Opacity` = fare üstünde ya da pencere aktifken 1.0, aksi halde notun değeri (`NoteWindow.UpdateWindowOpacity`).
- Menü: şeritteki renk düğmesinin menüsünün altında "Opaklık" bölümü. Tray: "Tüm notları opak yap" (`NoteManager.MakeAllOpaque`).
- **Yöntem: tam pencere opaklığı** (yazı da saydamlaşır). Yedek (yalnızca arka plan alfası) GEREKMEDİ: 12 notta sürükleme/yazma opak ve saydam notlarda aynı akıcılıkta ölçüldü.

**Not penceresi**
- Pencere = görünür not + her kenarda 12 px gölge payı (`NoteWindow.Pad`). Gölge `Effect` değil, üst üste düşük alfalı kenarlıklar (SoftwareOnly'de ucuz).
- Kaydedilen/geri yüklenen konum ve boyut GÖRÜNÜR dikdörtgendir (pay hariç) → açılışta kayma yok (testle doğrulandı: konumlar yeniden başlatmada aynı).
- `AllowsTransparency` ile `WindowChrome` çalışmadığı için kenar/köşe boyutlandırma kaldırıldı ve yerine gölge payındaki 8 görünmez bölge + sağ-alt tutamaç
  `WM_SYSCOMMAND/SC_SIZE` ile boyutlandırır: **kenardan boyutlandırma ÇALIŞIYOR** (pay içinden, görünür kenarın 12 px dışından tutulur); kenarın içinden tutulmaz.
- Şerit 30 px, ikonlar 15 px/koyu, hover'da yuvarlak arka plan, her zaman görünen soluk başlık (ilk satır), "+" = yeni not. Yazı 13.5, iç boşluk 12/8.
- Kaydırma çubuğu: ince, yuvarlak, hafif iz çizgisi (`Theme.xaml` örtük `ScrollBar` stili).
- Min genişlik 220 görünür (şerit düğmeleri sığsın); varsayılan yeni not 284x260.

**Explorer / diyalog / tray**
- `NoteContent.Preview` (metin: ilk satırdan sonrası; checklist: "d/t tamam"), `Note.CreatedAt` (UUIDv7 kimliğinden — şema değişmedi), `DateText.Relative` ("bugün/dün HH:mm").
- Sekme rozetleri "Notlar 8 · Çöp 3" (arama/filtreden bağımsız toplam), sıralama menüsü (son düzenlenen / oluşturma), renk filtresi noktaları (tek renk, tekrar tıkla = kaldır), Ctrl+F (Explorer'da ve not penceresinde) aramaya odaklar.
- Tek vurgu ailesi: Explorer başlık çubuğu = sarı not şeridi (#F5D95A); vurgu #E3B21F; sarı not #FFF4B8.
- `ConfirmDialog`: şeffaf pencere + yumuşak gölge, yuvarlak köşe.
- `ThemedMenuRenderer`: tray `ContextMenuStrip` için özel renderer (krem yüzey, sarı vurgu, Segoe UI). `--show-tray-menu` bayrağı menüyü ekranda açar (ekran görüntüsü/test yardımcısı).
- NU1903: `Microsoft.Data.Sqlite 10.0.0` transitif olarak `SQLitePCLRaw.lib.e_sqlite3 2.1.11` çeker; `StickyNotes.Core.csproj`'a açık `2.1.14` referansı eklendi. `dotnet build` 0 uyarı.

**Dosyalar:** `Core/Note.cs` (Opacity, NoteIds, NoteOpacity, DateText), `Core/NoteRepository.cs`, `Core/NoteContent.cs`, `App/NoteWindow.xaml(.cs)`, `App/NoteViewModel.cs`, `App/NoteManager.cs`,
`App/ExplorerWindow.xaml(.cs)`, `App/ConfirmDialog.xaml`, `App/ThemedMenuRenderer.cs` (yeni), `App/App.xaml.cs`, `App/NoteTheme.cs`, `App/Themes/Theme.xaml`,
`tests/…` (+7 test, toplam 30), `tools/seed/` (test verisi + `--dump`), `tools/ui-test/lib150.ps1, tek150a/b/d/e.ps1, backdrop.ps1`.

**Test:** `dotnet test` 30/30. Gerçek exe ile elle test (UI Automation + fare + piksel örnekleme): `tools/ui-test/run.sh tek150a.ps1` (opaklık, şerit, boyutlandırma), `tek150b.ps1` (Explorer, diyalog, tray), `tek150d.ps1` (kalıcılık/kayma/tray), `tek150e.ps1` (12 not akıcılık).
Önce `dotnet run --project tools/seed -- <data-dir> 8`; ortam: `APP_EXE, DATA_DIR, SHOT_DIR, TOOLS_DIR`. PowerShell betikleri için `run.sh` zaten gerekli ayarı yapar; kendi çağrında `-ExecutionPolicy Bypass` (yalnızca o süreç) kullan.

**Bilinen kısıtlar / açık kalanlar**
- Opaklık sync edilmiyor ve `edited_at`'i etkilemiyor (sync tasarımında cihaz-yerel mi paylaşılan mı kararı gerek).
- Kenardan boyutlandırma yalnızca gölge payından; Aero Snap/maksimize denenmedi. Daraltılmışken yalnızca yatay boyutlanır.
- Gölge yaklaşık (katmanlı kenarlık), gerçek Gauss değil. Sürüklemede 12 notla ~2 sn'lik 60 adımlık sürüklemede uygulama CPU ~0,4 sn.
- Tray menüsü köşeleri düzdür (WinForms `ContextMenuStrip`). arm64 exe derlendi ama çalıştırılıp denenmedi (VM x64).
- Kaynak arşivi: .NET `ZipFile`/`Compress-Archive` kullan, `tar` kullanma.

## 2026-11-17 — TEK-149 — WinVMAgent: tema/tasarım katmanı, özel diyaloglar, daralt-genişlet

**Ne:** "Windows Forms gibi" duran arayüz tek bir temayla yeniden stillendi; kalıcı sil / çöpü boşalt onayları
temalı özel diyaloğa taşındı; not penceresine daralt/genişlet özelliği eklendi (SQLite'ta kalıcı). Sunucu/sync/git/push yok.
Müdür bu issue için de WinVMAgent'a kod yazma istisnası verdi (yalnızca TEK-149).

**Dosyalar**
- `src/StickyNotes.App/Themes/Theme.xaml` (yeni, `App.xaml` içinde birleştirilir): renk paleti, kaydırma çubuğu,
  düğmeler (varsayılan/Primary/Danger/Strip/Title), metin kutusu + arama kutusu, onay kutusu (checklist), hap sekmeler,
  liste + liste öğesi, ContextMenu/MenuItem (renk menüsü), ToolTip. Simgeler Segoe MDL2 Assets glifleridir (emoji yok).
- `src/StickyNotes.App/ConfirmDialog.xaml(.cs)` (yeni): çerçevesiz, temalı onay diyaloğu (`ConfirmDialog.Ask`).
  Enter/Esc = Vazgeç (varsayılan düğme güvenli olan), onay düğmesi kırmızı.
- `ExplorerWindow.xaml(.cs)`: çerçevesiz (`WindowChrome`) özel başlık çubuğu (küçült/kapat), temalı arama/sekme/liste/düğmeler; MessageBox yerine `ConfirmDialog`.
- `NoteWindow.xaml(.cs)`: `WindowChrome` ile çerçevesiz; pencere zemini not renginde (üstteki beyaz bant kalktı), özel boyut tutamacı,
  özel checkbox/kaydırma çubuğu/renk menüsü; şerit çift tık veya `CollapseButton` = daralt/genişlet; daraltılmışken şeritte notun ilk satırı görünür.
- `NoteViewModel.cs`: `IsCollapsed`, `Title`. `NoteManager.cs`: collapsed/ExpandedHeight kaydı.
- `src/StickyNotes.Core/Note.cs`, `NoteRepository.cs`: `NoteWindowState` artık `Collapsed` ve `ExpandedHeight` taşır;
  `window_state` tablosuna `collapsed INTEGER NOT NULL DEFAULT 0` ve `expanded_height REAL NOT NULL DEFAULT 0` sütunları.
- `tests/…/NoteRepositoryTests.cs`: 2 yeni test (collapsed gidiş-dönüş; eski şemalı DB'nin migrasyonu). Toplam 23 test.
- `tools/ui-test/tek149*.ps1`: bu issue'nun elle test senaryoları.

**Gözden geçirme düzeltmesi (aynı gün):** kaynak arşivi yine tar çıkmıştı (Git Bash `tar`); artık .NET `ZipFile` ile gerçek zip üretilip `Expand-Archive` ile açılabildiği doğrulanıyor. Migrasyon testindeki eksik virgül SQL'de düzeltildi. **Arşiv üretirken `tar` kullanma.**

**Migrasyon:** `NoteRepository` açılışta `PRAGMA table_info(window_state)` ile eksik sütunları `ALTER TABLE ADD COLUMN` ile ekler
(varsayılan 0 = genişletilmiş). Eski veritabanları veri kaybı olmadan açılır; ikinci açılışta tekrar çalışması zararsızdır. `notes` tablosu değişmedi (sync şeması aynı).

**Daralt/genişlet kararları:** daraltılmış yükseklik 30 px (şerit 28 + kenarlık); `MaxHeight=MinHeight=30` ile dikey boyutlanamaz.
Genişletince `ExpandedHeight` (daraltmadan önceki ActualHeight) geri yüklenir. Daraltma `edited_at`'i değiştirmez (yalnızca pencere durumu).
Eski satırda `expanded_height=0` ise `height` kullanılır.

**Test:** `dotnet test` (23/23). Elle test: UI Automation + fare + SendKeys ile gerçek exe; ekran görüntüleri issue yorumunda.

**Bilinen kısıtlar / açık kalanlar**
- Tek tema; tema seçici/dark mode yok (kapsam dışı).
- Not penceresi sınırları `WindowChrome` ile boyutlanır; Aero Snap/maksimize denenmedi.
- Segoe MDL2 Assets Windows 10/11'de var; eski Windows'ta glifler görünmeyebilir.
- Önceki (TEK-148) kısıtlar geçerli: arm64 çalıştırılıp denenmedi, NU1903 uyarısı, çok monitör/DPI denenmedi.

## 2026-11-17 — TEK-148 — WinVMAgent: Sticky Notes PoC (yalnızca yerel WPF, sunucusuz)

**Gözden geçirme düzeltmeleri (aynı gün):** kaynak arşivi gerçek zip olarak yeniden üretildi (ilki tar idi); `DeleteNote` pencereyi kapatmadan önce bekleyen kaydı yazıyor; `Application.SessionEnding` ile oturum kapanırken kayıtlar yazılıp tray ikonu temizleniyor; `ui.ps1` ekran görüntü klasörü `SHOT_DIR` ile verilir.

**Ne:** Simple Sticky Notes benzeri yerel masaüstü uygulaması sıfırdan yazıldı. Sunucu/sync/git/push yok.
**Neden:** Kullanıcı kararı (TEK-146 alt issue'su): önce çalışan bir Windows exe; senkronizasyon sonra.
Not: Müdür bu issue için WinVMAgent'a kod yazma istisnası verdi (WPF yalnızca Windows SDK'lı VM'de yazılıp denenebilir).

**Yapı / dosyalar**
- `src/StickyNotes.Core` (net10.0, Windows'tan bağımsız): `Note.cs` (modeller, renkler), `NoteContent.cs`
  (içerik JSON biçimi + metin↔checklist dönüşümü), `NoteRepository.cs` (Microsoft.Data.Sqlite, ham SQL).
- `src/StickyNotes.App` (net10.0-windows, WPF + WinForms yalnızca NotifyIcon, CommunityToolkit.Mvvm):
  `App.xaml(.cs)` (tek örnek mutex, tray, `--data-dir`), `NoteManager.cs` (pencere/kayıt koordinasyonu),
  `NoteViewModel.cs`, `NoteWindow.xaml(.cs)`, `ExplorerWindow.xaml(.cs)`, `NoteTheme.cs`, `AppLog.cs`,
  `Assets/app.ico` (kendi çizdiğimiz ikon, `tools/make-icon.ps1` ile üretildi).
- `tests/StickyNotes.Core.Tests` (xUnit): 21 test — içerik dönüşümleri, SQLite deposu, çöp/geri al/kalıcı sil/çöpü boşalt, arama, pencere durumu, kalıcılık.
- `tools/ui-test/` — UI Automation yardımcı betiği (elle test sırasında kullanıldı).

**Veri modeli (sync'e hazır, sync kodu YOK):** `notes(id UUIDv7, type text|checklist, content JSON, color,
edited_at UTC ISO-8601, deleted_at, purged_at)`. Pencere durumu ayrı tabloda (`window_state`), böylece
senkronizasyon yalnızca `notes` üzerinden yapılabilir. Kalıcı silme satırı siler değil: `purged_at` set edilir,
içerik boşaltılır (ileride sync için mezar taşı).

**Tasarım kararları**
- Kayıt: değişiklikten 500 ms sonra (DispatcherTimer debounce); çıkışta ve gizlerken anında. İçerik değişmediyse `edited_at` güncellenmez (yalnızca konum/boyut değişimi not tarihini bozmaz).
- Yazılım render (`RenderMode.SoftwareOnly`): GPU'suz VM'de WPF pencereleri boş (beyaz) çiziliyordu; not uygulaması için yazılım render yeterli.
- Tek örnek: `Local\StickyNotes_<data-dir yolunun SHA256'sı>`; farklı `--data-dir` ayrı örnek olarak çalışabilir.
- Çıkışta `NotifyIcon.Visible=false` + Dispose (hayalet ikon kalmaz; elle doğrulandı).
- Hedef .NET: VM'de 8.0.426 ve 10.0.402 SDK kurulu; en yeni (net10.0) seçildi.

**Çalıştırma / test:** bkz. `CLIENT.md`.

**Bilinen kısıtlar / açık kalanlar**
- `indent` alanı modelde ve JSON'da var, arayüzde girinti verme (Tab) henüz yok.
- Kısayollar yalnızca uygulama penceresi odaktayken çalışır (Ctrl+N, Alt+X); sistem geneli yok (kapsam gereği).
- NuGet uyarısı NU1903: `SQLitePCLRaw.lib.e_sqlite3 2.1.11` için bilinen yüksek önem dereceli SQLite uyarısı (GHSA-2m69-gcr7-jv3q); daha yeni paket çıkınca güncellenmeli.
- win-arm64 sürümü derlendi ancak bu VM x64 olduğundan çalıştırılıp denenmedi.
- Pencere durumu çok monitörlü ekranlarda yalnızca görünür alan içine alınarak geri yüklenir; DPI farklı monitör senaryosu denenmedi.
- Sunucu/API/sync, hatırlatıcı, tema, export vb. kapsam dışı (issue'da belirtildiği gibi).
