# StickyNotes

Windows için tray'de çalışan yapışkan not uygulaması: WPF, .NET 10, SQLite; isteğe bağlı Memos eşitlemesi. Repo: `TeknoLab45/StickyNotes` (private).

> Not: Repo eskiden `batv45/lab-denemeler` (TeknoLAB deneme/prototip monoreposu) idi; StickyNotes terfi edip kendi reposuna geçti. Eski deneme reposu kuralları artık geçerli değil.

## Yapı

- [stickynotes/](stickynotes/) — uygulama (kaynak, testler, araçlar). Kök dizine alınmadı; tüm `dotnet` komutları bu klasörde çalışır.
  - `src/StickyNotes.Core` (not modeli, SQLite, eşitleme), `src/StickyNotes.App` (WPF arayüz), `tests/`, `tools/`, `docs/`
  - `StickyNotes.exe` — win-x64 self-contained tek dosya; güncel olmayabilir (bkz. [stickynotes/README.md](stickynotes/README.md)).
- [docs/](docs/) — repo düzeyi değişiklik günlüğü. Uygulama dokümanları: [stickynotes/docs/README.md](stickynotes/docs/README.md).

## Hızlı başlangıç

```
cd stickynotes
dotnet build          # Linux'ta: dotnet build StickyNotes.slnx -p:EnableWindowsTargeting=true
dotnet test           # Linux'ta: dotnet test tests/StickyNotes.Core.Tests -p:EnableWindowsTargeting=true
```

Publish komutları, kullanım, kısayollar ve Memos eşitleme ayarı: [stickynotes/docs/CLIENT.md](stickynotes/docs/CLIENT.md).

## Dal kuralı

- Ajanlar `agent/...` dallarında çalışır.
- `main` dalına yalnızca kullanıcının açık izniyle `--no-ff` merge edilir.
