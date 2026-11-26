# Not düzenleyici: başlık, Markdown, kalın modu, düz metin yapıştırma (TEK-467, 0.4.0)

## Kararlar

| Konu | Karar | Neden |
|---|---|---|
| Not içeriği | Markdown **kaynak metni** olarak yazılır ve görünür (`- ` ekranda `-` kalır, madde imine dönüşmez) | Memos ile bire bir aynı metin; WPF'te WYSIWYG liste ↔ markdown eşlemesi ayrı ve büyük iş |
| Başlık | Ayrı alan (`content` JSON `title`), Memos'ta ilk satır **`# Başlık`** | Düz ilk satır olsaydı Memos'tan geri okurken başlık ile içeriğin ilk satırı ayırt edilemezdi |
| Başlıksız not | Şeritte ve Explorer'da oluşturma tarihi (`dd.MM.yyyy HH:mm`); Memos'a tarih **yazılmaz** | Memos'tan gelen not değişmeden kalır |
| Liste modu | Kaldırıldı; eski checklist notları `- [ ]` / `- [x]` metnine çevrilir | Tek düzenleyici |

## Başlık
- **F2** (not penceresi odaktayken, daraltılmışken de): şeritte başlık kutusu açılır. Enter ya da odağın başka yere geçmesi kaydeder, Esc vazgeçer, boş bırakmak başlığı kaldırır. Salt okunur notta çalışmaz.
- Memos → yerel: memonun ilk satırı `# X` ise başlık `X`, kalanı içerik. `##`, `#etiket` ya da sonraki satırlardaki başlıklar içerik sayılır.
- Yerel → Memos: başlık varsa `# Başlık` + içerik.
- Memos'tan gelen başlıksız notun tarihi memonun `createTime` değeridir (`notes.created_at`). Eski sürümün çektiği notlarda sonraki eşitlemede doldurulur.
- Çakışma kopyasında etiket başlıklı notta başlığa eklenir.

## Markdown listeleri
- Tanınanlar: `- `, `* `, `+ `, `1. `, `1) `, `- [ ] ` / `- [x] `.
- **Enter**: yeni madde (numara artar, görev işaretsiz başlar). Boş maddede Enter listeden çıkar.
- **Tab / Shift+Tab**: liste satırını (seçimdeki tüm liste satırlarını) 2 boşluk içeri/dışarı alır. Liste dışında Tab sekme karakteri yazar.
- Kod: `Core/MarkdownList.cs` (saf mantık), `NoteWindow.xaml.cs` (tuşlar).

## Kalın modu
- Seçim yokken **Ctrl+B** (ya da sağ tık → Kalın): bundan sonra yazılanlar kalın; tekrar Ctrl+B normale döner. İmleç taşınınca mod, imlecin bulunduğu yerin biçimine döner. Liste maddesinde Enter modu korur.
- Seçim varken davranış eskisi gibi. Altı çizili / üstü çizili yalnızca seçili yazıya uygulanır.
- Markdown'a yazarken `**` işaretleri boşlukların ve liste işaretinin dışında kalır (`- **madde** `).

## Düz metin yapıştırma
- **Ctrl+Shift+V** (sol ya da sağ Shift) ve sağ tık → "Düz metin olarak yapıştır": panodaki metin hiçbir biçim almadan (kalın modu açık olsa da) eklenir.
- Ctrl+V de kaynak biçimini zaten atar; farkı imleçteki biçimi (kalın modu) devralmasıdır.

## Uyumluluk
- `notes` tablosuna `created_at` sütunu eklenir; checklist satırları açılışta metne çevrilir (`edited_at` değişmez, Memos'a yazma doğurmaz).
- Eski sürüm başlığı bilmez: eski sürümde düzenlenen notun başlığı düşer. Tüm PC'ler güncellenmeli.
- Eşitlemede `**`/`~~` içeren bir satır aynen geri üretilemiyorsa (ör. `2 ** 3 ** 4`) biçim sayılmaz, metin olarak kalır.

## Doğrulama (Windows 10 VM, .NET SDK 10, 2026-11-26)
- `dotnet build` → 0 uyarı, 0 hata. `dotnet test` → 175 geçti, 1 atlandı (`MemosLiveTests`).
- `tools/ui-test/tek467.ps1` (gerçek pencere, SendKeys) → 16/16 geçti. Ayrıca elle betikle: daraltılmış notta F2, Ctrl+Z ile liste devamını geri alma, Ayarlar ve Explorer ekran görüntüsü.

## Açık kalanlar / kısıtlar
- Madde imi, onay kutusu, başlık boyutu gibi görsel Markdown çizimi yok; onay kutusu tıklanmaz, `[x]` elle yazılır.
- Numaralı listede araya madde eklenince sonraki numaralar yeniden sıralanmaz.
- Gerçek Memos sunucusuyla canlı test yapılmadı (bu makinede Docker yok); eşitleme sahte sunucuyla test edildi.
- Eski UI betikleri (`tek149*`, `tek150*`, `tek151*`) liste modu ya da ilk satır başlığı varsayıyor olabilir; güncellenmedi ve koşulmadı.
- Depodaki `StickyNotes.exe` yeniden derlenmedi; yeni sürüm Release'teki kurulum dosyasıdır.
