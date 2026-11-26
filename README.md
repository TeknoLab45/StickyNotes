# StickyNotes

Windows için tray'de çalışan yapışkan not uygulaması: WPF, .NET 10, SQLite; isteğe bağlı Memos eşitlemesi. Repo: `TeknoLab45/StickyNotes` (private).

- **Durum:** geliştirmede (sürüm 0.3.0: Windows kurulum paketi, bkz. [CHANGELOG.md](CHANGELOG.md); kurulum: [docs/CLIENT.md](docs/CLIENT.md#kurulum)). Memos eşitlemesi (Core + arayüz) `main`'de; Memos'tan silme yalnızca kalıcı silmede (TEK-432). Core testleri 145 geçti, 1 atlandı (`MemosLiveTests`, env yoksa). Depodaki `StickyNotes.exe` eski sürümdür (Memos eşitlemesi yok); yeni exe derlenmedi.
- **Çalıştırma:** `StickyNotes.exe` (win-x64, self-contained tek dosya, ~73 MB; .NET kurulumu gerekmez). Tray ikonu belirir. Veri: `%LOCALAPPDATA%\StickyNotes`; `--data-dir <yol>` ile değişir.

> Not: Repo eskiden `batv45/lab-denemeler` (deneme/prototip monoreposu) idi; StickyNotes kendi reposuna geçti.

## Yapı

- `src/StickyNotes.Core` (not modeli, SQLite, eşitleme), `src/StickyNotes.App` (WPF arayüz), `tests/`, `tools/` (ikon, seed, UI test betikleri), `StickyNotes.slnx`
- [docs/](docs/README.md) — uygulama dokümanları ve değişiklik günlüğü.

## Derleme ve test

```
dotnet build          # Linux'ta: dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true
dotnet test           # Linux'ta: dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true
dotnet publish src/StickyNotes.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o dist/win-x64
```

Çıkan `dist/win-x64/StickyNotes.exe` (`dist/` gitignore'da). arm64, küçük sürüm, kullanım, kısayollar ve Memos ayarı: [docs/CLIENT.md](docs/CLIENT.md).

## Dal kuralı

- Ajanlar `agent/...` dallarında çalışır.
- `main` dalına yalnızca kullanıcının açık izniyle `--no-ff` merge edilir.
