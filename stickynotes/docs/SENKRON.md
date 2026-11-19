# Memos eşitlemesi: Core (G1 + G2)

Bu dosya TEK-167 kapsamını anlatır: StickyNotes ile Memos sunucusu arasında Space tabanlı iki yönlü eşitlemenin **Core** katmanı (`src/StickyNotes.Core/Sync/`) ve Docker'da gerçek Memos sürümleriyle yapılan doğrulama. Arayüz (Ayarlar, tray, token saklama), exe yayını ve Windows testleri bu kapsamda değil (G3–G4).

## Ne yapıldı

- **Markdown dönüşümü** (`MemoMarkdown.cs`): kalın → `**x**`, üstü çizili → `~~x~~`, altı çizili sunucuya gitmez. Checklist → `- [ ] / - [x]`, girinti 2 boşluk. Yabancı markdown düz metin olarak korunur. Parmak izi (`Fingerprint`): sondaki boşluk yok sayılır, çöp durumu yalnızca düzenlenebilir notlarda sayılır.
- **İstemci** (`MemosClient.cs`, `IMemosApi`): kök algılama (`/api` → `/api/v1`), Bearer PAT, sayfalı listeleme, hata türleri (`Unauthorized`, `Forbidden`, `NotFound` → null, `AlreadyExists`, `Offline`, `Version`). Token hiçbir hata metnine yazılmaz.
- **Ayarlar** (`SyncSettings.cs`): `memos_*` anahtarları `settings` tablosunda (bu PC'ye özel). Token DPAPI ile App'te şifrelenir; Core yalnızca `memos_token_enc` metnini taşır.
- **Eşleme tablosu** `sync_state` (`NoteRepository.cs`): not kimliği ↔ memo adı, Space, yazar, salt okunur bayrağı, son eşitlenen içerik parmak izi. Eski veritabanları için `CREATE TABLE IF NOT EXISTS`, veri kaybı yok.
- **Eşitleme turu** (`SyncEngine.cs`), sırayla:
  1. Ön kontrol: Space yoksa (404) tur `Paused` biter ve **hiçbir yazma yapılmaz**.
  2. Sigorta ve kayıp memolar: eşlenmiş notların hem 5'ten fazlası hem %30'u sunucuda yoksa tur duraklar. Tek tek kayıp memo ancak `GetMemo` 404 dönerse çöpe taşınır.
  3. Uzak memolar: yeni olanlar yerel nota çevrilir (32 hex kimlik → aynı UUID). Eşlenmişler için taban parmak izine göre: yalnızca uzak değişmiş → uygula; yalnızca yerel değişmiş → PATCH (salt okunursa kopya); ikisi de değişmiş → yerel kazanır, sunucu sürümü `(çakışma kopyası)` olarak yeni not olur.
  4. Eşlemesi olmayan yerel notlar `POST ?memoId=<uuid tiresiz>` ile gönderilir. 409 gelirse memo bizimse eşlenir, değilse yeni kimlikle tekrar denenir.
  - Her başarılı uzak işlemden hemen sonra `sync_state` yazılır: yarıda kesilen tur kaldığı yerden devam eder.
  - Kalıcı silme (`Purge`) sunucuya hiçbir istek göndermez.
- **Testler**: `tests/StickyNotes.Core.Tests/` — `MemoMarkdownTests.cs`, `MemosClientTests.cs`, `SyncEngineTests.cs` (sahte `IMemosApi` ile senaryolar), `MemosLiveTests.cs` (canlı sunucu, env ile).

## Docker'da doğrulama (G1)

Test sunucuları (yerel, `localhost`):

| Sürüm | Port | Görüntü |
|---|---|---|
| 0.31.0 | 5231 | `neosmemo/memos:0.31.0` |
| 26.11.1 | 5232 | `neosmemo/memos:26.11.1` |

`neosmemo/memos:0.31.1` ve `0.31.2` Docker Hub'da yok; en düşük desteklenen 0.31 olarak **0.31.0** test edildi.

### Bulgular

| Konu | 0.31.0 | 26.11.1 | Kod karşılığı |
|---|---|---|---|
| Profil (kök algılama) | `/api/instance/profile` 404, `/api/v1/instance/profile` 200 | `/api/instance/profile` 200; `/api/v1/*` 410 | `DetectRootAsync`: önce `/api`, sonra `/api/v1` |
| Kimlik | `GET /api/v1/auth/me` (Bearer PAT) | `GET /api/auth/me` (Bearer PAT) | `GetMeAsync` |
| PAT oluşturma (web değil, API) | REST `POST /api/v1/users/{user}/personalAccessTokens` → `token: memos_pat_…` | Connect `POST /memos.api.UserService/CreatePersonalAccessToken` `{"parent":"users/host"}` | Test kurulumu; istemci PAT alır, oluşturmaz |
| Space listesi / tek Space | `/api/v1/spaces`, `/api/v1/{name}` | `/api/spaces`, `/api/{name}` | `ListSpacesAsync`, `GetSpaceAsync` |
| Space alanları | `name`, `title`, `currentUserRole`, `memberCount` | aynı | `MemosSpace` |
| Memo alanları | `name`, `creator`, `content`, `state`, `visibility`, `space`, `updateTime` | aynı | `ParseMemo` |
| Memo oluşturma | `POST …/memos?memoId=…` body `{content, visibility, space}` | aynı (`/api/memos`) | `CreateMemoAsync` |
| memoId kuralı | 1–36 karakter, harf/rakam/tire, baş-son alfanümerik; 32 hex, 31, 34 kabul; 64 → 400 | aynı kural | 32 hex (tiresiz UUID) kullanılır |
| memoId çakışması | 409 (kod 6, "already exists") | 409 (kod 6) | `AlreadyExists` → eşleme/yeniden kimlik |
| Memo listesi | `state=NORMAL\|ARCHIVED` sorgu parametresi + `filter=space == "spaces/…"` | aynı; `state` çalışıyor | `ListMemosAsync` |
| Memo güncelleme | `PATCH …/memos/{id}?updateMask=content,state` | aynı | `UpdateMemoAsync` |
| Kullanıcı | `GET /api/v1/users/{name}` | `GET /api/users/{name}` | `GetUserAsync` |
| Space silinince | Space `404`, memolar da gider (`GET memo` 404) | aynı | Ön kontrol → Paused |
| Üye olmayan kullanıcı Space'i | **404** `space not found` (403 değil) | 404 | Kayıt yok sayılır → duraklatma |
| Davet | `POST spaces/{id}/invitations` `{"invitee":"users/guest","role":"USER"}`; kabul `POST …/invitations/guest:accept` | aynı (kabul doğrulandı) | Test kurulumu |
| Üye, başkasının memosunu düzenleyemez | **403** `permission denied` | 403 | Salt okunur kuralı |
| Space admini başkasının memosunu düzenler | Evet (PATCH 200, içerik değişti) | Test edilmedi (yalnızca 0.31) | `isSpaceAdmin` (ADMIN) |
| Silinmiş memo, eşleme | `GET` 404 | 404 | `GetMemoAsync` null |

Sürümler arasında alan adı farkı görülmedi; DTO tek şema. Farklı bir sürüm çıkarsa DTO'da sürüm eşlemesi gerekir.

### Açık kalan (doğrulanamadı)

- **Web arayüzündeki token ayarları sayfası yolu**: Docker'daki web arayüzü paketinde doğrudan arandı, bulunamadı (arama zaman aşımına uğradı). Arayüzde "Token al" bağlantısının yolu **doğrulanmadı**. G3'te tarayıcıdan elle doğrulanmalı.
- Space değiştirme (eski Space'in eşlemeleri), iki PC'nin aynı UUID üretmesi, uzun çevrimdışı senaryosu ve veritabanı yeniden kurulumu **otomatik test edilmedi** (plan G2 listesinde). Kısmen kapsanan: çevrimdışı ortada kesilen tur, 409 ile eşleme, salt okunur, 403'ten salt okunura geçiş.
- 403'ün `POST` (oluşturma) tarafı için ayrı test yok; `PATCH` tarafı test edildi.

## Testleri çalıştırma

```bash
export PATH=$HOME/.dotnet:$PATH          # .NET 10 SDK
cd stickynotes
dotnet test tests/StickyNotes.Core.Tests/StickyNotes.Core.Tests.csproj
```

Canlı sunucu testi (`MemosLiveTests`) yalnızca üç ortam değişkeni tanımlıyken çalışır; yoksa **sessizce geçer** (atlanır, sonuç "Passed" görünür):

```bash
MEMOS_TEST_URL=http://localhost:5231 \
MEMOS_TEST_TOKEN=<PAT> \
MEMOS_TEST_SPACE=spaces/<id> \
dotnet test tests/StickyNotes.Core.Tests/StickyNotes.Core.Tests.csproj --filter FullyQualifiedName~MemosLiveTests
```

Token ve adres test çıktısına yazılmaz. Üye olunan bir Space gerekir.

Docker sunucusunu başlatmak için (test verisi, ayrı volume):

```bash
docker run -d --name memos031  -p 5231:5230 -v memos031-data:/var/opt/memos  neosmemo/memos:0.31.0
docker run -d --name memos2611 -p 5232:5230 -v memos2611-data:/var/opt/memos neosmemo/memos:26.11.1
```

## Bilinen kısıtlar

- Markdown'a gitmeyen biçim: altı çizili. Sunucudan gelen `**`/`~~` çiftleri kalın/üstü çizili sayılır; kullanıcının kendi metninde geçen bu çiftler de öyle yorumlanır (kaçış yok).
- Çakışma kopyası, yazma ile kayıt arasında çökme olursa iki kez oluşabilir (kopya, veri kaybı değil).
- Sigorta eşiği (5 ve %30) hem mutlak hem oransal aynı anda aşılırsa tetiklenir. Tek tek kayıp memolar sigortaya takılmaz.
- Uygulama sunucuya tek turda bağlanır; iki eşzamanlı tur `SyncEngine` içinde sıraya girer, ama iki ayrı süreç arasında kilit yoktur (App katmanı tek örnek garanti etmeli).
