# StickyNotes — repo bilgisi

Windows tray yapışkan not uygulaması (WPF, .NET 10, SQLite, isteğe bağlı Memos eşitlemesi). Uygulama `stickynotes/` altındadır; repo kökünde yalnızca `README.md`, `CLAUDE.md`, `docs/` ve `.gitignore` vardır.

## Düzen
- `stickynotes/src/StickyNotes.Core` — arayüzsüz mantık: `Note`, `NoteRepository` (SQLite), `RichText`, `Sync/` (`MemoMarkdown`, `MemosClient`, `SyncEngine`, `SyncSettings`).
- `stickynotes/src/StickyNotes.App` — WPF (net10.0-windows): pencereler, tray, kısayollar, `SyncService`, `TokenStore` (DPAPI).
- `stickynotes/tests/StickyNotes.Core.Tests` — xUnit; `MemosLiveTests` yalnızca `MEMOS_TEST_URL`/`MEMOS_TEST_TOKEN`/`MEMOS_TEST_SPACE` varsa koşar.
- `stickynotes/tools` — ikon, seed, Windows UI test betikleri (`ui-test/`).
- `stickynotes/docs` — uygulama dokümanları (indeks: `stickynotes/docs/README.md`); repo düzeyi günlük: `docs/`.

## Derleme ve test (`stickynotes/` içinde, .NET SDK 10)
- Linux: `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true`, `dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true` (WPF çalıştırılamaz; arayüz Windows'ta elle denenir).
- Windows: `dotnet build`, `dotnet test`. Publish komutları: `stickynotes/docs/CLIENT.md`.

## Kurallar
- Memos'ta çöpe atma sunucuya istek göndermez; memo yalnızca kalıcı silmede/çöp boşaltmada silinir (`stickynotes/docs/SENKRON.md`).
- Biçim `marks` olarak saklanır; biçimi bilmeyen eski sürüm biçimi kaybeder (`stickynotes/docs/BICIMLENDIRME.md`).
- `stickynotes/StickyNotes.exe` git'te izlenir (`.gitignore` istisnası); `dist/` izlenmez. Yeni exe yalnızca istenince derlenip konur.
- Token düz metin saklanmaz ve günlüğe yazılmaz.
- Belge dosya adlarında Türkçe karakter kullanılmaz; içerik Türkçedir.
