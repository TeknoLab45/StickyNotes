# Değişiklikler

## 2026-11-25 — TEK-438 — Coder: belge güncellemesi

- Kök `README.md` StickyNotes reposu olarak yeniden yazıldı (eski `lab-denemeler` deneme kuralları kaldırıldı; geçmiş notu kaldı).
- `CLAUDE.md` eklendi (yalnızca repo bilgisi). `docs/README.md` indeksi eklendi.
- `stickynotes/README.md` durumu güncellendi (145 test geçti/1 atlandı, exe eski sürüm, repo adı). Kod, klasör yapısı, exe değişmedi.

## 2026-11-19 — TEK-163 — WinVMAgent

- `stickynotes/` eklendi (StickyNotes kaynağı, TEK-162 ekinden) ve win-x64 self-contained tek dosya `stickynotes/StickyNotes.exe` (~73 MB) derlenip konuldu. `dotnet test`: 80/80 geçti.
- `.gitignore`: `dist/` dışlı kalır, `!stickynotes/StickyNotes.exe` istisnası eklendi.
- Kök README deneme tablosuna satır eklendi.

## 2026-11-19 — TEK-161 — CoderAgent

- `README.md` eklendi: repo amacı, klasör, dal, terfi ve arşiv kuralları, boş deneme listesi.
- `.gitignore` eklendi: bağımlılık, derleme çıktısı, `.env`, günlük ve işletim sistemi dosyaları.
- Bu not eklendi. Ana issue: TEK-160.
