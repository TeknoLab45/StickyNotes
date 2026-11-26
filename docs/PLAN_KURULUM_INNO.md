# Plan: Inno Setup kurulum paketi ve release CI (TEK-460)

Yazan: Lead, 2026-11-26. Durum: plan. Uygulamayı Coder yapacak.

## Amaç ve kapsam
- Windows (x64) için tek bir kurulum dosyası: `StickyNotes-Setup-<sürüm>.exe` (Inno Setup 6).
- Sürüm `0.3.0`. Değişiklik notları kullanıcının okuyacağı ayrı bir dosyada tutulacak: `CHANGELOG.md`.
- `v*` tag'i push edilince GitHub Actions setup exe'yi derleyip tag'in Release'ine **tek asset** olarak yükleyecek. Taşınabilir `StickyNotes.exe` artık Release'e yüklenmeyecek (istek: "sadece Windows için setup exe").
- Kod onaylanınca `main`'e `--no-ff` merge ve `v0.3.0` tag'i atılacak. İzin kullanıcının kendi isteğinde var (aşağıda alıntı).
- Kapsam dışı: arm64 ve diğer platformlar, kod imzalama, otomatik güncelleme, depoda izlenen eski `StickyNotes.exe`'nin güncellenmesi.

## Kaynak kod incelemesi (Lead)
- `origin/main` = `533b813`. `v0.2.0` tag'inden sonra yalnızca TEK-444 var (klasör taşıma ve belgeler, kod değişmedi).
- Linux'ta `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true`: 0 hata. `dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true`: 145 geçti, 1 atlandı (`MemosLiveTests`, env yok).
- Kurulumu ilgilendiren bulgular:
  - Veri ve log `%LOCALAPPDATA%\StickyNotes` altında. Exe'nin yanına yazılan bir şey yok, bu yüzden uygulama Program Files'tan da çalışır.
  - Tek örnek kilidi: `Local\StickyNotes_<veri klasörü hash'i>` (`src/StickyNotes.App/App.xaml.cs:38`). Ad hash'li olduğu için Inno `AppMutex` bu kilidi kullanamaz. Kurulum için sabit adlı ek bir mutex gerekir (İş 1).
  - Uygulamada "Windows açılışında başlat" özelliği yok. Kurulumda isteğe bağlı bir görev olarak eklenecek.
- Kod bulguları (KRİTİK yok, kurulum yapılabilir):
  - ÖNEMLİ: Mutex adı kullanıcıya göre değiştiği için Inno çalışan uygulamayı göremez. Güncelleme sırasında kilitli exe'nin üzerine yazılamaz ya da kurulum yeniden başlatma ister. İş 1 bunu çözer. Ek risk: not penceresini kapatmak yalnızca pencereyi gizler (`NoteWindow.xaml.cs:359`), bu yüzden Restart Manager ile uygulamayı kapatmaya güvenilmez. Kurulumda `CloseApplications=no` kullanılır ve kullanıcıdan uygulamayı kapatması istenir.
  - ÖNERİ (kapsam dışı, ayrı iş): İkinci kez başlatılan uygulama hiçbir şey göstermeden kapanıyor (`App.xaml.cs:54`). Uygulama tray'de açıkken Başlat menüsü kısayoluna tıklamak bir şey yapmıyor gibi görünür. Çalışan örneğe "notları göster" sinyali gönderilebilir.
  - Kontrol edildi, sorun yok: genel hata yakalayıcılar, tek `async void` (try/catch içinde), SQLite yalnızca UI iş parçacığında, eşitleme turunun yeniden girişe karşı kilidi ve kesinti sonrası devam etmesi, token'ın DPAPI ile saklanıp loga yazılmaması, çıkışta ve Windows kapanırken kayıt.

## Mimari ve neden
- **Kullanıcı başına kurulum** (`PrivilegesRequired=lowest`, `DefaultDirName={autopf}\StickyNotes`, yani `%LOCALAPPDATA%\Programs\StickyNotes`). Neden: tray uygulaması, veri zaten kullanıcı profilinde, yönetici izni (UAC) istenmez. Kullanıcı isterse kurulum penceresinden tüm kullanıcılar için kurmayı seçebilir (`PrivilegesRequiredOverridesAllowed=dialog`).
- Kurulumun içine mevcut publish çıktısı girer: win-x64, self-contained, tek dosya `StickyNotes.exe`. Böylece kullanıcının makinesinde .NET gerekmez.
- `ArchitecturesAllowed=x64compatible`, `ArchitecturesInstallIn64BitMode=x64compatible`.
- Sabit `AppId` (GUID, bir kez üretilir ve hiç değişmez). Yeni sürüm aynı klasöre kurulur ve eskisinin üzerine yazar.
- Sürüm tek yerde tutulur: csproj `<Version>`. CI tag'i (`v0.3.0`) csproj ile karşılaştırır, uyuşmazsa job hata verir. Sürüm `iscc /DAppVersion=...` ile kuruluma geçer.
- Release notu, `CHANGELOG.md`'nin o sürüme ait bölümünden alınır (`gh release create --notes-file`). Bölüm yoksa job hata verir.

## İş listesi (tek dal, sıralı; önerilen ajan: Coder)

### İş 1 — Kurulum için sabit mutex
- `App.OnStartup` mevcut kilide ek olarak iki sabit mutex açar: `StickyNotesSetupMutex` ve `Global\StickyNotesSetupMutex`. Bunlar yalnızca kurulumun uygulamanın açık olduğunu görmesi içindir. `Global\` olanı oluşturulamazsa (yetki) hata yutulur ve loga yazılır, uygulama açılmaya devam eder. Kapanışta serbest bırakılır. İkinci örneği engelleme davranışı değişmez.
- Bitti ölçütü: build 0 hata. `.iss` dosyasında `AppMutex=StickyNotesSetupMutex,Global\StickyNotesSetupMutex`. Uygulama açıkken kurulum "kapatın" uyarısı verir (Windows'ta elle denenecek, bkz. "Bilinen kısıt").

### İş 2 — `installer/StickyNotes.iss`
- `[Setup]`: AppId, AppName=StickyNotes, AppVersion={#AppVersion}, AppPublisher=TeknoLAB, `DefaultDirName={autopf}\StickyNotes`, `DefaultGroupName=StickyNotes`, `PrivilegesRequired=lowest`, `PrivilegesRequiredOverridesAllowed=dialog`, x64 ayarları, `SetupIconFile=..\src\StickyNotes.App\Assets\app.ico`, `UninstallDisplayIcon={app}\StickyNotes.exe`, `OutputBaseFilename=StickyNotes-Setup-{#AppVersion}`, `Compression=lzma2`, `SolidCompression=yes`, `WizardStyle=modern`, `AppMutex=StickyNotesSetupMutex,Global\StickyNotesSetupMutex`, `CloseApplications=no`.
- `[Languages]`: Türkçe (`compiler:Languages\Turkish.isl`) ve İngilizce.
- `[Files]`: `{#PublishDir}\StickyNotes.exe` → `{app}`. `PublishDir` varsayılanı `..\dist\win-x64`.
- `[Icons]`: Başlat menüsü kısayolu, kaldırma kısayolu. `desktopicon` görevi seçilirse masaüstü kısayolu. `autostart` görevi seçilirse `{userstartup}` kısayolu (varsayılan: işaretsiz).
- `[Run]`: kurulum bitince "StickyNotes'u başlat" (`postinstall nowait skipifsilent`).
- Kaldırma: `%LOCALAPPDATA%\StickyNotes` içindeki notlar ve ayarlar **silinmez**.
- Bitti ölçütü: Windows'ta `iscc /DAppVersion=0.3.0 installer\StickyNotes.iss` hatasız `installer\Output\StickyNotes-Setup-0.3.0.exe` üretir. `installer/Output/` `.gitignore`'a eklenir.

### İş 3 — Sürüm ve changelog
- csproj `<Version>0.3.0</Version>`.
- Kökte `CHANGELOG.md`: `## [0.3.0] - <tarih>` (Windows kurulum paketi, açılışta başlatma seçeneği, kaldırmada verinin korunması) ve `## [0.2.0] - 2026-11-25` (Memos eşitlemesi, salt okunur başkasının notu, Memos'tan yalnızca kalıcı silmede silme, metin biçimi, opaklık, Ayarlar küçük ekran düzeltmesi). Kaynak: `docs/DEGISIKLIKLER.md`. Kullanıcının anlayacağı dille, kısa yazılır.
- Bitti ölçütü: CI'daki çıkarma betiği `0.3.0` bölümünü boş olmayan bir metin olarak bulur.

### İş 4 — `release.yml` güncellemesi
Adımlar: checkout → setup-dotnet 10 → **sürüm kontrolü** (tag ile csproj `<Version>` eşit mi) → test → publish (mevcut komut) → Inno Setup (`iscc` yoksa `choco install innosetup -y --no-progress`) → `iscc /DAppVersion=<sürüm> /DPublishDir=<tam yol> installer\StickyNotes.iss` → `CHANGELOG.md`'den o sürümün bölümünü `notes.md`'ye çıkar → Release yoksa `gh release create <tag> --title "StickyNotes <sürüm>" --notes-file notes.md`, varsa `gh release edit --notes-file` → `gh release upload <tag> installer\Output\StickyNotes-Setup-<sürüm>.exe --clobber`.
- `permissions: contents: write` kalır. `runs-on: windows-latest`.
- Bitti ölçütü: `v0.3.0` tag'inde `release` çalışması yeşil biter. Release'te yalnızca `StickyNotes-Setup-0.3.0.exe` asset'i ve CHANGELOG'daki not vardır.
- Hata ayıklamak için kendi dalında `workflow_dispatch` eklenebilir. Yayın yalnızca tag ile yapılır.

### İş 5 — Belgeler
- `docs/CLIENT.md`: "Kurulum" bölümü (setup'ı indir, çalıştır, kurulum yeri, açılışta başlatma, kaldırınca veri korunur). "Sürüm çıkarma" bölümü yeni akışa göre güncellenir (Version + CHANGELOG + tag).
- `README.md` durum satırı, `docs/README.md` indeksine bu plan ve `CHANGELOG.md`, `docs/DEGISIKLIKLER.md`'ye TEK-460 kaydı.

### İş 6 — Merge ve tag (yerel testler geçtikten sonra)
- `main`'e `--no-ff` merge → `git tag v0.3.0` → `git push origin main v0.3.0`. CI release'i oluşturur.
- Coder bir kez `gh run view <id>` ile sonucu ve `gh release view v0.3.0` ile asset'i raporlar. Kullanıcı "CI tag'e asset yüklesin" diye istediği için sonuç kontrol edilir.
- CI kırmızı biterse: tag'i silme ya da yeniden atma (force-push yasak). Düzeltip `v0.3.1` çıkar ve durumu raporla.

## Riskler ve açık noktalar
- **Bilinen kısıt:** Kurulum Linux'ta derlenemez ve denenemez. İlk gerçek doğrulama CI'da olur. Kurulumu kendi makinesinde denemek isteyen (WinVMAgent ya da kullanıcı) kurulumu, açılışta başlatma seçeneğini ve kaldırmayı elle dener.
- `windows-latest` imajında Inno Setup kurulu olmayabilir. Bu durum için choco ile kurma yedeği planda var.
- İmzasız exe olduğu için SmartScreen "tanınmayan uygulama" uyarısı verir. Kod imzalama kapsam dışı.
- Repo private olduğu için asset'i yalnızca repoya erişimi olanlar indirebilir.
- Depoda izlenen eski `StickyNotes.exe` (Memos öncesi sürüm) yanlış yönlendirebilir. Bu işte dokunulmaz; kaldırılması ayrı bir karardır.

## İzin
Kullanıcının kendi isteği (TEK-460): "Merge ve tag çıkart." Bu izin yalnızca TEK-460 içindir ve "sorun / özellik eksiği hatası yoksa" şartına bağlıdır.
