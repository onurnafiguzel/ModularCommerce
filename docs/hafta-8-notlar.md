# Hafta 8 Karar Notları — Outbox + Event Bus (OrderPaid → RabbitMQ)

> Roadmap Hafta 8 çıktısı: **el yapımı Transactional Outbox** ile `OrderPaid` integration
> event'i RabbitMQ'ya yayınlanır; kanıt event'in RabbitMQ Management UI'da görünmesi + log-only
> consumer'ın tüketmesi. Bu hafta **NFR-5.2** (sipariş durumu ile yayınlanan event'ler ATOMİK —
> Outbox pattern zorunlu) kapatıldı; H6-7'de bilinçli ertelenmişti.

## Mimari akış (tek istek)

```
order.MarkPaid("checkout")
  ├─ OrderStatusChanged(PaymentPending→Paid)   [mevcut, history/iz]
  └─ OrderPaid(...)                            [YENİ domain event — yalnız bu dışa terfi eder]
       ▼
orders.AddAsync → SaveChangesAsync()  ─── TEK transaction ───┐
  DomainEventToOutboxInterceptor:                            │
    ChangeTracker<Entity> → DomainEvents topla               │
    registry.TryMap: OrderPaid✔ / StatusChanged,Created ✘    │
    JSON serialize → outbox_messages satırı Add              │
    entity.ClearDomainEvents()                               │
  ATOMİK COMMIT: orders + outbox_messages birlikte ──────────┘
       ▼ (transaction sınırı)
OutboxDispatcher (BackgroundService, ayrı scope, ~1 sn poll):
  WHERE ProcessedOnUtc IS NULL ORDER BY OccurredOnUtc LIMIT 20
  ResolveType("OrderPaid") → deserialize → Publish(obj, type)
  başarı→ProcessedOnUtc=now / hata→Error+RetryCount++
       ▼                          ▼
  RabbitMQ fanout exchange  ──►  OrderPaidLoggingConsumer → log
```

## SOLID prensipleri (uygulandığı yer)

- **SRP:** `DomainEventToOutboxInterceptor` yalnız topla+çevir; `OutboxDispatcher` yalnız
  poll+publish+mark; `OrderPaidLoggingConsumer` yalnız logla. **CheckoutHandler'a hiç
  dokunulmadı** — ödeme akışı yayın sorumluluğundan ayrı kaldı.
- **OCP:** `OrderingIntegrationEventRegistry` tek genişleme noktası. Yeni event yayınlamak = bir
  `Register` satırı; interceptor/dispatcher değişmez (W10 PaymentCompleted böyle binecek).
- **LSP:** `OutboxDispatcher : BackgroundService`, `OrderPaidLoggingConsumer : IConsumer<OrderPaid>`
  — framework taban sözleşmelerine (ExecuteAsync/Consume, stoppingToken) davranış bozmadan takılır.
- **ISP:** Dispatcher'a dar `IPublishEndpoint` enjekte edildi (tüm `IBus` değil); mapper yalnız
  `TryMap`/`ResolveType`. Consumer publish bilmez, dispatcher consume bilmez.
- **DIP:** Interceptor→`IIntegrationEventMapper`; dispatcher→`IPublishEndpoint`; MassTransit/RabbitMQ
  ayrıntısı `AddEventBus` ile Shared'da gizli. Contracts POCO — MassTransit'e bile bağlı değil.
  W12 Azure Service Bus'a geçişte dispatcher/consumer kodu değişmez.

## Tasarım kalıpları (seçim gerekçesi)

- **Transactional Outbox:** dual-write problemini (DB commit + broker publish ayrı → biri
  patlarsa tutarsızlık) EF'in tek transaction'ıyla çözer. El yapımı seçildi (built-in EF outbox
  kara kutu — öğretici değeri sıfır; kullanıcı kararı).
- **Interceptor-based collection:** `SaveChangesInterceptor` ChangeTracker'dan tek noktada toplar
  → repository/handler event bilmez. Manuel toplama her repo'da tekrar + SRP sızıntısı olurdu.
- **Registry/Strategy:** domain→integration + discriminator↔Type merkezi, genişletilebilir.
  `switch` OCP'yi bozardı; reflection aşırı mühendislik olurdu (1 event için gereksiz sihir).
- **Template Method / polling loop:** `OutboxDispatcher.ExecuteAsync` sabit iskelet
  poll→publish→mark, parametrik interval/batch. Quartz/Hangfire gereksiz bağımlılık olurdu.
- **Adapter (dolaylı):** mapper domain event'i Contracts POCO'suna çevirir → domain iç tipi dış
  sözleşmeden ayrık; domain'i doğrudan publish etmek MassTransit serileştirmesine kilitlerdi.

## Alınan kararlar

1. **OrderPaid = YENİ domain event, `Order.MarkPaid`'de raise** (D1). Açık niyet: yalnız Paid
   geçişi dışa duyurulur, dispatcher genel `OrderStatusChanged`'leri `ToStatus`'e göre süzmek
   zorunda kalmaz. `MarkPaid` bugüne dek ayrı event üretmiyordu — tek domain dokunuşu.
2. **Domain event ≠ integration event.** Domain'de `Ordering.Domain.Orders.OrderPaid` (iç);
   Contracts'ta `Ordering.Contracts.IntegrationEvents.OrderPaid` (dışa açık, SAF POCO). Mapper
   birini diğerine çevirir; alanlar bilinçli seçilir (dışa ne sızdıracağımızın sözleşmesi).
3. **Outbox ordering-lokal, `ordering` şeması** (D2). Atomiklik DbContext-başına — merkezi outbox
   farklı context'lere atomik yazamaz. `Content` **jsonb**; kısmi index `ix_outbox_unprocessed`
   (`WHERE "ProcessedOnUtc" IS NULL`) sıcak sorguyu ucuz tutar.
4. **Discriminator stabil string ("OrderPaid"), AssemblyQualifiedName DEĞİL** (D5). Tip
   rename/taşımada storage kırılmaz; registry `discriminator↔Type` iki yön çözer.
5. **Tip-silinmiş publish:** `IPublishEndpoint.Publish(object, Type, ct)` — dispatcher generic
   `Publish<T>`'i derleme anında bilemez; registry CLR tipini verir, MassTransit doğru exchange'e
   yönlendirir (RabbitMQ'da `...IntegrationEvents:OrderPaid` fanout exchange'i oluştu).
6. **Interceptor YALNIZ OrderingDbContext'e opt-in bağlanır** (D7, R3). `AddModuleDbContext`'e
   geriye uyumlu `configure` param eklendi (null default → diğer 5 modül değişmedi). Interceptor
   başka modülün DbContext'ine sızmaz.
7. **MassTransit tek bus, `AddEventBus` Program.cs'te BİR kez** (D9). Modül-başına `AddMassTransit`
   çakışırdı. Consumer'lar composition root'tan `configureConsumers` ile enjekte — Shared somut
   consumer tipini bilmez (bağımlılık yönü doğru).
8. **Log-only consumer Notification modülünde** (D10, geçici). `Ordering.Contracts`'a referans
   izinli (Kural 1 bozulmaz — Contracts iç katman değil). Gerçek iş mantığı + idempotent tüketim
   + DLQ W10.
9. **At-least-once yayın** (D11). Publish→mark arası crash → tekrar yayın; log-only consumer'da
   zararsız. İdempotent consumer + dedup W10.
10. **Outbox/interceptor/dispatcher Shared'a çıkarılmadı** (D8). Ordering tek üretici — YAGNI.
    İkinci üretici (Payment, W10) gelince generic parçalar Shared'a terfi eder, registry
    modül-lokal kalır (roadmap ilkesi: "altyapı ilk talep eden feature ile gelir").

## Dürüst kapsam sınırı

- **Yalnız OrderPaid yayınlanır.** Payment integration event'leri (`PaymentCompleted`/
  `PaymentFailed`, FR-6.4) bu hafta **dispatch EDİLMEZ** — tüketicileri W10'da doğar; şimdi
  yayınlamak boşa mesaj üretmek olurdu. **FR-6.4'ün Payment kısmı bu hafta TAM KAPANMADI.**

## Doğrulama (8 Temmuz 2026)

### Testler — tümü yeşil
- **Ordering.UnitTests 73** (+4 registry map/resolve/null, +2 MarkPaid→OrderPaid raise/yanlış-duyuru-yok).
- **Ordering.IntegrationTests 8** (+4): atomiklik (OrderPaid outbox satırı Order ile aynı
  transaction, yalnız 1 satır — StatusChanged/Created terfi etmez); **atomiklik geri sarımı**
  (SaveChanges 23505'te patlar → NE order NE outbox); dispatcher publish+mark; publish hata →
  RetryCount++ + Error saklanır. **Publish→consume:** MassTransit `ITestHarness` (in-memory,
  RabbitMQ container'sız) — `Published.Any<OrderPaid>` + `Consumed.Any<OrderPaid>`.
- **ArchitectureTests 32** yeşil: Notification.Api→Ordering.Contracts (iç değil); Contracts
  referanssız POCO; interceptor/dispatcher Infrastructure'da; OrderPaid domain event yalnız
  Shared.Kernel.

### Uçtan uca (gerçek RabbitMQ)
```
POST /api/ordering/checkout (Idempotency-Key) → 201 status=Paid, orderId=86e82c2a...
RabbitMQ UI (15672): exchange "ModularCommerce.Ordering.Contracts.IntegrationEvents:OrderPaid"
  [fanout] + consumer kuyruğu "order-paid-logging"
Host logu: [W8 log-only] OrderPaid alındı: OrderId=86e82c2a... CustomerId=1bb343ef... Tutar=2699.00 TRY
Postgres: SELECT ... FROM ordering.outbox_messages → OrderPaid | ISLENDI | RetryCount=0
```

## Bilinçli ertelemeler

| Konu | Bu hafta | Hafta |
|---|---|---|
| Payment event dispatch (FR-6.4 tam kapanış) | Domain event raise, dispatch YOK | W10 |
| İdempotent consumer + dedup (inbox/mesaj-id) | Log-only, çift yayın zararsız | W10 |
| Dead-letter queue + zehirli mesaj yönetimi | MaxRetries=10 sonrası satır durur (Error kalır) | W10 |
| Retry/backoff olgunlaşması (exponential+jitter) | Düz RetryCount sayacı | W10 |
| Outbox temizleme/arşivleme job'u | İşlenen satırlar tabloda kalır | W11 |
| Gerçek Shipping/Notification iş mantığı | Yok | W10 |
| Refund/void (P4 parasal telafi, W7 borcu) | Yok | W9 |

## Riskler / notlar

- **Shared'a minimal dokunuş:** `AddModuleDbContext` opt-in `configure` param (interceptor'ı
  yalnız OrderingDbContext'e bağlamak için, R3) + `AddEventBus` (bus tek kayıt, DIP) +
  Shared.Infrastructure.csproj'a MassTransit.RabbitMQ. Outbox mekanizmasının kendisi
  Ordering.Infrastructure'da kaldı.
- **jsonb + SQL LIKE:** `Content.Contains(...)` jsonb kolonda `~~` operatörü olmadığından
  patlıyor — integration testleri outbox satırlarını belleğe alıp CustomerId'ye göre filtreliyor
  (koleksiyon paylaşımlı DB deseni).
- **PendingModelChangesWarning tuzağı:** migration eklendikten sonra Host `--no-build` ile eski
  binary'yi çalıştırınca model-migration uyuşmazlığı exit 82 verdi; çözüm tam rebuild.
- **Dispatcher test edilebilirliği:** batch çekirdeği `internal ProcessBatchAsync(db, publisher,
  mapper, ct)` olarak ayrıldı (DIP seam) + `InternalsVisibleTo` — scope/DI kurmadan doğrudan test.
- Test sayısı 275 → **281** civarı (Ordering unit +6, integration +5; ayrıntı yukarıda).
