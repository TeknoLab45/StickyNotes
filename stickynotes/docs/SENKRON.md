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
| Token ayarları sayfası (web) | `/setting#access-token` (kaynak: `web/src/router/routes.ts` `SETTING: "/setting"`, `web/src/components/Settings/settingSections.ts` anahtarı `access-token`) | aynı `/setting#access-token` | "Token al" bağlantısının yolu; sürüme bağlı değil |

Sürümler arasında alan adı farkı görülmedi; DTO tek şema. Farklı bir sürüm çıkarsa DTO'da sürüm eşlemesi gerekir.

Token sayfası iki sürümde de aynı yol: `/setting#access-token`. Yol kaynak koddan doğrulandı (tag `v0.31.0` ve `26.11.1`); tarayıcıda elle bakılmadı. Yol sürüme bağlı olmadığı için kök algılamaya göre seçim gerekmez.

Space rolü: düzenleme yetkisi Space rolüne bağlı. `ADMIN` (ve `HOST`) rolü başkasının memosunu da düzenleyebilir; `USER` yalnız kendi memosunu düzenler. Plandaki "sunucu yöneticisi" ifadesi bu Space rolüne karşılık gelir. 0.31'de doğrulandı; 26.x'te yalnızca üyenin PATCH'inin 403 dönmesi doğrulandı.

### Test kapsamı (plan G2 listesi)

Otomatik testte olanlar (`SyncEngineTests.cs`, `SyncScenarioTests.cs`):
- İlk eşitleme iki yönde; uzak değişim; yerel değişim; çakışma (tek kopya, sonraki turda yok).
- Yerel çöp → ARCHIVED; uzak ARCHIVED → yerel çöp; web'de geri alma (ARCHIVED → NORMAL, iki tur, sunucuda NORMAL kalır, yerelde çöpte değil).
- Kalıcı silme sunucuya istek göndermez.
- Space 404 → duraklatma, DB bayt bayt aynı.
- Sigorta (10 eşleme, boş liste); tek memo kayıp + GetMemo 404 → çöp; GetMemo 200 → dokunulmaz.
- Space değişimi ve dönüş: eski alanın eşlemeleri yeni alana POST edilmez, eşlenmemiş notlar yeni alana gider, dönünce eski eşlemeler çalışır.
- İki PC aynı UUID: tek memo, yineleme yok, kopya yok.
- Uzun çevrimdışı: iki tarafta yeni / değişmiş / çöpe atılmış notlar. Tek turda tek çakışma kopyası; sonraki tur 0 değişiklik; iki taraf aynı küme.
- DB yeniden kurulumu (boş DB, dolu sunucu): kopya yok, yinelenen memo yok.
- Kesilen tur (kaldığı yerden devam), 409 ile eşleme, 401 → duraklatma, ağ hatası → Offline.
- Salt okunur: başkasının notunu yerelde düzenleme → kopya, asıl not sunucu içeriği; salt okunur çöp yalnızca yerelde gizler, sunucuya gitmez; admin düzenleyebilir.
- 403 (PATCH) → salt okunura geçiş ve kopya; 403 (POST) → not yerelde kalır, diğerleri gönderilir.

Açık kalan:
- Arayüz (G3) kodlandı (TEK-168): `SyncService`, `TokenStore` (DPAPI), Ayarlar'da Memos bölümü, salt okunur not, tray. Windows'ta çalıştırılmadı; `tools/ui-test/tek166.ps1` bekliyor.
- exe (G4): WinVMAgent işi.
- Canlı Memos testi yalnızca `MEMOS_TEST_URL`, `MEMOS_TEST_TOKEN`, `MEMOS_TEST_SPACE` tanımlıysa çalışır; yoksa `Skipped` görünür (`Xunit.SkippableFact`).

## Testleri çalıştırma

```bash
export PATH=$HOME/.dotnet:$PATH          # .NET 10 SDK
cd stickynotes
dotnet test tests/StickyNotes.Core.Tests/StickyNotes.Core.Tests.csproj
```

Canlı sunucu testi (`MemosLiveTests`) yalnızca üç ortam değişkeni tanımlıyken çalışır; yoksa **Skipped** görünür:

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
- Kayıp memo 404 ile çöpe alındıktan sonra not zaten çöpteyse `GetMemo` tekrar çağrılmaz; eşleme satırı korunur.
- Uygulama sunucuya tek turda bağlanır; iki eşzamanlı tur `SyncEngine` içinde sıraya girer, ama iki ayrı süreç arasında kilit yoktur (App katmanı tek örnek garanti etmeli).
