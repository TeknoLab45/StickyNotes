# StickyNotes

- **Amaç:** Windows için tray'de çalışan yapışkan not uygulaması (WPF, .NET 10, SQLite).
- **Durum:** deneme. Memos eşitlemesi (Core + arayüz) kodlandı (TEK-167/TEK-168); Windows'ta UI testi ve exe yayını bekliyor. Core testleri 124 geçti, 1 atlandı.
- **Tarih:** 2026-11-19
- **Issue:** TEK-162 (alt issue TEK-163); Memos: TEK-166 (TEK-167, TEK-168)

## Çalıştırma
`StickyNotes.exe` (win-x64, self-contained tek dosya, ~73 MB; .NET kurulumu gerekmez). Bu sürümün exe'si henüz güncellenmedi (sürüm 0.2.0 kaynağı hazır; exe WinVMAgent işinde). Tray ikonu belirir. Veri: `%LOCALAPPDATA%\StickyNotes`; `--data-dir <yol>` ile değişir.

## Derleme (stickynotes/ içinde, .NET SDK 10)
```
dotnet test
dotnet publish src/StickyNotes.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-x64
```
Çıkan `dist/win-x64/StickyNotes.exe` bu klasöre kopyalandı (`dist/` gitignore'da). arm64 ve küçük (framework-dependent) sürüm komutları, kullanım ve kısayollar: `docs/CLIENT.md`.

## Klasörler
`src/` (Core + App), `tests/`, `tools/` (ikon, seed, UI test betikleri), `docs/`.
