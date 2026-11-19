# StickyNotes

- **Amaç:** Windows için tray'de çalışan yapışkan not uygulaması (WPF, .NET 10, SQLite).
- **Durum:** deneme; derlenmiş exe dahil. Core testleri 80/80 geçti.
- **Tarih:** 2026-11-19
- **Issue:** TEK-162 (alt issue TEK-163)

## Çalıştırma
`StickyNotes.exe` (win-x64, self-contained tek dosya, ~73 MB; .NET kurulumu gerekmez). Tray ikonu belirir. Veri: `%LOCALAPPDATA%\StickyNotes`; `--data-dir <yol>` ile değişir.

## Derleme (stickynotes/ içinde, .NET SDK 10)
```
dotnet test
dotnet publish src/StickyNotes.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-x64
```
Çıkan `dist/win-x64/StickyNotes.exe` bu klasöre kopyalandı (`dist/` gitignore'da). arm64 ve küçük (framework-dependent) sürüm komutları, kullanım ve kısayollar: `docs/CLIENT.md`.

## Klasörler
`src/` (Core + App), `tests/`, `tools/` (ikon, seed, UI test betikleri), `docs/`.
