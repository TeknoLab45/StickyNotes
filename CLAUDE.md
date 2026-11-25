# StickyNotes — repo bilgisi

Windows tray yapışkan not uygulaması (WPF, .NET 10, SQLite, isteğe bağlı Memos eşitlemesi). Uygulama repo kökündedir.

## Düzen
- `src/StickyNotes.Core` — arayüzsüz mantık: `Note`, `NoteRepository` (SQLite), `RichText`, `Sync/` (`MemoMarkdown`, `MemosClient`, `SyncEngine`, `SyncSettings`).
- `src/StickyNotes.App` — WPF (net10.0-windows): pencereler, tray, kısayollar, `SyncService`, `TokenStore` (DPAPI).
- `tests/StickyNotes.Core.Tests` — xUnit; `MemosLiveTests` yalnızca `MEMOS_TEST_URL`/`MEMOS_TEST_TOKEN`/`MEMOS_TEST_SPACE` varsa koşar.
- `tools` — ikon, seed, Windows UI test betikleri (`ui-test/`).
- `docs` — dokümanlar ve değişiklik günlüğü (indeks: `docs/README.md`).

## Derleme ve test (repo kökünde, .NET SDK 10)
- Linux: `dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true`, `dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true` (WPF çalıştırılamaz; arayüz Windows'ta elle denenir).
- Windows: `dotnet build`, `dotnet test`. Publish komutları: `docs/CLIENT.md`.

## Kurallar
- Memos'ta çöpe atma sunucuya istek göndermez; memo yalnızca kalıcı silmede/çöp boşaltmada silinir (`docs/SENKRON.md`).
- Biçim `marks` olarak saklanır; biçimi bilmeyen eski sürüm biçimi kaybeder (`docs/BICIMLENDIRME.md`).
- `StickyNotes.exe` git'te izlenir (`.gitignore` istisnası); `dist/` izlenmez. Yeni exe yalnızca istenince derlenip konur.
- Token düz metin saklanmaz ve günlüğe yazılmaz.
- Belge dosya adlarında Türkçe karakter kullanılmaz; içerik Türkçedir.
