# Hafta 10 Karar Notları — Notification: Idempotent Consumer + DLQ

> Roadmap Hafta 10 çıktısı: **Notification event tüketicisi** (`OrderPaid` dinler) + cross-cutting
> **idempotent consumer** ve **dead-letter queue**. Kanıt: tam akış (checkout+ödeme tek istek) →
> OrderPaid → bildirim (e-posta/webhook SİM) + idempotency (çift teslim → tek bildirim) + DLQ demo.
> **Kapsam kararı:** Shipping bu hafta yok — yalnız Notification (kullanıcı kararı).

## KRİTİK BULGU — İdempotency anahtarı MessageId olamaz

El yapımı `OutboxDispatcher` her `IPublishEndpoint.Publish(...)` çağrısında MassTransit'e **yeni bir
MessageId** ürettirir ([OutboxDispatcher.cs:100](../src/Modules/Ordering/ModularCommerce.Ordering.Infrastructure/Outbox/OutboxDispatcher.cs#L100)).
At-least-once tekrar (publish sonrası `ProcessedOnUtc` mark'ından önce crash) **aynı outbox satırını**
tekrar yayınlarsa MassTransit **farklı** MessageId atar. Sonuç: MessageId ile dedup eden bir inbox
**tekrarı yakalayamaz**.

→ İdempotency anahtarı **iş anahtarı** olmalı: `OrderPaid` için `"OrderPaid:{OrderId}"` (sipariş
başına tek onay bildirimi). Bu anahtar outbox satırı boyunca stabildir. "İdempotency key = doğal iş
anahtarı" prensibinin somut kanıtı; W10'un öğretici çekirdeği. (MassTransit MessageId yalnız audit
kolonu olarak saklanır.)

## Mimari akış

```
checkout+ödeme (tek istek) → Order.MarkPaid → OrderPaid domain event
  → DomainEventToOutboxInterceptor → ordering.outbox_messages (aynı txn, W8)
  → OutboxDispatcher (~1sn) → Publish(OrderPaid)          [her publish YENİ MessageId]
      → RabbitMQ → kebab-case kuyruk: order-paid-notification
          → OrderPaidNotificationConsumer  [ConsumeContext → NotificationInstruction (ACL)]
              key = "OrderPaid:{OrderId}"   ← iş anahtarı, MessageId DEĞİL
              → INotificationProcessor.ProcessAsync
                  ├ processed_messages'ta (key,consumer) VAR? → SKIP (idempotent no-op)
                  ├ YOK → her kanal SendAsync (e-posta + webhook SİM)
                  │        └ FaultInjectingChannel: FailureRate>rng → THROW ─┐
                  └ TEK TRANSACTION: NotificationLog(×kanal) + ProcessedMessage → SaveChanges
                           └ PK 23505 (eşzamanlı çift teslim) → idempotent skip
                                                                             │
   THROW ─→ UseMessageRetry(3×, 2sn) tükenir ─→ order-paid-notification_error   (DLQ, FR-8.3)
```

## İdempotency mekanizması (el yapımı inbox)

- **`processed_messages`** tablosu, PK = `(IdempotencyKey, ConsumerType)` → `pk_processed_messages`.
- **Nihai hakem PK insert'idir** (Payment'ın `(customer,key)` unique-index hakemiyle aynı desen):
  `AnyAsync` kontrolü yalnız hızlı-yoldur; eşzamanlı iki teslim ikisi de kontrolü boş geçse bile
  PK yalnız birinin insert'ine izin verir, kaybeden `23505` → `DbUpdateException` yakalanır →
  idempotent başarı döner.
- **Sıra: gönderim → sonra persist, tek transaction.** `SendAsync` fırlarsa `SaveChanges`'e hiç
  ulaşılmaz → processed satırı yazılmaz → mesaj retry edilir (at-least-once). Audit satırları +
  processed-marker atomik yazılır (W8 outbox'ın "iş yazımı + tracking satırı aynı SaveChanges"
  simetriği).

## SOLID prensipleri

- **SRP:** `OrderPaidNotificationConsumer` yalnız ConsumeContext→instruction+processor çağrısı;
  `NotificationProcessor` yalnız inbox-guard + gönderim + audit; her kanal tek teslim mekanizması;
  `FaultInjectingChannel` yalnız hata enjeksiyonu (kanallar temiz kalır).
- **OCP:** yeni kanal = yeni `INotificationChannel` impl + DI kaydı (processor iterasyonu değişmez);
  yeni tüketilen event = yeni consumer+definition (mevcut consumer'a dokunulmaz — W8 registry
  felsefesinin tüketici tarafı).
- **LSP:** `OrderPaidNotificationConsumer : IConsumer<OrderPaid>` (log-only ile bire-bir değişti);
  kanallar `INotificationChannel` arkasında birbirinin yerine geçer.
- **ISP:** `INotificationChannel` tek `SendAsync`; `INotificationProcessor` tek `ProcessAsync` —
  consumer DbContext'i görmez.
- **DIP:** consumer soyut `INotificationProcessor`'a bağlı (Infrastructure implement eder); processor
  somut kanala değil `IEnumerable<INotificationChannel>`'a bağlı — testte sahte kanal enjekte edilir.

## Tasarım kalıpları

- **Idempotent Consumer / Inbox (el yapımı):** `processed_messages` + PK hakemi (NFR-8.1).
- **Transactional Inbox:** audit + processed-marker tek SaveChanges (W8 outbox simetriği).
- **Strategy:** `INotificationChannel` (e-posta/webhook) — FR-8.1 çok-kanal, FR-6.5 "takılıp
  çıkarılabilir" felsefesi.
- **Decorator:** `FaultInjectingChannel` gerçek kanalı sarar — cross-cutting hata enjeksiyonu kanalı
  kirletmeden (OCP), DLQ demo'su tek noktadan.
- **Options/Knob:** `NotificationOptions` (config, 0 default) — Payment PSP knob deseni; K6/demo'da
  1.0'a çekilir.
- **Consumer Definition:** `OrderPaidNotificationConsumerDefinition` retry/DLQ politikasını
  tüketiciyle yan yana tutar ve YALNIZ bu endpoint'e uygular (AddEventBus imzası değişmedi).
- **Adapter/ACL:** consumer `OrderPaid` → iç `NotificationMessage` (Ordering.Contracts tipi iç modele
  sızmaz — modül sınırı yalnız Api'de görülür).

## Alınan kararlar

1. **Kapsam yalnız Notification** (kullanıcı kararı): Shipping, `ShipmentStatusChanged` ve outbox'ın
   Shared'a terfisi bu hafta yok. "İkinci üretici" gelmediğinden W8 notunun öngördüğü terfi ertelendi.
2. **El yapımı inbox** (kullanıcı kararı): MassTransit built-in EF inbox yerine `processed_messages` —
   W8 el yapımı outbox ile simetri, desen sınıf sınıf görünür.
3. **İdempotency anahtarı iş anahtarı** (`OrderId`), MessageId değil — kritik bulgu (yukarıda).
4. **Retry politikası consumer definition'da** (`UseMessageRetry` 3×, 2sn): AddEventBus imzası
   korundu (Shared'a **sıfır** dokunuş, D9). `ConfigureEndpoints(context)` definition'ı zaten uygular.
5. **DLQ = hata-knob + `_error` kuyruğu** (kullanıcı kararı): `FailureRate=1.0` → retry tükenir →
   MassTransit mesajı otomatik `order-paid-notification_error` kuyruğuna taşır (RabbitMQ UI kanıtı).
6. **Gecikmeli redelivery (`UseDelayedRedelivery`) yok:** RabbitMQ delayed-exchange plugin'i
   gerektirir; yalnız in-memory message retry kullanıldı.
7. **İki kanal** (e-posta + webhook), kanal başına bir `NotificationLog` audit satırı → idempotency
   DB'de görünür (çift teslim → tek satır seti).
8. **Development-only audit endpoint** `GET /api/notification/dev/logs/{orderId}` — idempotency
   kanıtı buradan okunur (Payment dev-okuma deseni; production'da map edilmez).

## Doğrulama (9 Temmuz 2026)

### Testler — Notification +7 (tümü yeşil)
- **Notification.UnitTests 2:** `FaultInjectingChannel` knob=1.0 fırlatır / knob=0 delege eder.
- **Notification.IntegrationTests 5** (Testcontainers Postgres):
  - `FirstTime_WritesOneLogPerChannel_AndOneProcessedRow` — 2 audit + 1 processed satırı.
  - `SameOrder_DifferentMessageId_IsIdempotent` — **farklı MessageId, aynı OrderId** → yine 2 audit
    (MessageId ile dedup edilseydi 4 olurdu — kritik bulgunun kanıtı).
  - `ConcurrentDoubleDelivery_PkArbiterKeepsOneSet` — eşzamanlı iki teslim → **1 processed + 2 audit**,
    her ikisi de başarı (PK hakemi).
  - Harness (gerçek consumer+definition): mutlu yol audit yazar; knob=1.0 → consumer **fault** olur
    (retry → `_error`/DLQ yolu), fault yolunda hiçbir satır kalıcı olmaz.
- **ArchitectureTests 32** — 4 kural yeşil (Notification.Api yalnız Ordering.Contracts'a bağlı).

### DLQ demo (manuel, gerçek RabbitMQ)
```
Notification__Delivery__FailureRate=1.0 ile Host çalıştır → checkout+ödeme
  → OutboxDispatcher OrderPaid publish → consumer 3 kez dener, hepsi knob'la fırlar
  → RabbitMQ UI (localhost:15672): order-paid-notification_error kuyruğunda mesaj görünür
Normal koşu (FailureRate=0): dev/logs/{orderId} → 2 satır (email+webhook), tek set.
```

## Bilinçli ertelemeler

| Konu | Bu hafta | Hafta |
|---|---|---|
| Shipping modülü (FR-7.*) + `ShipmentStatusChanged` | YOK (kullanıcı kararı) | W11+ |
| Outbox'ın Shared'a terfisi (ikinci üretici) | YOK — üretici gelmedi | üretici geldiğinde |
| `OrderCancelled` / `PaymentCompleted` tüketimi | Yayınlanır, tüketici yok (OCP-hazır) | sonraki |
| İkinci-seviye gecikmeli redelivery | Yalnız in-memory retry | delayed-exchange plugin geldiğinde |
| Gerçek SMTP/HTTP webhook | Simüle (log) | W12+ |

## Riskler / notlar

- **Shared'a SIFIR kod değişikliği** — retry/DLQ consumer definition'dan gelir; inbox
  Notification-lokal; `AddEventBus` imzası korundu.
- **Fault yolunda undersell yok:** gönderim persist'ten önce yapılır; fırlarsa transaction geri
  sarılır → ne audit ne processed satırı kalır → mesaj güvenle retry/DLQ edilir.
- **Eşzamanlı çift teslim penceresi:** race'te iki "SİM mail" gidebilir (at-least-once kabulü) ama
  PK hakemi yalnız tek audit satırı bırakır → gözlemlenebilir sonuç idempotenttir.
- **Test Harness `_error` kuyruğu üretmez** — fault harness'ta assert edilir; gerçek `_error` kuyruğu
  yalnız RabbitMQ'da (manuel UI kanıtı).
