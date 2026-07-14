# Medium Makalesi — Mermaid Diyagramları

[medium-mimari-makale.md](medium-mimari-makale.md) içindeki ASCII şemaların Mermaid karşılıkları.

**Kullanım:** GitHub bu blokları doğrudan render eder. Medium mermaid desteklemediği için
her bloğu [mermaid.live](https://mermaid.live) sitesine yapıştırıp PNG/SVG olarak dışa aktarın
ve makaleye görsel olarak gömün (`mmdc` CLI ile toplu export da mümkün:
`mmdc -i medium-mimari-diagramlar.md -o cikti.png`).

---

## 1. Büyük Resim

```mermaid
flowchart TB
    Client["Client<br/>(SPA / K6)"]

    subgraph Host["ModularCommerce.Host — tek süreç"]
        direction TB
        MW["Middleware Pipeline<br/>CorrelationId → Serilog → ExceptionHandler → Auth → RateLimiter"]
        subgraph Modules["8 Modül"]
            direction LR
            ID[Identity]
            CAT[Catalog]
            CRT[Cart]
            INV[Inventory]
            ORD[Ordering]
            PAY[Payment]
            SHP[Shipping]
            NOT[Notification]
        end
        MW --> Modules
    end

    Client -- "HTTPS / JSON + ProblemDetails" --> MW

    PG[("PostgreSQL<br/>şema/modül: identity, catalog,<br/>ordering, payment, inventory, notification")]
    RD[("Redis<br/>cart + dağıtık kilit + catalog cache")]
    MQ[["RabbitMQ / MassTransit<br/>OrderPaid, OrderCancelled"]]

    Modules --> PG
    Modules --> RD
    Modules --> MQ
```

---

## 2. Modül Katmanları ve Referans Yönü

```mermaid
flowchart LR
    Api["Api<br/>(IModule + endpoint'ler)"] --> Infra["Infrastructure<br/>(EF Core, Redis, adaptörler)"]
    Infra --> App["Application<br/>(use case handler'ları)"]
    App --> Domain["Domain<br/>(saf iş kuralları)"]
    App --> Contracts["Contracts<br/>(DIŞ DÜNYAYA TEK KAPI)"]
    Domain --> SK["Shared.Kernel<br/>(Result, Error, Money, Entity)"]
    Contracts --> SK
```

---

## 3. Modüller Arası Konuşma: Ordering → Cart Örneği

```mermaid
flowchart TB
    subgraph ORD["Ordering — tüketici"]
        CH["CheckoutHandler<br/>ctor(ICartService, IProductReader,<br/>IStockReservationService, IPaymentService)"]
    end

    subgraph CC["Cart.Contracts — SINIR BURADA"]
        ICS["ICartService<br/>+ CartLineDto(ProductId, Quantity)"]
    end

    subgraph CRT["Cart — sahip"]
        CS["CartService — adaptör"]
        CM["CartModule.Register<br/>AddScoped(ICartService → CartService)"]
    end

    CH -- "yalnız arayüzü bilir" --> ICS
    CS -. "implements" .-> ICS
    CM -. "DI kaydı" .-> CS
```

---

## 4. Sınır İstisnası: Ters Yön Bağımlılığı (Döngüsüz)

```mermaid
flowchart LR
    OA["Ordering.Application"] -- "checkout:<br/>rezerve et" --> IC["Inventory.Contracts"]
    II["Inventory.Infrastructure"] -- "TTL süpürücüsü:<br/>sipariş ödendi mi?" --> OC["Ordering.Contracts"]
    IC --> SK["Shared.Kernel"]
    OC --> SK

    style OC stroke-dasharray: 5 5
```

Contracts projeleri geriye hiçbir modüle referans vermez → graf **asiklik** kalır.

---

## 5. Middleware Boru Hattı

```mermaid
flowchart TD
    A["İstek"] --> B["CorrelationId<br/>(X-Correlation-Id üret/taşı)"]
    B --> C["Serilog request log"]
    C --> D["ExceptionHandler<br/>(beklenmeyen hata → 500 ProblemDetails)"]
    D --> E["Authentication (JWT)"]
    E --> F["Authorization"]
    F --> G["RateLimiter<br/>(global + auth/checkout policy)"]
    G --> H["Endpoint"]
```

---

## 6. Kimlik: Signup → Login

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant ID as Identity Modülü
    participant DB as identity şeması

    C->>ID: POST /api/identity/signup
    ID->>DB: INSERT users
    Note over DB: unique index = e-posta yarışının<br/>GERÇEK hakemi
    alt 23505 — yarışı kaybetti
        ID-->>C: 409 EmailAlreadyExists
    else başarılı
        ID-->>C: 201 Created
    end

    C->>ID: POST /api/identity/login
    ID->>DB: şifre doğrula + refresh token yaz
    ID-->>C: accessToken (15 dk) + refreshToken (7 gün, ROTASYONLU)
```

---

## 7. Catalog: Cache'li Okuma (Decorator)

```mermaid
flowchart TD
    A["GET /api/catalog/products/:id"] --> B["CachingProductQueries<br/>(Decorator)"]
    B --> C{"Redis GET<br/>catalog:product::id"}
    C -- "HIT" --> OK["200 ProductDetail<br/>(DB'ye HİÇ gidilmez)"]
    C -- "MISS veya Redis DOWN<br/>(graceful degrade)" --> D["EF Core sorgusu<br/>(ProductQueries)"]
    D --> E["Redis SET — TTL 60s<br/>(best-effort)"]
    E --> OK
```

---

## 8. Sepet: Redis-Only

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant CR as Cart Modülü
    participant CAT as Catalog (IProductReader)
    participant R as Redis

    C->>CR: POST /api/cart/items {productId, quantity}
    CR->>CAT: ürün snapshot (Contracts üzerinden)
    CAT-->>CR: ProductSnapshotDto
    CR->>R: SET cart:{userId} — TTL 7 gün, her yazmada kayar
    CR-->>C: 200 sepet
    Note over R: DbContext YOK — sepet AP tarafında<br/>(kaybolabilir veri), sipariş/ödeme CP
```

---

## 9. Checkout (Ana Olay)

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant O as Ordering
    participant CA as Cart
    participant CT as Catalog
    participant I as Inventory
    participant P as Payment

    C->>O: POST /api/ordering/checkout<br/>Idempotency-Key: k1 (ZORUNLU)
    O->>O: (a) k1 daha önce işlendi mi?
    alt k1 mevcut
        O-->>C: 200 — mevcut siparişin KOPYASI
    else yeni istek
        O->>CA: (b) GetItemsAsync
        CA-->>O: sepet satırları
        O->>CT: (c) fiyat/ad snapshot (batch)
        CT-->>O: ProductSnapshotDto[]
        loop her satır
            O->>I: (d) ReserveAsync → Reserved += q
        end
        Note over O,I: rezervasyon başarısızsa öncekiler<br/>Release edilir → 409
        O->>O: (e) Created → StockReserved → PaymentPending
        O->>P: (e2) ChargeAsync(k1)
        Note over P: 1. HAKEM: payments unique index<br/>aynı k1 ASLA iki kez charge edilemez<br/>(içeride Polly boru hattı)
        alt ödeme reddedildi
            O->>I: rezervasyonları Release
            O-->>C: 409 Payment.Declined — sipariş HİÇ yazılmadı
        else ödeme başarılı
            O->>O: (f) Order'ı Paid persist et<br/>+ OrderPaid outbox AYNI transaction'da
            Note over O: 2. HAKEM: orders unique index (customer+k1)<br/>yarışı kaybeden → kazananın kopyası
            O->>I: (f2) Commit → OnHand -= q, Reserved -= q
            O->>CA: (g) sepeti temizle (best-effort)
            O-->>C: 201 Created
        end
    end
```

---

## 10. Payment İçi Dayanıklılık: Polly Boru Hattı

```mermaid
flowchart LR
    A["ChargeAsync"] --> B["Toplam timeout<br/>3 sn"]
    B --> C["Retry + jitter<br/>(geçici hatada)"]
    C --> D["Circuit breaker<br/>(PSP hasta → devre açık)"]
    D --> E["Bulkhead<br/>(eşzamanlılık sınırı)"]
    E --> F["Deneme-başı timeout<br/>1 sn"]
    F --> G["FakePspClient"]
```

---

## 11. Asenkron Akış: Outbox → RabbitMQ → Idempotent Inbox → DLQ

```mermaid
sequenceDiagram
    autonumber
    participant O as Ordering<br/>(checkout tx)
    participant OB as ordering.outbox_messages
    participant D as OutboxDispatcher<br/>(~1s poll)
    participant MQ as RabbitMQ
    participant N as OrderPaidNotification<br/>Consumer
    participant NDB as notification şeması

    O->>OB: OrderPaid satırı — sipariş ile AYNI transaction (atomik)
    loop her ~1 sn
        D->>OB: pending satırları oku
        D->>MQ: Publish(OrderPaid)
        D->>OB: ProcessedOnUtc işaretle
    end
    Note over D,MQ: en-az-bir-kez: crash olursa<br/>aynı mesaj TEKRAR yayınlanır
    MQ->>N: consume
    N->>NDB: processed_messages'a yaz<br/>PK = ("OrderPaid:{OrderId}", ConsumerType)
    Note over N,NDB: iş anahtarı — MassTransit MessageId DEĞİL<br/>(el yapımı outbox her yayında yeni id basar)
    alt PK çakışması (23505)
        N-->>MQ: idempotent SKIP — kopya
    else ilk işleme
        N->>N: e-posta + webhook kanalları<br/>(Strategy + FaultInjecting Decorator)
        N->>NDB: notification_logs + marker<br/>TEK SaveChanges (Transactional Inbox)
    end
    Note over MQ,N: 3 deneme de başarısız →<br/>order-paid-notification_error (DLQ)
```

---

## 12. İptal ve Telafi

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant O as Ordering
    participant I as Inventory
    participant P as Payment

    C->>O: POST /api/ordering/orders/{id}/cancel
    O->>O: Order.Cancel — matris: Paid → Cancelled
    loop her satır
        O->>I: ReturnAsync → OnHand += q (best-effort)
    end
    O->>P: RefundAsync
    alt refund BAŞARISIZ
        O-->>C: hata — iptal PERSIST EDİLMEZ,<br/>sipariş Paid kalır
    else refund başarılı
        O->>O: Cancelled + OrderCancelled outbox<br/>TEK transaction'da
        O-->>C: 204 No Content
    end
    Note over O,P: sıralama bilinçli: para iade edilemeyecekse<br/>"iptal edildi" göstermek yalan olur
```

---

## 13. TTL Süpürücüsü (Self-Healing)

```mermaid
flowchart TD
    S["ReservationTtlSweeper<br/>(BackgroundService, 30s poll)"] --> Q["Süresi dolmuş Active<br/>rezervasyonları bul"]
    Q --> R{"IOrderReservationReconciler:<br/>bu rezervasyonun siparişi Paid mi?"}
    R -- "evet (ödendi)" --> CM["Commit<br/>Available değişmez → oversell İMKANSIZ"]
    R -- "sahipsiz" --> EX["Expire<br/>Reserved -= q → stok tekrar satılabilir"]
    R -- "sınıflandırma hatası" --> NX["Batch'e dokunma<br/>(bir sonraki turda tekrar)"]
```

---

## 14. Order Durum Makinesi

```mermaid
stateDiagram-v2
    [*] --> Created : checkout başladı
    Created --> StockReserved : rezervasyonlar alındı
    Created --> Cancelled
    StockReserved --> PaymentPending : ödemeye geçiliyor
    StockReserved --> Cancelled
    StockReserved --> Expired : TTL doldu
    PaymentPending --> Paid : charge başarılı
    PaymentPending --> Cancelled
    PaymentPending --> Expired : TTL doldu
    Paid --> Shipped
    Paid --> Cancelled : kapsamlı iptal (refund + stok iade)
    Shipped --> [*]
    Cancelled --> [*]
    Expired --> [*]
```

---

## 15. Aggregate Yapıları

```mermaid
classDiagram
    class Order {
        <<aggregate root>>
        +CustomerId
        +Status
        +IdempotencyKey
        +TotalAmount: Money (türetilmiş)
        +MarkPaid() Result
        +Cancel() Result
    }
    class OrderLine {
        <<owned>>
        +ProductId
        +ProductName (snapshot)
        +UnitPrice: Money (snapshot)
        +Quantity
        +ReservationId
    }
    class OrderStatusChange {
        <<owned — audit>>
        +FromStatus
        +ToStatus
        +TriggeredBy
        +OccurredAtUtc
    }
    class Payment {
        <<aggregate root>>
        +OrderId
        +Amount: Money
        +IdempotencyKey (unique)
        +Status
        +Complete() Result
        +Refund() Result
    }
    class PaymentAttempt {
        <<owned — append-only>>
        +Outcome
        +PspTransactionId
    }
    class StockItem {
        <<aggregate root>>
        +ProductId
        +OnHand
        +Reserved
        +Reserve() Result
        +Commit() Result
        +Release() Result
    }
    class Reservation {
        <<owned>>
        +Quantity
        +Status: Active/Committed/Released/Expired/Returned
    }
    class Money {
        <<value object — Shared.Kernel>>
        +Amount: decimal
        +Currency: string
        +Add(Money) Money
        +Multiply(int) Money
    }

    Order "1" *-- "1..*" OrderLine
    Order "1" *-- "1..*" OrderStatusChange
    Payment "1" *-- "0..*" PaymentAttempt
    StockItem "1" *-- "0..*" Reservation
    OrderLine --> Money
    Payment --> Money
```
