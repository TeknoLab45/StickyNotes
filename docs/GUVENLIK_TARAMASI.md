# Gizli bilgi taramasi (public yapmadan once)

Tarih: 2026-11-26 — TEK-463 — Coder. Bu dosya public olacagi icin sir degeri icermez.

## Kapsam
- Git gecmisi: `git rev-list --all` ile 24 commit; bütün yerel/uzak dallar ve `v0.2.0`, `v0.3.0` tag'leri; silinmis dosyalar dahil (tum commit agaclari).
- Commit mesajlari ve yazar/committer e-postalari.
- Ikili dosyalar: gecmiste tek buyuk blob var (`StickyNotes.exe`, ~73 MB, tek surum); byte duzeyinde arandi.
- `.github/workflows/release.yml`, depodaki `secrets.*` kullanimi.
- GitHub tarafi: Release asset'leri (`v0.2.0`, `v0.3.0`), Actions calisma loglari, issue/PR listesi, repo Actions secret/artifact sayisi.

## Komutlar (ozet)
- `git grep -I -i -E '<password|secret|token|api key|private key|bearer|connection string|Server=|AKIA|ghp_|github_pat_|xox|eyJ|memos_pat_|user:pass@>' $(git rev-list --all)`
- `git grep -h -o -E` ile token biciminde (`memos_pat_...`, `AKIA...`, `ghp_...`, JWT, `-----BEGIN ...-----`), URL, IP, e-posta ve yerel yol desenleri; sonuclar `sort | uniq -c`.
- `git rev-list --objects --all | git cat-file --batch-check` ile buyuk blob listesi; exe icinde `memos_pat_`, IP, `/home/`, `C:\Users\`, e-posta, kullanici adi desenleri.
- `gh release list/view`, `gh run view --log`, `gh issue/pr list`, `gh api .../actions/secrets|artifacts`.
- gitleaks bu makinede kurulu degildi; elle arama kullanildi.

## Sonuc
**Gercek sir yok.** Parola, token, ozel anahtar, baglanti dizesi, kisisel not verisi bulunmadi.
- `memos_pat_SECRET_TEST_TOKEN`: birim testlerindeki uydurma sahte deger.
- Canli testler ve `tools/ui-test/tek166.ps1` token'i ortam degiskeninden (`MEMOS_TEST_*`) okur; degerler depoda yok.
- Uygulama token'i DPAPI ile sifreler; duz token depoya/log'a yazilmaz.
- Exe icinde sir ya da kisisel yol/e-posta bulunmadi (yalnizca Microsoft runtime pdb yollari).
- Release logunda yalnizca GitHub'in maskeledigi (`***`) otomatik `GITHUB_TOKEN` gorunur; repo secret'i yok (0), artifact yok (0). Issue/PR yok.

## Herkese acilacak, sir olmayan bilgiler (dusuk risk)
- Commit e-postasi: yazar/committer `activ45@hotmail.com` (24 commitin hepsi; "Batuhan" / "Batuhan OK").
- Ornek ozel ag adresi `http://192.168.1.10:5230` (dokuman/ipucu metni; RFC1918, yalniz ornek).
- Gelistirme makinesi yolu `/home/dietpi/.dotnet/dotnet` (docs/DEGISIKLIKLER.md ve eski gecmis).
- Test/ornek adresler: `http://127.0.0.1:5230`, `http://localhost:5231`, `http://memos.test`, `http://h:5230`.
- Gercek sunucu alan adi, Tailscale adi, kisisel e-posta ya da gercek not verisi icerigi bulunmadi.
- Eski gecmiste `stickynotes/` onekli yollar ve bozuk kodlanmis bir dosya adi var (kozmetik).
- Kok dizinde `StickyNotes.exe` (~73 MB) izleniyor; sir icermiyor ama repoyu sisiriyor (kapsam disi, not).

## Diger
- LICENSE dosyasi yok (eklenmedi; karar kullanicinin). Lisanssiz public repo varsayilan olarak "tum haklari saklidir" demektir.
