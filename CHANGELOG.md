# Değişiklik günlüğü

Kullanıcıya dönük sürüm notları. Ayrıntılı geliştirme kayıtları: `docs/DEGISIKLIKLER.md`.

## [0.4.0] - 2026-11-26

- Ctrl+B artık yazarken de çalışır: seçim yokken kalın modunu açar/kapatır.
- F2 ile not başlığı düzenlenir. Başlık Memos'ta ilk satır (`# Başlık`) olur; başlıksız notta tarih görünür.
- Not içeriği Markdown: `- `, `1. `, `- [ ] ` listeleri Enter ile devam eder, Tab / Shift+Tab ile girintilenir.
- Ctrl+Shift+V ve sağ tık menüsü ile düz metin yapıştırma.
- Liste modu kaldırıldı; eski liste notları Markdown görev listesine çevrilir.
- Tüm bilgisayarları güncelleyin: eski sürüm başlığı korumaz.

## [0.3.1] - 2026-11-26

- Depo public yapilmadan once gizli bilgi taramasi yapildi (sonuc: temiz); rapor `docs/GUVENLIK_TARAMASI.md`. Uygulama davranisi degismedi.

## [0.3.0] - 2026-11-26

- Windows için kurulum paketi eklendi (`StickyNotes-Setup-0.3.0.exe`): yönetici izni istemeden kullanıcı başına kurulur, Türkçe ve İngilizce arayüz.
- Kurulumda isteğe bağlı masaüstü kısayolu ve "Windows açılışında başlat" seçeneği.
- Uygulama açıkken kurulum çalıştırılırsa önce uygulamayı kapatmanız istenir.
- Kaldırma notlarınızı ve ayarlarınızı silmez.

## [0.2.0] - 2026-11-25

- Memos ile eşitleme: Ayarlar'dan adres ve token girilir, notlar iki yönlü eşitlenir.
- Başkasının Memos notu salt okunur açılır (kilit simgesi).
- Çöpe atılan not Memos'tan silinmez; yalnızca "Kalıcı sil" ya da "Çöpü boşalt" ile silinir.
- Metin biçimi: kalın, altı çizili, üstü çizili.
- Not başına opaklık ayarı.
- Küçük ekranlarda Ayarlar penceresi düzeltildi.
