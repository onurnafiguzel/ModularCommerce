# Modüler Monolit ile E-Ticaret: Bir Mimarinin Anatomisi

*Mikroservis karmaşasına girmeden, "büyük çamur topu"na da dönüşmeden: 8 modüllü bir .NET 10 e-ticaret platformunun sınırları, DDD uygulanışı ve uçtan uca istek yolculukları.*

---

## Neden Modüler Monolit?

Bir e-ticaret sistemi kurarken iki uçta da tuzak var:

- **Klasik monolit**: Her şey her şeyi çağırır. `OrderService` doğrudan `products` tablosuna JOIN atar, altı ay sonra kimse hangi değişikliğin neyi kıracağını bilemez.
- **Erken mikroservis**: Daha domain'i anlamadan ağ sınırları, dağıtık transaction'lar, servis keşfi, izleme altyapısı... Karmaşıklığı iş probleminden önce ödersiniz.

**Modüler monolit** üçüncü yolu seçer: *tek deploy edilebilir süreç, ama içeride mikroservis disiplininde sınırlar.* Modüller birbirinin iç katmanlarına dokunamaz; iletişim yalnız sözleşmeler (Contracts) ve olaylar (integration events) üzerinden akar. Sınırlar baştan doğru çizildiği için, yarın bir modülü gerçekten ayrı servise çıkarmak "big bang" değil, bir taşıma işlemi olur.

ModularCommerce tam bu felsefeyle inşa edildi: race condition, idempotency ve resiliency problemlerini **ölçülebilir** biçimde çözen, K6 yük testleriyle kanıtlanan bir platform.

---

## Büyük Resim

```
                                ┌────────────────────────────────────────────────┐
                                │        ModularCommerce.Host (tek süreç)        │
                                │                                                │
  ┌──────────┐   HTTPS          │  Middleware Pipeline                           │
  │  Client   │ ───────────────▶│  CorrelationId → Serilog → ExceptionHandler    │
  │ (SPA/K6)  │ ◀─────────────── │  → Auth → RateLimiter → Endpoints             │
  └──────────┘   JSON/Problem   │                                                │
                                │  ┌────────┐ ┌────────┐ ┌────────┐ ┌─────────┐  │
                                │  │Identity│ │Catalog │ │  Cart  │ │Inventory│  │
                                │  └────────┘ └────────┘ └────────┘ └─────────┘  │
                                │  ┌────────┐ ┌────────┐ ┌────────┐ ┌─────────┐  │
                                │  │Ordering│ │Payment │ │Shipping│ │Notific. │  │
                                │  └────────┘ └────────┘ └────────┘ └─────────┘  │
                                └───────┬──────────────┬──────────────┬──────────┘
                                        │              │              │
                              ┌─────────▼────┐  ┌──────▼─────┐  ┌─────▼──────┐
                              │  PostgreSQL  │  │   Redis    │  │  RabbitMQ  │
                              │ (şema/modül) │  │ cart+lock  │  │ (MassTr.)  │
                              │ identity ord.│  │ +catalog$  │  │ OrderPaid  │
                              │ payment inv..│  │            │  │ OrderCanc. │
                              └──────────────┘  └────────────┘  └────────────┘
```

**8 modül**: Identity, Catalog, Cart, Inventory, Ordering, Payment, Shipping, Notification. Hepsi aynı süreçte yaşar ama:

- Her modülün **kendi PostgreSQL şeması** vardır (`identity`, `catalog`, `ordering`, `payment`, `inventory`, `notification`). Bir modül diğerinin tablosuna SQL bile atamaz — runtime'da ayrı DB kullanıcılarıyla da kilitlenebilir.
- Cart hiç ilişkisel veritabanı kullanmaz: **yalnız Redis**, 7 gün TTL. Sepet kaybolabilir bir veridir; AP tarafında konumlanır.
- Modüller arası *gerçekler* (sipariş ödendi, iptal edildi) **RabbitMQ** üzerinden olay olarak yayılır.

---

## Bir Modülün Anatomisi: 5 Proje, Tek Yön

Her modül aynı iskeleti taşır:

```
src/Modules/Ordering/
 ├── ModularCommerce.Ordering.Domain          ← saf domain modeli
 ├── ModularCommerce.Ordering.Application     ← use case'ler (handler'lar)
 ├── ModularCommerce.Ordering.Infrastructure  ← EF Core, Redis, adaptörler
 ├── ModularCommerce.Ordering.Api             ← IModule + endpoint'ler
 └── ModularCommerce.Ordering.Contracts       ← DIŞ DÜNYAYA AÇIK TEK KAPI
```

Referans yönü tek ve kesindir:

```
        Api ──▶ Infrastructure ──▶ Application ──▶ Domain
                                        │              │
                                        ▼              ▼
                                    Contracts    Shared.Kernel
```

- **Domain** yalnız `Shared.Kernel`'i görür. EF Core'a bile referans veremez — bunu bir mimari test zorlar. Domain'de `DbContext` yoktur, `[Key]` attribute'u yoktur; yalnız iş kuralları vardır.
- **Contracts**, diğer modüllerin referans verebileceği *tek* projedir: arayüzler (`IPaymentService`, `IStockReservationService`), DTO'lar, integration event POCO'ları. Kendi modülünün iç katmanlarına dahi referans veremez.
- **Api** katmanındaki `<Modül>Module : IModule` sınıfı, modülün composition root'udur: `Register(services, config)` ile kendi servislerini kaydeder, `MapEndpoints(app)` ile rotalarını açar.

Host tarafında tek satırlık bir dizi tüm sistemi ayağa kaldırır:

```csharp
// Program.cs — modül eklemek = diziye eleman eklemek
private static readonly IModule[] Modules =
[
    new IdentityModule(), new CatalogModule(), new CartModule(),
    new InventoryModule(), new OrderingModule(), new PaymentModule(),
    new ShippingModule(), new NotificationModule(),
];
```

### Modüller Arası Konuşma: Somut Bir Örnek

Teoriyi bir gerçek çağrıyla somutlaştıralım: checkout sırasında **Ordering, Cart'a "bu müşterinin sepetinde ne var?"** diye sorar. Bu konuşmanın her parçasının *nerede yaşadığına* dikkat edin:

**Sözleşme, sahibinin `Contracts` projesindedir.** Arayüz de, çağrının yanıt modeli (DTO) de `Cart.Contracts` içinde tanımlıdır — çünkü sepetin sahibi Cart'tır:

```csharp
// src/Modules/Cart/ModularCommerce.Cart.Contracts/ICartService.cs
public interface ICartService
{
    Task<Result<IReadOnlyList<CartLineDto>>> GetItemsAsync(
        Guid customerId, CancellationToken cancellationToken);

    Task<Result> ClearAsync(Guid customerId, CancellationToken cancellationToken);
}

// src/Modules/Cart/ModularCommerce.Cart.Contracts/CartLineDto.cs
public sealed record CartLineDto(Guid ProductId, int Quantity);
```

`CartLineDto`'nun ne kadar *fakir* olduğuna bakın: sadece `ProductId` + `Quantity`. Cart'ın iç modelinde daha fazlası olabilir; dışarıya yalnız tüketicinin ihtiyacı kadar sızar. DTO burada bir **anti-corruption hattıdır** — Ordering, Cart'ın iç modeline değil, bu küçük kayda bağımlıdır.

**Tüketici yalnız arayüzü enjekte eder.** Ordering'in `CheckoutHandler`'ı Cart'ın hiçbir iç sınıfını görmez; csproj'unda yalnız `Cart.Contracts` referansı vardır:

```csharp
// Ordering.Application — CheckoutHandler (primary constructor ile)
public sealed class CheckoutHandler(
    IOrderRepository orders,               // kendi domain portu
    ICartService cartService,              // ← Cart.Contracts
    IProductReader productReader,          // ← Catalog.Contracts
    IStockReservationService stockReservation,  // ← Inventory.Contracts
    IPaymentService paymentService,        // ← Payment.Contracts
    ...)
{
    // ...
    var cartResult = await cartService.GetItemsAsync(command.CustomerId, ct);
}
```

Bu constructor imzası aslında bir mimari haritadır: Ordering'in dört komşusuyla ilişkisinin tamamı dört `Contracts` arayüzüne sığar.

**İmplementasyon (adaptör) sahibinin içindedir ve sahibi tarafından kaydedilir.** `ICartService`'i gerçekleyen `CartService`, Cart modülünün içinde yaşar; DI kaydını da `CartModule.Register` yapar:

```csharp
// CartModule.cs — Cart kendi sözleşmesinin adaptörünü kendisi bağlar
services.AddScoped<Cart.Contracts.ICartService, CartService>();
```

Akışın tamamı tek şemada:

```
   Ordering (tüketici)                          Cart (sahip)
 ┌─────────────────────────┐            ┌───────────────────────────────┐
 │ CheckoutHandler          │            │ CartService (adaptör)         │
 │   ctor(ICartService ...) │            │   : ICartService              │
 └───────────┬─────────────┘            └───────────────▲───────────────┘
             │ yalnız arayüzü bilir                      │ implements
             ▼                                           │
        ┌─────────────────────────────────────────────────────┐
        │  Cart.Contracts   ←— SINIR BURADA                   │
        │    ICartService + CartLineDto (+ Result, kernel'den)│
        └─────────────────────────────────────────────────────┘
             DI çalışma zamanında ikisini buluşturur (in-process çağrı)
```

**Peki HTTP request/response modelleri nerede?** Onlar cross-module sözleşme *değildir* — modülün kendi dış kapısına aittir ve kendi `Application` katmanında yaşar. Örneğin `POST /api/ordering/checkout`'un yanıtı olan `OrderResponse`/`CheckoutResponse`, `Ordering.Application/Orders/Common` altındadır; endpoint bunları doğrudan JSON'a serileştirir. Kural şu:

| Model türü | Nerede yaşar | Kim görür |
|---|---|---|
| Cross-module DTO (`CartLineDto`, `ProductSnapshotDto`) | **Sahibin `Contracts`** projesi | Diğer modüller |
| HTTP request/response (`CheckoutResponse`, `OrderResponse`) | Modülün kendi `Application` katmanı | Yalnız o modülün endpoint'i + istemci |
| Domain modeli (`Order`, `OrderLine`) | Modülün `Domain`'i | Yalnız modülün kendisi |

Bugün bu çağrı süreç-içi bir metot çağrısıdır (nanosaniyeler). Yarın Cart ayrı bir servise çıkarsa, değişen tek şey `CartService` adaptörünün içi olur — HTTP/gRPC istemcisine dönüşür. `CheckoutHandler` ve `ICartService` imzası aynen kalır. Sınırı baştan çizmenin getirisi tam olarak budur.

### Sınır Nasıl Korunuyor? (Üç Katmanlı Savunma)

Sınır kuralı yalnızca "lütfen uyalım" değil; üç yerde zorlanır — ve evet, ilki için **gerçekten kod yazdık**.

**1. Derleme sonrası — NetArchTest (kendi yazdığımız mimari testler).** Bu bir IDE ayarı ya da analizör paketi büyüsü değil; sıradan bir xUnit test projesidir (`tests/ModularCommerce.ArchitectureTests`). NetArchTest kütüphanesi, derlenmiş assembly'leri reflection ile tarar ve içindeki *her tipin* bağımlılıklarını inceler. Kuralın kendisi elle yazılmış birkaç satırdır:

```csharp
// ModuleBoundaryTests.cs — kuralın TAMAMI bu iki diziden türetilir
private static readonly string[] Modules =
    ["Identity", "Catalog", "Cart", "Inventory",
     "Ordering", "Payment", "Shipping", "Notification"];

private static readonly string[] InternalLayers =
    ["Domain", "Application", "Infrastructure", "Api"];

[Theory]
[MemberData(nameof(ModuleNames))]
public void Module_should_not_depend_on_other_modules_internals(string module)
{
    var assembly = Assembly.Load($"ModularCommerce.{module}.Api");

    // 7 diğer modül × 4 iç katman = bu modül için 28 yasak namespace
    var forbidden = Modules
        .Where(other => other != module)
        .SelectMany(other => InternalLayers.Select(l => $"ModularCommerce.{other}.{l}"))
        .ToArray();

    Types.InAssembly(assembly)
        .Should().NotHaveDependencyOnAny(forbidden)
        .GetResult().IsSuccessful.Should().BeTrue();
}
```

Nasıl çalıştığına dikkat edin: **yasaklar tek tek yazılmaz, iki diziden üretilir.** Yarın 9. modül gelirse tek yapılacak şey `Modules` dizisine bir string eklemek — 8 test otomatik 9 olur ve yeni modül anında hem denetlenen hem yasak listesine giren taraf olur. Aynı dosyada üç kural daha vardır: `Contracts` kendi kendine yeterli olmalı (iç katman sızdıramaz), `Application` EF Core görmemeli, `Domain` hiçbir dış katmanı görmemeli. Bu testler `dotnet test` ile her CI koşusunda çalışır; birisi `CheckoutHandler`'a `using ModularCommerce.Inventory.Domain;` yazarsa build **kırmızıya döner** — tartışma PR yorumunda değil, test çıktısında biter.

**2. Çalışma zamanı — şema izolasyonu.** Her modülün DbContext'i kendi PostgreSQL şemasına kilitlidir; Ordering'in context'inde `products` diye bir DbSet zaten yoktur. Ayrı DB kullanıcılarıyla bu, veritabanı seviyesinde de kilitlenebilir (GRANT yalnız kendi şemasına).

**3. Kod incelemesi — mekanik kuralın göremediği ihlaller için.** Peki test varken review neden gerekli? Çünkü mekanik kural yalnız *referansı* görür; **niyeti** göremez. Review'da bakılan iki somut yer vardır: csproj'a eklenen `<ProjectReference>` satırları (diff'te tek satırdır, gözden kaçmaz) ve dosya başındaki `using` blokları. Ama asıl değeri daha yumuşak ihlallerde gösterir — teknik olarak *yasal* ama mimari olarak *yanlış* olan şeylerde:

- Bir DTO'ya gereğinden fazla alan koyup iç modeli `Contracts` üzerinden sızdırmak (referans kuralı ihlal edilmez, kapsülleme edilir),
- İş kuralını Domain yerine handler'a yazmak,
- İki modülün senkron çağrıyla çözmemesi gereken bir şeyi çağrıyla çözmesi (olay gerekirken RPC).

Yani üç katman şöyle tamamlanır: test *yapıyı*, şema *veriyi*, insan *anlamı* korur.

**"Bilinçli istisna" nasıl oluyor — madem hiçbir şekilde olmuyor?** Burada sık karışan bir nüansı açalım. Kural hiçbir zaman "başka modüle referans yasak" demez; **"başka modülün *iç katmanlarına* referans yasak"** der. `Contracts`'a referans vermek her modül için, her yönde *serbesttir* — sınırın var oluş amacı zaten o kapıyı açık bırakmaktır. Dolayısıyla `Inventory.Infrastructure → Ordering.Contracts` referansı kuralı **ihlal etmez** ve mimari testlerden yeşil geçer.

Onu "istisna" yapan şey kural değil, **yön**dür. Sistemdeki doğal akış hep orkestratörden aşağıya doğruydu: Ordering, Inventory/Payment/Cart sözleşmelerini tüketir. TTL süpürücüsü ise ilk kez tersini gerektirdi — Inventory'nin, Ordering'e "bu rezervasyonun siparişi ödendi mi?" diye sorması gerekiyor (`IOrderReservationReconciler`). Peki bu döngü (cycle) yaratmaz mı? Yaratmaz, çünkü iki yöndeki referans da iç katmanlara değil `Contracts`'a gider ve Contracts projeleri geriye hiçbir şeye referans vermez:

```
Ordering.Application ──▶ Inventory.Contracts   (checkout: rezerve et)
Inventory.Infrastructure ──▶ Ordering.Contracts (süpürücü: ödendi mi?)

Contracts projeleri ──▶ yalnız Shared.Kernel    (geri ok YOK → graf asiklik)
```

"Bilinçli" ve "belgeli" olması ise mühendislik hijyenidir: bu ilk ters-yön bağımlılığı hem CLAUDE.md'de hem kod yorumunda gerekçesiyle not edilmiştir. İkinci ve üçüncü ters ok gelmeye başlarsa bu bir koku olur — belki o noktada süpürücünün sorusu senkron çağrı yerine bir olaya (`OrderPaid`'i Inventory'nin da dinlemesi) dönüşmelidir. Sınır mimarisi statik bir yasa değil, izlenen bir bütçedir.

---

## DDD Burada Nasıl Görünüyor?

Kitaptaki DDD ile üretimdeki DDD arasında fark vardır. Bu projede uygulanan biçimi:

### Bounded Context = Modül

Her modül bir bounded context'tir ve **aynı kavram farklı bağlamlarda farklı modellenir**. "Ürün" kelimesi:

- **Catalog**'da zengin bir aggregate'tir: ad, açıklama, SKU, fiyat, aktiflik.
- **Ordering**'de yalnız bir *snapshot*'tır: `OrderLine` sipariş anındaki adı ve fiyatı kopyalar. Katalogda fiyat sonradan değişse bile sipariş tarihi bozulmaz.
- **Inventory**'de sadece bir `ProductId` + sayaçlardır (OnHand/Reserved). Stokun ürünün rengiyle işi yoktur.

### Aggregate'ler ve İş Kuralı Yeri

İş kuralları **yalnız Domain'de** yaşar. Handler'lar orkestra şefidir, kural koymaz:

```
Order (aggregate root)
 ├── OrderLine[]           (owned — aggregate dışında yaşayamaz)
 ├── OrderStatusChange[]   (owned — her geçişin kalıcı izi)
 └── AllowedTransitions    (durum makinesi: TEK doğruluk kaynağı)

Payment (aggregate root)
 ├── Money Amount          (value object)
 └── PaymentAttempt[]      (append-only deneme izi)

StockItem (aggregate root)
 └── Reservation[]         (Active → Committed/Released/Expired/Returned)
```

`Order` durum makinesi bir tablodur; hangi durumdan hangisine geçilebileceği tek yerde tanımlıdır:

```csharp
private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
{
    [OrderStatus.Created]        = [OrderStatus.StockReserved, OrderStatus.Cancelled],
    [OrderStatus.StockReserved]  = [OrderStatus.PaymentPending, OrderStatus.Cancelled, OrderStatus.Expired],
    [OrderStatus.PaymentPending] = [OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.Expired],
    [OrderStatus.Paid]           = [OrderStatus.Shipped, OrderStatus.Cancelled],
    [OrderStatus.Shipped]        = [],
    [OrderStatus.Cancelled]      = [],
    [OrderStatus.Expired]        = [],
};
```

Geçersiz geçiş istisna değil, `Result.Failure` döner — çünkü:

### Result/Error Railway: Beklenen Hata İstisna Değildir

`Shared.Kernel`'deki `Result<T>` + `Error(Code, Message, Type, Retryable)` tipi tüm katmanlarda akar. "Stok yetersiz" bir istisna değil, işin doğal bir sonucudur:

```csharp
public static Result<Order> Create(...)
{
    if (lines.Count == 0)
        return Result.Failure<Order>(OrderErrors.NoLines);
    ...
    return Result.Success(order);
}
```

Bu zincirin ucunda tek bir çevirmen vardır: `ToHttpResult()` uzantısı `ErrorType`'ı HTTP koduna eşler (Validation→400, NotFound→404, Conflict→409, Unauthorized→401) ve gövdeye makine-okunur `code` + geçici hatalarda `retryable: true` yazar. Hiçbir endpoint kendi hata eşlemesini yapmaz.

### Value Object: Money

`(decimal, string)` çiftinin her yerde elle taşınması yerine kernel'de tek bir `Money`:

```csharp
var price = Money.Create(2499.90m, "TRY");   // doğrulama tek noktada
var total = unitPrice.Multiply(quantity);     // satır toplamı
var sum   = a.Add(b);                          // farklı para birimi = kırık invariant → fırlatır
```

Catalog fiyatı, Payment tutarı ve OrderLine birim fiyatı aynı tipi paylaşır; EF tarafında `ComplexProperty`/`OwnsOne` ile aynı kolonlara eşlenir — migration'sız zenginleştirme.

### Domain Event ≠ Integration Event

Kritik bir ayrım: `Order.MarkPaid()` içeride bir **domain event** (`OrderPaid`) fırlatır. Dış dünyaya çıkan ise `Ordering.Contracts` içindeki ayrı bir POCO'dur. İç model istediği kadar evrilebilir; dış sözleşme sabittir.

---

## İstek Yolculukları: Client'tan Sunucuya

Asıl hikâye burada. Tipik bir kullanıcının yolculuğunu uçtan uca izleyelim.

### Ortak Kapı: Middleware Pipeline

Her istek aynı boru hattından geçer:

```
İstek → CorrelationId (X-Correlation-Id üret/taşı)
      → Serilog request log
      → ExceptionHandler (beklenmeyen hata → 500 ProblemDetails)
      → Authentication (JWT doğrula)
      → Authorization
      → RateLimiter (global + auth/checkout policy'leri)
      → Endpoint
```

Rate limiter auth'tan *sonra* durur — böylece partition anahtarı olarak anonim istekte IP'yi, kimlikli istekte kullanıcı id'sini görebilir.

### 1. Kimlik: Signup → Login → JWT

```
Client                          Identity Modülü                    identity şeması
  │  POST /api/identity/signup      │                                   │
  │ ────────────────────────────▶  │  User aggregate + hash             │
  │                                 │ ─────────────────────────────────▶│ users
  │  201                            │   (unique index: e-posta yarışının │
  │ ◀────────────────────────────  │    gerçek hakemi — 23505 → 409)    │
  │                                 │                                   │
  │  POST /api/identity/login       │                                   │
  │ ────────────────────────────▶  │  şifre doğrula                     │
  │  { accessToken, refreshToken }  │ ─────────────────────────────────▶│ refresh_tokens
  │ ◀────────────────────────────  │                                   │
```

- Access token 15 dk, refresh token 7 gün ve **rotasyonlu**: her `POST /refresh` eski token'ı geçersiz kılıp yenisini verir.
- Login/signup `auth` rate-limit policy'sine bağlıdır: IP başına sıkı limit — brute-force burada 429 yer.
- İnce ama önemli ders: "e-posta zaten var mı?" kontrolü *check-then-insert* yarışına açıktır. Gerçek hakem uygulama kodu değil, **veritabanının unique index'idir**; 23505 hatası domain hatasına çevrilir.

### 2. Vitrin: Catalog (Cache'li Okuma)

```
Client                     Catalog Modülü
  │ GET /api/catalog/products/{id}  │
  │ ───────────────────────────▶   │
  │                                 │   CachingProductQueries (Decorator)
  │                                 │     1. Redis GET catalog:product:{id}
  │                                 │        ├─ HIT  → DB'ye hiç gitmez
  │                                 │        └─ MISS → EF sorgusu → Redis SET (TTL 60s)
  │  200 ProductDetail              │
  │ ◀───────────────────────────   │
```

Cache bir **Decorator**'dır: DB implementasyonu (`ProductQueries`) cache'ten habersizdir. En kritik özellik *graceful degradation* — Redis düşerse okuma cache-miss gibi davranıp veritabanına düşer. Cart'ta Redis *kaynaktır* (düşerse hata döner); Catalog'da yalnız *hızlandırıcıdır* (düşerse yavaşlar, kırılmaz). Aynı teknoloji, iki farklı sorumluluk sözleşmesi.

### 3. Sepet: Redis-Only

```
Client                          Cart Modülü                Redis
  │ POST /api/cart/items            │                        │
  │   { productId, quantity }       │  IProductReader ile    │
  │ ───────────────────────────▶   │  fiyat/ad snapshot al   │
  │                                 │  (Catalog.Contracts)   │
  │                                 │ ──────────────────────▶│ SET cart:{userId}
  │  200 sepet                      │      (TTL 7 gün,       │
  │ ◀───────────────────────────   │       her yazmada kayar)│
```

Sepetin DbContext'i yoktur. JSON belge olarak `cart:{customerId}` anahtarında yaşar. TTL her dokunuşta yenilenir — aktif sepet yaşar, terk edilen sepet 7 günde buharlaşır. Bu bilinçli bir **CAP konumlanmasıdır**: sepet AP'dir (kaybolması can yakmaz), sipariş ve ödeme CP'dir (asla çift olamaz).

### 4. Ana Olay: Checkout

Sistemin kalbi. Tek bir `POST`, dört modülü senkron koordine eder ve iki veritabanı hakemiyle "tam bir kez" garantisi verir:

```
Client            Ordering           Cart      Catalog    Inventory      Payment
  │ POST /api/ordering/checkout        │          │           │             │
  │ Idempotency-Key: k1  (ZORUNLU)     │          │           │             │
  │ ─────────────▶ │                   │          │           │             │
  │                │ (a) k1 daha önce işlendi mi? ── evet ise → 200 KOPYA   │
  │                │                   │          │           │             │
  │                │ (b) sepeti oku ──▶│          │           │             │
  │                │ (c) fiyat/ad snapshot ──────▶│           │             │
  │                │ (d) her satır için rezerve ─────────────▶│ Reserved+=q │
  │                │     (başarısızsa: öncekileri Release et, 409 dön)      │
  │                │ (e) Order: Created → StockReserved → PaymentPending    │
  │                │ (e2) ChargeAsync(k1) ────────────────────────────────▶ │
  │                │      1. HAKEM: payments unique index (aynı k1 asla     │
  │                │         iki kez charge OLAMAZ)             Polly boru  │
  │                │      declined ise → rezervasyonlar Release, sipariş    │
  │                │      HİÇ yazılmaz, hata aynı yanıtta döner             │
  │                │ (f) Order'ı Paid olarak persist et                     │
  │                │      2. HAKEM: orders unique index (customer+k1)       │
  │                │      yarışı kaybeden → kazananın kopyasını döner       │
  │                │      + OrderPaid outbox satırı AYNI transaction'da     │
  │                │ (f2) rezervasyonları Commit et ─────────▶│ OnHand-=q   │
  │                │ (g) sepeti temizle (best-effort) ▶│      │ Reserved-=q │
  │  201 / 200     │                   │          │           │             │
  │ ◀───────────── │                   │          │           │             │
```

Buradaki üç tasarım kararı makalelik:

**1) Idempotency zorunlu, hakem veritabanı.** `Idempotency-Key` başlığı olmadan checkout reddedilir. Aynı key ile 100 paralel istek gönderin (K6 testimiz tam bunu yapar): 1 tanesi 201 alır, 99'u aynı siparişin kopyasını 200 ile alır. Bunu sağlayan bir mutex değil, `(customer_id, idempotency_key)` unique index'idir — kilit yoktur, yarış vardır ve yarışın hakemi atomiktir.

**2) İki hakem, iki ayrı felaketi önler.** Payments'taki unique index *çift tahsilatı*, orders'taki unique index *çift siparişi* imkânsızlaştırır. Kaybeden yol asla `Commit` çağırmaz — kalıcı stok düşüşünü yalnız kazanan yapar.

**3) Ödeme başarısızsa sipariş yazılmaz.** Order satırı ancak para gerçekten alındıysa doğar. "Ödenmemiş sipariş" diye bir ara durum veritabanında hiç var olmaz; rezervasyonlar telafi (compensation) ile geri bırakılır.

Payment'ın içinde de ayrı bir dayanıklılık katmanı vardır — sahte PSP bir **Polly boru hattının** arkasındadır:

```
ChargeAsync → Toplam timeout (3s)
              → Retry + jitter (geçici hatada)
                → Circuit breaker (PSP hasta ise devreyi aç)
                  → Bulkhead (eşzamanlılık sınırı)
                    → Deneme-başı timeout (1s)
                      → FakePspClient
```

### 5. Asenkron Kuyruk: Sipariş Ödendi → Bildirim

Checkout yanıtı döndükten sonra hikâye bitmez. `OrderPaid` gerçeği asenkron yayılır — ve bu yol, dağıtık sistemlerin iki klasik problemini el yapımı çözümlerle gösterir:

```
Ordering                                  RabbitMQ              Notification
   │                                          │                      │
   │ (checkout transaction'ı İÇİNDE)          │                      │
   │ outbox_messages satırı yazıldı           │                      │
   │                                          │                      │
   │ OutboxDispatcher (arka plan, ~1s poll)   │                      │
   │ pending satırları oku ────────publish──▶│                      │
   │ ProcessedOnUtc işaretle                  │ ──────consume──────▶ │
   │ (en-az-bir-kez: crash olursa             │                      │
   │  aynı mesaj TEKRAR yayınlanır)           │   INBOX kontrolü:    │
   │                                          │   processed_messages │
   │                                          │   PK("OrderPaid:{id}",│
   │                                          │      consumerType)   │
   │                                          │   23505 → idempotent │
   │                                          │   SKIP (kopya)       │
   │                                          │                      │
   │                                          │   e-posta + webhook  │
   │                                          │   kanalları (Strategy│
   │                                          │   + FaultInjecting   │
   │                                          │   Decorator)         │
   │                                          │                      │
   │                                          │   3 deneme başarısız │
   │                                          │   ise → *_error DLQ  │
```

- **Transactional Outbox**: "DB'ye yaz + mesaj yayınla" iki ayrı sistemdir ve ikisi birden atomik olamaz. Çözüm: olayı sipariş ile **aynı transaction'da** bir outbox tablosuna yazmak; ayrı bir dispatcher sonra yayınlar. Sipariş yazılıp olay kaybolamaz.
- **Idempotent Inbox**: En-az-bir-kez teslimat, kopya demektir. Tüketici her mesajı işlemeden önce `processed_messages` tablosuna iş anahtarını (`"OrderPaid:{OrderId}"` — MassTransit MessageId *değil*, çünkü el yapımı outbox her yeniden yayında yeni MessageId basar!) yazar. Primary key çakışırsa mesaj daha önce işlenmiştir → sessizce atlanır.
- **DLQ**: Teslimat 3 denemede de başarısızsa mesaj `order-paid-notification_error` kuyruğuna düşer — kaybolmaz, incelenmeyi bekler.

### 6. Geri Vites: İptal ve Telafi

```
Client            Ordering              Inventory        Payment
  │ POST /orders/{id}/cancel │              │               │
  │ ────────────▶ │                         │               │
  │               │ Order.Cancel (Paid→Cancelled matriste)  │
  │               │ (1) her satır için Return ─▶ OnHand+=q  │
  │               │     (best-effort)         │              │
  │               │ (2) RefundAsync ────────────────────────▶│
  │               │     KRİTİK: refund başarısızsa iptal     │
  │               │     PERSIST EDİLMEZ — sipariş Paid kalır │
  │               │ (3) Cancelled + OrderCancelled outbox    │
  │               │     TEK transaction'da                   │
  │  204          │                         │                │
  │ ◀──────────── │                         │                │
```

Sıralama bilinçlidir: müşterinin parası iade edilemeyecekse siparişi "iptal edildi" göstermek yalan olur. Stok iadesi ise best-effort'tur — başarısızsa envanter fazla görünür (undersell), asla eksik satılmaz (oversell).

### 7. Görünmez Bekçi: TTL Süpürücüsü

Checkout ortasında süreç çökerse rezervasyonlar askıda kalır. `ReservationTtlSweeper` (30 saniyede bir) süresi dolmuş Active rezervasyonları bulur ve Ordering'e sorar: *"Bu rezervasyonun siparişi ödendi mi?"*

- Ödendiyse → **Commit** (geç kalmış kesinleştirme; Available değişmez, oversell imkânsız)
- Sahipsizse → **Expire** (stok serbest bırakılır, tekrar satılabilir)

Bu, sistemin kendi kendini onaran (self-healing) parçasıdır.

---

## Hata Sözleşmesi: İstemcinin Yol Haritası

Tüm bu akışların istemciye görünen yüzü tek tip bir `ProblemDetails` gövdesidir — 400 de, 429 da, 500 de aynı kabuğu taşır (`correlationId` + `traceId` dahil):

```json
{
  "title": "Inventory.ConcurrencyConflict",
  "status": 409,
  "detail": "Stok bilgisi güncellendi, lütfen tekrar deneyin.",
  "code": "Inventory.ConcurrencyConflict",
  "retryable": true,
  "correlationId": "a1b2c3...",
  "traceId": "00-..."
}
```

İstemcinin karar tablosu üç satırdır:

| Yanıt | Anlamı | İstemci ne yapmalı? |
|---|---|---|
| `409` + `retryable: true` | Geçici çakışma (concurrency, kilit, PSP meşgul) | **AYNI Idempotency-Key ile hemen tekrar dene** |
| `409` + retryable yok | Terminal ret (declined, timeout, stok yok) | Durum kabul; yeni deneme = **yeni key** |
| `429` + `Retry-After` | Hız sınırı | `Retry-After` kadar bekle, sonra tekrar |

`retryable` bayrağı hata *tanımlandığı yerde* (domain error kataloğunda) beyan edilir; HTTP katmanı yalnız okur. İstemci kod string'i ezberlemez, boolean'a bakar.

---

## Kesişen Dertler (Cross-Cutting)

**Rate Limiting — katmanlı:** Global bir sliding-window (kullanıcı/IP bazlı) her şeyi kapsar; üstüne iki adlandırılmış policy biner. `auth` (IP bazlı, sıkı — brute-force kalkanı) ve `checkout` (kullanıcı bazlı, *burst-emici*: permit+queue ≥ 100, çünkü meşru idempotent 100-paralel burst'ü boğmak testin kendisini kırar). Limitler appsettings'ten ayarlanır; yük testi ortamında gevşetilir.

**Health Checks — liveness ≠ readiness:** `/health/live` hiçbir bağımlılığa bakmaz ("süreç yaşıyor mu?" — geçici DB kesintisi container'ı öldürmemeli). `/health/ready` ise Postgres `SELECT 1` + Redis `PING` + RabbitMQ bus probu koşar; biri düşerse 503 döner ve yük dengeleyici trafiği keser. El yapımı, sıfır üçüncü parti health paketi.

**Stok yarışı — üç strateji, tek config:** Inventory, rezervasyon yarışını üç değiştirilebilir stratejiyle çözer (Strategy pattern): `Naive` (bilerek yanlış — yarışı *göstermek* için), `OptimisticConcurrency` (PostgreSQL `xmin` row-version), `RedisLock` (dağıtık kilit). `Inventory:ReservationStrategy` config anahtarı ile seçilir; K6 flash-sale senaryosu her stratejide oversell sayacını ölçer. Doğru stratejilerde sonuç: **oversell = 0**.

---

## Kanıt: İddia Değil, Ölçüm

Bu mimarinin her iddiası bir teste bağlıdır:

- **350+ birim/entegrasyon testi** — Testcontainers ile gerçek PostgreSQL ve Redis'e karşı; yarış testleri gerçek paralel görevlerle.
- **32 mimari test** — modül sınırlarının bekçisi.
- **K6 checkout-smoke** — aynı key ile 100 paralel checkout → 1 sipariş, 1 tahsilat.
- **K6 flash-sale** — rampalı yük (yüzlerce istek/sn) altında 50 adetlik stok: satış + sold-out 409 + rate-limit 429 karışımı, teardown'da doğrulama: **oversell = 0**.

---

## Kapanış: Bu Mimariden Ne Taşınır?

1. **Sınır, disiplinden önce gelir.** "Dikkatli oluruz" ölçeklenmez; derleme zamanında kırmızıya dönen bir mimari test ölçeklenir.
2. **Hakem her zaman veritabanıdır.** Uygulama kodundaki hiçbir "önce kontrol et" yarışı kazanamaz; unique index kazanır.
3. **İdempotency bir özellik değil, sözleşmedir.** Key zorunluysa, kopya istek güvenliyse ve `retryable` makine-okunursa, istemci de dayanıklı olabilir.
4. **Asenkron dünyada iki tablo sizi kurtarır:** outbox (olay kaybolmasın) ve inbox (kopya işlenmesin).
5. **Monolitin içinde mikroservis disiplini mümkündür** — ve o disiplin sayesinde, bir gün gerçekten bölmek gerektiğinde bıçak izi çoktan çizilmiştir.

*Kod, testler ve K6 senaryolarıyla birlikte: ModularCommerce — .NET 10, EF Core, PostgreSQL, Redis, RabbitMQ/MassTransit, Polly, K6.*
