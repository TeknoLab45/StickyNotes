# Metin biçimlendirme (kalın / altı çizili / üstü çizili)

Durum: **Core katmanı (TEK-154) ve WPF katmanı (TEK-155, Görev B) kodlandı.** WPF tarafı Linux'ta derlenmez/çalışmaz; derleme yalnızca `-p:EnableWindowsTargeting=true` ile doğrulandı. Windows'ta `tools/ui-test/tek152.ps1` ve plan §7 elle testleri henüz çalıştırılmadı. Kaynak planı: TEK-153 (`BICIMLENDIRME-PLANI.md`, ArchitectAgent).

## 1. Kayıt biçimi

Metin notunun `notes.content` alanı:

```json
{"v":1,"text":"Toplantı yarın saat 10","marks":[{"t":"b","s":0,"e":8},{"t":"u","s":15,"e":22}]}
```

- `t`: `"b"` kalın, `"u"` altı çizili, `"s"` üstü çizili.
- `s` / `e`: `text` içinde **UTF-16 kod birimi** ofseti; aralık `[s, e)` yarı açık.
  JavaScript `String` indeksleriyle aynıdır, sunucu/web tarafında ek dönüşüm gerekmez.
- `text` satır sonu olarak yalnızca `\n` içermeli. Biçim ofsetleri bu metne göre hesaplanır; çağıran taraf `\r\n` göndermemeli (WPF tarafı `RichText.NormalizeNewlines` ile çevirir).
- `marks` alanı isteğe bağlıdır. Biçim yoksa alan **hiç yazılmaz**; biçimsiz notun JSON'u TEK-151 sürümüyle bayt bayt aynıdır (`{"v":1,"text":"..."}`), böylece gereksiz `edited_at` güncellemesi ve sync çakışması olmaz.
- `v` 1 kalır. Alan ekleme yoluyla geriye uyumludur; eski kod `v`'yi kontrol etmez.

### Normalizasyon (kayıttan önce her zaman)
`RichText.Normalize(marks, textLength)`:
1. Bilinmeyen `t` türleri atılır.
2. Aralık `[0, textLength]`'e kırpılır; boş ya da ters aralıklar atılır.
3. Aynı türde çakışan/bitişik aralıklar birleştirilir.
4. `(t, s)` sırasına konur (tür ordinal sıralı).

Farklı türler aynı karakterde çakışabilir (kalın + altı çizili + üstü çizili birlikte olabilir).

### Okuma (`NoteContent.ParseMarks`)
- `marks` yok, dizi değil, ya da bir elemanda `t` string değil / `s`,`e` tam sayı değilse o eleman atlanır.
- Kök nesne değilse, JSON bozuksa boş liste döner. **Exception fırlatmaz.**
- Sonuç metin uzunluğuna göre normalize edilir.

## 2. Core API (`src/StickyNotes.Core/RichText.cs`, `NoteContent.cs`)

| Fonksiyon | Davranış |
|---|---|
| `MarkTypes.Bold/Underline/Strike` | `"b"`, `"u"`, `"s"` |
| `record Mark(string Type, int Start, int End)` | `[Start, End)` aralığı |
| `RichText.Normalize(marks, textLength)` | Yukarıdaki normalizasyon |
| `RichText.Toggle(marks, type, start, end, textLength)` | Aralık tamamen o türle kaplıysa türü çıkarır (gerekirse böler), değilse ekler. Boş aralıkta girdi normalize edilip döner. Diğer türlere dokunmaz. |
| `RichText.IsFullyMarked(marks, type, start, end)` | Tamamen kaplı mı; boş aralık için `false` |
| `RichText.TypesAt(marks, index)` | `index` karakterini kaplayan türler (`e` hariç) |
| `RichText.NormalizeNewlines(text)` | `\r\n` ve `\r` → `\n` |
| `NoteContent.CreateText(text, marks = null)` | Marks boşsa eski çıktı; doluysa `marks` alanı eklenir |
| `NoteContent.ParseMarks(content)` | Yukarıdaki okuma kuralları |

`ParseText`, `FirstLine`, `Preview`, `PlainText` yalnızca `text`'i kullanır; biçim arama, önizleme ve başlıkta görünmez.

## 2b. WPF katmanı (`src/StickyNotes.App`)

- `RichTextMapper.cs`: `Write(doc, text, marks)`, `Read(doc)`, `GetOffset(doc, pointer)`, `GetPointer(doc, offset)`. Biçim yalnızca **yerel** değerlerden okunur (`ReadLocalValue`: `FontWeight ≥ 700`, `TextDecorations` Underline/Strikethrough); ayarlardan miras gelen yazı stili biçim sayılmaz. Paragraf geçişi 1 karakter (`
`).
- `NoteViewModel`: `Marks`, `SetRich(text, marks)`, `BuildContent` → `CreateText(Text, Marks)`. Düzenleyiciden gelen değişiklik, yalnızca CRLF→LF farkı ve biçim yoksa değişiklik sayılmaz (`edited_at` değişmez).
- Ctrl+B = kalın, Ctrl+U = altı çizili. Üstü çizili yalnızca sağ tık menüsünde. Biçim komutu `RichText.Toggle` ile hesaplanır (WPF `ToggleBold/ToggleUnderline` kullanılmaz: üstü çiziliyi siler ve Ayarlar'daki kalın stili ile çelişir).
- Sağ tık menüsü: Kalın, Altı çizili, Üstü çizili, ayraç, Kes, Kopyala, Yapıştır. Seçim yoksa biçim maddeleri devre dışı.
- Yapıştırma her zaman düz metin (resim/dosya yapıştırılmaz). Biçimli metin kopyalanıp yapıştırılınca yalnızca düz metin gelir.
- Engellenen RichTextBox komutları (italik, yazı boyutu, hizalama, liste, girinti, alt/üst simge): `CanExecute=false`. Ctrl+N, Ctrl+F, Alt+X pencere kısayolları çalışmaya devam eder.
- Checklist'e dönüşümde biçim düşer; checklist'ten metne dönüşte düzenleyici `Vm.Text`/`Vm.Marks` ile yeniden yüklenir.

## 3. Geriye uyum

- **Eski not, yeni sürüm:** `marks` yok → biçimsiz açılır. Eski `TextBox` `\r\n` yazmış olabilir; okuma `\r\n|\n|\r` ile böler. Açmak kaydetmeye yol açmaz (yükleme sırasında geri yazım bastırılır — WPF görevi).
- **Yeni not, eski sürüm:** eski `ParseText` yalnızca `text`'i okur → not düz metin olarak sorunsuz açılır, bozulmaz. Eski sürümde düzenlenip kaydedilirse `marks` düşer (biçim kaybolur, metin korunur). Tüm PC'ler aynı sürüme güncellenmeli.
- **Bilinmeyen tür** (ileride italik vb.): bu sürümde okunurken atılır.
- **Metin → checklist:** `marks` düşer (checklist biçimi kapsam dışı). **Checklist → metin:** biçimsiz metin.

## 4. Senkronizasyon (TEK-146 için not)

- `notes.content` hâlâ tek JSON dizesi; şema değişmedi, yeni tablo yok.
- Sunucu ve web istemcisi `text`'i düz metin olarak her zaman gösterebilir. Biçim göstermek isterse `marks`'ı aynı kurallarla `<b>/<u>/<s>`'ye çevirir.
- Kural: `marks`'ı anlamayan istemci metni değiştirirse `marks`'ı silmeli; anlamayan ama metni değiştirmeyen istemci alanı olduğu gibi korumalı. (Aksi halde ofsetler kayar.)

## 5. Test

- Komut: `dotnet build tests/StickyNotes.Core.Tests` ve `dotnet test tests/StickyNotes.Core.Tests` (.NET 10 SDK; Windows gerekmez).
- Birim testleri: `tests/StickyNotes.Core.Tests/RichTextTests.cs` — Normalize, Toggle, IsFullyMarked/TypesAt sınırları, NoteContent round-trip, eski içerik, bozuk marks, PlainText/FirstLine/Preview, ToChecklist, Türkçe + emoji UTF-16 ofsetleri, NoteRepository kaydet-yeniden aç-ara.

## 6. Açık kalanlar

- Windows'ta doğrulanmadı: `RichTextMapper` ofset eşlemesi (paragraf sınırı, emoji), Ctrl+Z (R2), yapıştırma olayının `DataObject` ataması, RichTextBox'ın `IsDocumentEnabled="False"` ile davranışı, menü ayracının görünümü. Plan §7 elle testleri ve `tools/ui-test/tek152.ps1` çalıştırılmalı.
- Eski `TextBox` tabanlı UI testleri (`tek149`, `tek149b`, `tek151a`, `tek151d`) artık `Set-Editor` (ui.ps1) ile yazar; `ValuePattern` RichTextBox'ta yoktur.
- Seçim yokken biçim (yazmaya devam et) desteklenmez; sonraki tur.
- Checklist maddelerinde biçim: ilk tur dışı.
