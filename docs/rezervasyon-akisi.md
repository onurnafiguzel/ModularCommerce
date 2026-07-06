# Rezervasyon Akış Diyagramları — Uçtan Uca (Hafta 3 durumu + gelecek haftalar)

> Sistem tasarımı anlatısı: bir rezervasyon isteğinin Client'tan PostgreSQL'e yolculuğu,
> race condition'ın nerede doğduğu ve hangi katmanda çözüldüğü.
> Kesikli oklar henüz gelmemiş parçalardır (Redis: Hafta 4, RabbitMQ/outbox: Hafta 7).

## 1. Büyük resim — topoloji

```mermaid
flowchart LR
    C[Client / K6] --> HOST["ModularCommerce.Host<br/>(tek deployable, ASP.NET pipeline:<br/>ExceptionHandler + Problem Details)"]

    HOST --> INV["Inventory modülü<br/>(CP — kesin tutarlılık)"]
    HOST --> CAT["Catalog modülü<br/>(AP — bayat veri kabul)"]

    INV --> PGI[("PostgreSQL<br/>inventory şeması<br/>stock_items + reservations")]
    CAT --> PGC[("PostgreSQL<br/>catalog şeması<br/>products")]

    INV -. "Hafta 4: SET NX PX<br/>distributed lock (3. strateji)" .-> R[("Redis")]
    CAT -. "Hafta 4: cache-aside<br/>TTL 30sn + invalidation" .-> R

    INV -. "Hafta 7: outbox →<br/>ProductSoldOut / StockReserved" .-> MQ[("RabbitMQ<br/>(MassTransit)")]
    MQ -. "Catalog rozet günceller<br/>(yaklaşık stok, FR-2.4)" .-> CAT
```

Sınır kuralı: modüller birbirinin şemasına SQL atamaz; Inventory stok gerçeğinin tek sahibi,
Catalog'daki stok yalnızca event'le beslenen yaklaşık kopya olacak.

## 2. Rezervasyon isteği — uçtan uca sekans (bugünkü kod)

```mermaid
sequenceDiagram
    autonumber
    actor C as Client<br/>(N eşzamanlı istek)
    participant MW as Host Pipeline<br/>GlobalExceptionHandler
    participant EP as Inventory.Api<br/>POST /api/inventory/reservations
    participant H as ReserveStockHandler<br/>(Application — orkestrasyon)
    participant S as IReservationStrategy<br/>(config: Naive | Optimistic)
    participant D as StockItem<br/>(Domain — iş kuralları)
    participant PG as PostgreSQL<br/>inventory.stock_items

    C->>MW: POST {productId, quantity}
    MW->>EP: routing
    EP->>H: HandleAsync(command)

    Note over H: FluentValidation — YALNIZ biçim:<br/>productId boş mu, quantity ≥ 1 mi
    alt biçim geçersiz
        H-->>C: 400 Problem Details<br/>Inventory.Reservations.InvalidCommand
    end

    H->>S: ReserveAsync(productId, qty)
    S->>PG: SELECT stock_item<br/>(Optimistic: TRACKED + xmin token okunur)
    S->>D: stockItem.Reserve(qty)

    Note over D: İŞ KURALI TEK YERDE (DDD):<br/>qty ≤ Available? → Reserved += qty<br/>Available == 0 → ProductSoldOut raise<br/>(dispatch Hafta 7 outbox)

    alt yetersiz stok
        D-->>C: 409 Inventory.InsufficientStock<br/>(TERMİNAL — retry anlamsız)
    end

    S->>PG: UPDATE stock_items SET Reserved = @r<br/>WHERE Id = @id AND xmin = @token
    Note over S,PG: RACE CONDITION ÇÖZÜMÜ BURADA:<br/>xmin (satır versiyonu) WHERE koşulunda —<br/>araya giren transaction xmin'i değiştirir

    alt yarışı kaybetti (0 satır etkilendi)
        PG-->>S: DbUpdateConcurrencyException
        S-->>C: 409 Inventory.ConcurrencyConflict<br/>(RETRYABLE — "tekrar deneyin")
        Note over C: Sunucu retry YAPMAZ (NFR-3.3/3.4)<br/>Retry İSTEMCİNİN kararı →<br/>10 stokta TAM 10 başarı
    else yarışı kazandı (1 satır)
        PG-->>S: UPDATE ok + INSERT reservation<br/>(aynı transaction, TTL = +5 dk)
        S-->>C: 201 Created + Location<br/>{reservationId, status: Active, expiresAtUtc}
    end
```

## 3. Yarışın anatomisi — Naive neden oversell yapar

Klasik **check-then-act** hatası: kontrol ile yazma arasında dünya değişir.

```mermaid
sequenceDiagram
    participant A as İstek A
    participant B as İstek B
    participant PG as PostgreSQL<br/>(OnHand=10, Reserved=9 → Available=1)

    par aynı anda
        A->>PG: SELECT (AsNoTracking) → Available=1 ✓ kontrolden geçti
    and
        B->>PG: SELECT (AsNoTracking) → Available=1 ✓ kontrolden geçti
    end

    Note over A,B: İkisi de AYNI bayat değeri okudu —<br/>domain kuralı doğru ama bayat veriyle çalışıyor

    A->>PG: UPDATE Reserved = Reserved + 1 (koşulsuz, xmin YOK)
    B->>PG: UPDATE Reserved = Reserved + 1 (koşulsuz, xmin YOK)

    Note over PG: Reserved=11 > OnHand=10 → OVERSELL<br/>Ölçüm (100 paralel istek): Naive → oversell 90,<br/>Optimistic (xmin) → oversell 0, tam 10 başarı
```

## Çözüm katmanları özeti (mülakat cevabı)

| Katman | Ne yapar | Neden tek başına yetmez |
|---|---|---|
| 1. Domain invariant (`StockItem.Reserve`) | `qty ≤ Available` kuralı — her stratejide çalışır | Bayat snapshot'a karşı çalışırsa yarışı GÖREMEZ (naive'in dersi) |
| 2. **Optimistic concurrency (xmin)** ← Hafta 3 | `UPDATE ... WHERE xmin=@token`; kaybeden 409 alır | Çakışma oranı aşırı yükselirse retry trafiği büyür |
| 3. Redis distributed lock ← Hafta 4 | `SET key NX PX` ile ürün başına kilit; yarış hiç başlamaz | Ek altyapı + kilit bekleme gecikmesi; ölçümle karşılaştırılacak |
| İlke (NFR-3.4) | Belirsizlikte REDDET, asla iyimser onay verme; sunucu retry yok, retry istemcinin | — |

İki 409'un ayrımı istemci sözleşmesinin parçası: `Inventory.ConcurrencyConflict` = tekrar dene,
`Inventory.InsufficientStock` = dur, stok bitti (ProblemDetails `title` alanından ayırt edilir).
