# StickyNotes

- **Amaç:** Windows için tray'de çalışan yapışkan not uygulaması (WPF, .NET 10, SQLite).
- **Durum:** geliştirmede (sürüm 0.2.0 kaynağı). Memos eşitlemesi (Core + arayüz) `main`'de; Memos'tan silme yalnızca kalıcı silmede (TEK-432). Core testleri 145 geçti, 1 atlandı (`MemosLiveTests`, env yoksa). Depodaki `StickyNotes.exe` eski sürümdür; yeni exe derlenmedi.
- **Başlangıç:** 2026-11-19 (TEK-162, alt issue TEK-163); Memos: TEK-166 (TEK-167, TEK-168); son: TEK-432
- **Repo:** `TeknoLab45/StickyNotes` (kök `README.md`; bu klasör uygulamanın kendisidir)

## Çalıştırma
`StickyNotes.exe` (win-x64, self-contained tek dosya, ~73 MB; .NET kurulumu gerekmez). Bu exe 0.2.0 öncesi sürümdür (Memos eşitlemesi yok); güncel kaynağın exe'si henüz derlenmedi. Tray ikonu belirir. Veri: `%LOCALAPPDATA%\StickyNotes`; `--data-dir <yol>` ile değişir.

## Derleme (stickynotes/ içinde, .NET SDK 10)
```
dotnet test
dotnet publish src/StickyNotes.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-x64
```
Çıkan `dist/win-x64/StickyNotes.exe` (`dist/` gitignore'da) gerekirse bu klasöre kopyalanır. arm64 ve küçük (framework-dependent) sürüm komutları, kullanım ve kısayollar: `docs/CLIENT.md`.

## Klasörler
`src/` (Core + App), `tests/`, `tools/` (ikon, seed, UI test betikleri), `docs/` (indeks: `docs/README.md`).
