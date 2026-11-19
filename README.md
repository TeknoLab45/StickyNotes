# lab-denemeler

> Durum: deneme reposu

Bu repo TeknoLAB'ın denemelik ve prototip işlerinin tek yeridir. Her deneme kendi klasöründe durur; kalıcılaşan projeler ayrı repoya taşınır.

## Klasör kuralı

- Her deneme `deneme-adi/` adıyla kendi klasöründe durur.
- Her deneme klasöründe kısa bir `README.md` bulunur: amaç, nasıl çalıştırılır, durum, tarih ve hangi Multica issue'sundan geldiği.

## Dal kuralı

- Ajanlar `agent/...` dallarında çalışır.
- `main` dalına yalnızca kullanıcının açık talimatıyla merge edilir.

## Terfi kuralı

Bir deneme ciddileşirse geçmişiyle birlikte ayrı repoya taşınır (`git subtree split` veya `git filter-repo`). Bu repoda ilgili klasör silinir ve bu README'ye taşındığı yeri gösteren bir not düşülür.

## Arşiv ve silme kuralı

Terk edilen veya taşınan denemeler, deneme listesinde durumu güncellenerek tutulur. Silme yalnızca açık bir talimatla yapılır.

## Deneme listesi

| Klasör | Amaç | Durum | Tarih | Issue |
|--------|------|-------|-------|-------|
