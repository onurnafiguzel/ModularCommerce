# Graph Report - .  (2026-07-18)

## Corpus Check
- 493 files · ~86,240 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3106 nodes · 5910 edges · 199 communities (191 shown, 8 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 344 edges (avg confidence: 0.81)
- Token cost: 239,258 input · 0 output

## Community Hubs (Navigation)
- Cross-Module Contract Interfaces
- Inventory TTL Sweeper & Persistence
- Catalog Product Domain
- C# Namespace Declarations
- Health Checks & Observability
- Host Bootstrap & Error Handling
- C# Namespace Declarations II
- Domain Events
- C# Namespace Declarations III
- Notification Delivery Channels
- C# Namespace Declarations IV
- Project & NuGet Dependencies
- Stock Reservation Aggregate
- Catalog Queries & Cache Decorator
- Product Reader Contract & Snapshots
- Cart Domain Aggregate
- Discovery Module Projects
- Refresh Token Persistence
- Payment Dev Endpoints
- Stock Endpoints & Handlers
- Repository Folder Structure
- Product Embedding Persistence
- OrderPaid Notification Consumer
- Cart Cache & Repository Decorator
- Identity Signup Flow
- Identity User & Email Domain
- Reservation Strategy Implementations
- Discovery Indexing Pipeline
- Notification Idempotent Inbox
- Cart API Endpoints
- Identity Infrastructure
- Payment Domain
- Inventory Infrastructure
- Payment Contracts
- Inventory Infrastructure
- Catalog Infrastructure
- Cart Infrastructure
- Notification Infrastructure
- Ordering Application
- Ordering Fragment
- Payment Domain
- Discovery Application
- Cart Application
- Cart Domain
- Identity Application
- Identity Application
- Discovery Api
- Catalog Application
- Identity Infrastructure
- Catalog Infrastructure
- Identity Application
- Identity Application
- Catalog Infrastructure
- Misc Fragment
- Ordering Infrastructure
- Ordering Infrastructure
- Catalog Infrastructure
- Cart Application
- Cart Infrastructure
- Shared Domain
- Notification Fragment
- Ordering Infrastructure
- Payment Infrastructure
- Inventory Fragment
- Cart Fragment
- Shipping Contracts
- Identity Infrastructure
- Ordering Domain
- Test Fragment
- Discovery Application
- Notification Api
- Discovery Infrastructure
- Catalog Application
- Discovery Application
- Test Fragment
- Payment Fragment
- Catalog Infrastructure
- Payment Infrastructure
- Inventory Infrastructure
- Payment Application
- Payment Application
- Notification Infrastructure
- Cart Infrastructure
- Identity Infrastructure
- Inventory Application
- Ordering Infrastructure
- Payment Application
- Inventory Domain
- Notification Fragment
- Cart Fragment
- Shared Contracts
- Payment Fragment
- Test Fragment
- Payment Fragment
- Payment Domain
- Ordering Contracts
- Ordering Domain
- Shared Fragment
- Catalog Application
- Inventory Application
- Ordering Domain
- Ordering Infrastructure
- Payment Infrastructure
- Payment Domain
- Inventory Fragment
- Ordering Fragment
- Inventory Infrastructure
- Ordering Infrastructure
- Inventory Fragment
- Cart Infrastructure
- Identity Domain
- Inventory Domain
- Discovery Infrastructure
- Cart Application
- Cart Application
- Inventory Domain
- Notification Application
- Ordering Fragment
- Shipping Api
- Cart Infrastructure
- Catalog Infrastructure
- Discovery Infrastructure
- Inventory Application
- Ordering Infrastructure
- Catalog Domain
- Cart Fragment
- Cart Fragment
- Inventory Domain
- Notification Fragment
- Cart Contracts
- Inventory Fragment
- Catalog Infrastructure
- Catalog Infrastructure
- Payment Infrastructure
- Shared Infrastructure
- Inventory Domain
- Inventory Domain
- Ordering Fragment
- Shared Infrastructure
- Catalog Infrastructure
- Ordering Infrastructure
- Inventory Fragment
- Cart Fragment
- Shared Fragment
- Misc Fragment
- Catalog Fragment
- Cart Fragment
- Shared Fragment
- Cart Fragment
- Shared Infrastructure
- Shared Infrastructure
- Shared Infrastructure
- Notification Fragment
- Cart Application
- Discovery Api
- Ordering Domain
- Payment Domain
- Cart Fragment
- Catalog Application
- Payment Fragment
- Payment Domain
- Cart Infrastructure
- Shared Infrastructure
- Catalog Infrastructure
- Misc Fragment
- Identity Fragment
- Discovery Application
- Test Fragment
- Cart Api
- Catalog Api
- Catalog Infrastructure
- Discovery Infrastructure
- Identity Api
- Inventory Api
- Inventory Infrastructure
- Notification Api
- Ordering Api
- Ordering Infrastructure
- Payment Api
- Shared Infrastructure
- Ordering Fragment
- Shared Infrastructure
- Payment Fragment
- Ordering Infrastructure
- Shared Infrastructure
- Inventory Fragment
- Inventory Infrastructure
- Misc Fragment
- Identity Fragment
- Catalog Fragment
- Inventory Fragment
- Payment Fragment
- Shipping Fragment
- Discovery Contracts
- Payment Fragment
- Catalog Infrastructure
- Ordering Domain
- Identity Fragment

## God Nodes (most connected - your core abstractions)
1. `Result` - 124 edges
2. `ModularCommerce.Shared.Kernel` - 113 edges
3. `ModularCommerce.Inventory.Domain.Stock` - 39 edges
4. `Order` - 38 edges
5. `Payment` - 33 edges
6. `Reservation` - 28 edges
7. `ModularCommerce.Ordering.Domain.Orders` - 28 edges
8. `Error` - 28 edges
9. `ModularCommerce.Identity.Domain.Users` - 27 edges
10. `Cart` - 26 edges

## Surprising Connections (you probably didn't know these)
- `IEmbeddingService (Fake / Http provider)` --semantically_similar_to--> `payment-psp Polly Resilience Pipeline`  [INFERRED] [semantically similar]
  CLAUDE.md → docs/hafta-7-notlar.md
- `IndexProductHandler (SHA-256 hash skip)` --semantically_similar_to--> `processed_messages Handmade Inbox`  [INFERRED] [semantically similar]
  CLAUDE.md → docs/hafta-10-notlar.md
- `Cart.Contracts (dış dünyaya açık tek kapı)` --implements--> `Modül Sınır Kuralı (yalnız Contracts)`  [INFERRED]
  src/Modules/Cart/ModularCommerce.Cart.Contracts/README.md → docs/medium-mimari-makale.md
- `Ordering.Contracts (dış dünyaya açık tek kapı)` --implements--> `Modül Sınır Kuralı (yalnız Contracts)`  [INFERRED]
  src/Modules/Ordering/ModularCommerce.Ordering.Contracts/README.md → docs/medium-mimari-makale.md
- `Catalog.Contracts (dış dünyaya açık tek kapı)` --implements--> `Modül Sınır Kuralı (yalnız Contracts)`  [INFERRED]
  src/Modules/Catalog/ModularCommerce.Catalog.Contracts/README.md → docs/medium-mimari-makale.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Synchronous Checkout: reserve → charge → persist → commit** — docs_hafta_6_notlar_icartservice, docs_hafta_6_notlar_iproductreader, docs_hafta_6_notlar_istockreservationservice, docs_hafta_7_notlar_two_arbiters, docs_hafta_7_notlar_commit_primitive, docs_hafta_6_notlar_order_state_machine [EXTRACTED 1.00]
- **Outbox → RabbitMQ → Idempotent Inbox → DLQ chain** — docs_hafta_8_notlar_interceptor, docs_hafta_8_notlar_outbox_dispatcher, docs_hafta_8_notlar_at_least_once, docs_hafta_10_notlar_business_key_idempotency, docs_hafta_10_notlar_processed_messages_inbox, docs_hafta_10_notlar_dlq [EXTRACTED 1.00]
- **Oversell=0 invariant preserved across reserve/commit/expire/reconcile** — docs_hafta_3_notlar_xmin_token, docs_hafta_4_notlar_redis_distributed_lock, docs_hafta_7_notlar_commit_primitive, docs_hafta_9_notlar_p2_reconcile, docs_hafta_9_notlar_stock_item_expire, readme_flash_sale_proof [INFERRED 0.95]
- **Checkout'ta Tam-Bir-Kez Zinciri** — docs_medium_mimari_makale_checkout_flow, docs_medium_mimari_makale_idempotency_key_contract, docs_medium_mimari_makale_database_as_arbiter, docs_medium_mimari_makale_polly_pipeline, docs_medium_mimari_makale_transactional_outbox [EXTRACTED 1.00]
- **Modül Sınırının Üç Katmanlı Savunması** — docs_medium_mimari_makale_module_boundary_tests, docs_medium_mimari_makale_schema_isolation, docs_medium_mimari_makale_module_boundary_rule, docs_medium_mimari_makale_three_layer_boundary_defense [EXTRACTED 1.00]
- **Oversell=0 Kanıt Zinciri (gereksinim → strateji → yük testi)** — docs_requirements_inventory_requirements, docs_rezervasyon_akisi_xmin_optimistic_concurrency, docs_rezervasyon_akisi_check_then_act_race, tests_loadtests_readme_inventory_oversell_scenario, tests_loadtests_readme_flash_sale_scenario [EXTRACTED 1.00]

## Communities (199 total, 8 thin omitted)

### Community 0 - "Cross-Module Contract Interfaces"
Cohesion: 0.06
Nodes (43): CartLineDto, CancellationToken, Guid, IReadOnlyList, Task, ICartService, CancellationToken, Guid (+35 more)

### Community 1 - "Inventory TTL Sweeper & Persistence"
Cohesion: 0.06
Nodes (35): BackgroundService, CancellationToken, int, Task, TimeSpan, ReservationTtlSweeper, DbSet, ModelBuilder (+27 more)

### Community 2 - "Catalog Product Domain"
Cohesion: 0.05
Nodes (36): ModularCommerce.Catalog.Infrastructure.Persistence.Configurations, CancellationToken, Guid, IEnumerable, Task, IProductRepository, DateTime, GeneratedRegex (+28 more)

### Community 3 - "C# Namespace Declarations"
Cohesion: 0.10
Nodes (22): ModularCommerce.Inventory.Infrastructure.Locking, ModularCommerce.Inventory.Infrastructure.Persistence, ModularCommerce.Inventory.IntegrationTests, ModularCommerce.Inventory.Application.Reservations.Common, ModularCommerce.Inventory.Infrastructure.Persistence.Repositories, ModularCommerce.Inventory.UnitTests.Application, ModularCommerce.Inventory.Application.Reservations.ReserveStock, ModularCommerce.Inventory.Application.Stock.GetStock (+14 more)

### Community 4 - "Health Checks & Observability"
Cohesion: 0.06
Nodes (30): ModularCommerce.Shared.Infrastructure.Observability, HealthReport, IHealthCheck, HttpContext, string, Task, CorrelationIdMiddleware, IEndpointRouteBuilder (+22 more)

### Community 5 - "Host Bootstrap & Error Handling"
Cohesion: 0.05
Nodes (31): CheckoutLimit, ModularCommerce.Shared.Infrastructure.Messaging, ModularCommerce.Shared.Infrastructure.ExceptionHandling, ModularCommerce.Shared.Infrastructure.RateLimiting, ModularCommerce.Shared.Infrastructure.Redis, IBusRegistrationConfigurator, IExceptionHandler, OnRejectedContext (+23 more)

### Community 6 - "C# Namespace Declarations II"
Cohesion: 0.07
Nodes (22): ModularCommerce.Ordering.Application.Orders.Common, ModularCommerce.Cart.Contracts, ModularCommerce.Inventory.Contracts, ModularCommerce.Ordering.Application.Orders.GetOrder, ModularCommerce.Ordering.UnitTests.Domain, ModularCommerce.Ordering.Application.Abstractions, ModularCommerce.Ordering.Api.Endpoints, ModularCommerce.Ordering.Infrastructure.Persistence.Repositories (+14 more)

### Community 7 - "Domain Events"
Cohesion: 0.07
Nodes (20): ModularCommerce.Shared.Kernel, ModularCommerce.Catalog.UnitTests.Domain, ProductCreated, ProductUpdated, UserRegistered, ProductSoldOut, StockCommitted, StockExpired (+12 more)

### Community 8 - "C# Namespace Declarations III"
Cohesion: 0.11
Nodes (18): ModularCommerce.Identity.UnitTests.Application, ModularCommerce.Shared.Infrastructure.Auth, ModularCommerce.Identity.Application.Auth.Refresh, ModularCommerce.Identity.Application.Auth.Common, ModularCommerce.Identity.Infrastructure.Persistence, ModularCommerce.Identity.Domain.Users, ModularCommerce.Identity.Application.Auth.Login, ModularCommerce.Identity.UnitTests.Infrastructure (+10 more)

### Community 9 - "Notification Delivery Channels"
Cohesion: 0.09
Nodes (24): ModularCommerce.Notification.UnitTests, ModularCommerce.Notification.Application.Abstractions, ModularCommerce.Notification.Infrastructure.Channels, ModularCommerce.Notification.Application.Delivery, CancellationToken, Task, INotificationChannel, NotificationMessage (+16 more)

### Community 10 - "C# Namespace Declarations IV"
Cohesion: 0.11
Nodes (16): ModularCommerce.Catalog.Infrastructure.Persistence.Repositories, ModularCommerce.Catalog.Infrastructure.Persistence, ModularCommerce.Catalog.Domain.Products, ModularCommerce.Catalog.Contracts, ModularCommerce.Catalog.Infrastructure.Persistence.Queries, ModularCommerce.Catalog.Application.Abstractions, ModularCommerce.Catalog.UnitTests.Application, ModularCommerce.Catalog.Application.Products.GetProductById (+8 more)

### Community 11 - "Project & NuGet Dependencies"
Cohesion: 0.08
Nodes (25): Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.EntityFrameworkCore.Design, NetArchTest.Rules, Microsoft.NET.Sdk.Web, Serilog.AspNetCore, Microsoft.NET.Sdk, Microsoft.NET.Sdk, MassTransit.RabbitMQ (+17 more)

### Community 12 - "Stock Reservation Aggregate"
Cohesion: 0.09
Nodes (16): ModularCommerce.Inventory.Infrastructure.Persistence.Configurations, DateTime, Guid, TimeSpan, Reservation, ReservationStatus, DateTime, StockItem (+8 more)

### Community 13 - "Catalog Queries & Cache Decorator"
Cohesion: 0.11
Nodes (21): CancellationToken, Guid, Task, IProductQueries, ProductDetailResponse, CancellationToken, Guid, Task (+13 more)

### Community 14 - "Product Reader Contract & Snapshots"
Cohesion: 0.09
Nodes (23): CancellationToken, Guid, IReadOnlyCollection, IReadOnlyList, Task, IProductReader, ProductSnapshotDto, CancellationToken (+15 more)

### Community 15 - "Cart Domain Aggregate"
Cohesion: 0.10
Nodes (16): ModularCommerce.Cart.Infrastructure.Caching, ModularCommerce.Cart.UnitTests.Persistence, ModularCommerce.Cart.UnitTests.Caching, ModularCommerce.Cart.Infrastructure.Persistence, ModularCommerce.Cart.Domain.Carts, ModularCommerce.Cart.UnitTests.Domain, Guid, IEnumerable (+8 more)

### Community 16 - "Discovery Module Projects"
Cohesion: 0.07
Nodes (26): FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore, Microsoft.Extensions.Resilience, Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL (+18 more)

### Community 17 - "Refresh Token Persistence"
Cohesion: 0.11
Nodes (14): ModularCommerce.Identity.Infrastructure.Persistence.Configurations, DateTime, Guid, RefreshToken, EntityTypeBuilder, RefreshTokenConfiguration, CancellationToken, Task (+6 more)

### Community 18 - "Payment Dev Endpoints"
Cohesion: 0.14
Nodes (14): ModularCommerce.Payment.IntegrationTests, ModularCommerce.Payment.Infrastructure.Persistence.Configurations, ModularCommerce.Payment.Infrastructure.Persistence, ModularCommerce.Payment.Api.Endpoints, ModularCommerce.Payment.Infrastructure.Psp, ModularCommerce.Payment.Infrastructure.ContractAdapters, ModularCommerce.Payment.Application.Abstractions, ModularCommerce.Payment.Domain.Payments (+6 more)

### Community 19 - "Stock Endpoints & Handlers"
Cohesion: 0.11
Nodes (14): IEndpointRouteBuilder, StockEndpoints, SetStockCommand, CancellationToken, Task, SetStockHandler, CancellationToken, Guid (+6 more)

### Community 20 - "Repository Folder Structure"
Cohesion: 0.09
Nodes (7): Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk

### Community 21 - "Product Embedding Persistence"
Cohesion: 0.12
Nodes (16): ModularCommerce.Discovery.Infrastructure.Persistence.Configurations, IClassFixture, VectorMatch, DateTime, Guid, ProductEmbedding, EntityTypeBuilder, ProductEmbeddingConfiguration (+8 more)

### Community 22 - "OrderPaid Notification Consumer"
Cohesion: 0.10
Nodes (17): ModularCommerce.Ordering.Contracts.IntegrationEvents, ModularCommerce.Notification.Api.Consumers, IConsumer, ConsumeContext, Task, OrderPaidNotificationConsumer, IConsumerConfigurator, IReceiveEndpointConfigurator (+9 more)

### Community 23 - "Cart Cache & Repository Decorator"
Cohesion: 0.19
Nodes (14): CancellationToken, Cart, Guid, Task, ICartCache, CancellationToken, Cart, Guid (+6 more)

### Community 24 - "Identity Signup Flow"
Cohesion: 0.12
Nodes (14): IPasswordHasher, CancellationToken, Task, SignupHandler, SignupResponse, Fact, InlineData, Task (+6 more)

### Community 25 - "Identity User & Email Domain"
Cohesion: 0.12
Nodes (14): int, Email, CancellationToken, Guid, Task, IUserRepository, DateTime, User (+6 more)

### Community 26 - "Reservation Strategy Implementations"
Cohesion: 0.11
Nodes (19): CancellationToken, Guid, Task, IReservationStrategy, CancellationToken, Task, ReserveStockHandler, NaiveReservationStrategy (+11 more)

### Community 27 - "Discovery Indexing Pipeline"
Cohesion: 0.11
Nodes (14): ModularCommerce.Discovery.IntegrationTests, ModularCommerce.Discovery.Infrastructure.Embedding, ModularCommerce.Discovery.Api, ModularCommerce.Discovery.Application.Indexing, ModularCommerce.Discovery.Application.Abstractions, ModularCommerce.Discovery.UnitTests.Search, ModularCommerce.Discovery.Infrastructure.Persistence, ModularCommerce.Discovery.UnitTests.Indexing (+6 more)

### Community 28 - "Notification Idempotent Inbox"
Cohesion: 0.10
Nodes (18): ModularCommerce.Notification.Domain.Notifications, ModularCommerce.Notification.Infrastructure.Persistence.Configurations, ModularCommerce.Notification.Domain.Inbox, DateTime, Guid, ProcessedMessage, DateTime, Guid (+10 more)

### Community 29 - "Cart API Endpoints"
Cohesion: 0.10
Nodes (15): ModularCommerce.Cart.Application.Carts.RemoveItem, ModularCommerce.Cart.Application.Carts.GetCart, ModularCommerce.Cart.Application.Carts.UpdateItemQuantity, ModularCommerce.Cart.UnitTests.Application, ModularCommerce.Cart.Api, ModularCommerce.Cart.Api.Endpoints, ModularCommerce.Cart.Application.Carts.AddItem, IEndpointRouteBuilder (+7 more)

### Community 30 - "Identity Infrastructure"
Cohesion: 0.13
Nodes (13): ModularCommerce.Identity.IntegrationTests.Fixtures, PasswordHasher, DbSet, ModelBuilder, string, IdentityDbContext, int, IdentityPasswordHasher (+5 more)

### Community 31 - "Payment Domain"
Cohesion: 0.09
Nodes (17): ModularCommerce.Payment.UnitTests.Domain, DateTime, Guid, int, IReadOnlyList, List, Payment, DateTime (+9 more)

### Community 32 - "Inventory Infrastructure"
Cohesion: 0.10
Nodes (18): IAsyncDisposable, CancellationToken, Task, TimeSpan, IDistributedLock, ILockHandle, CancellationToken, string (+10 more)

### Community 33 - "Payment Contracts"
Cohesion: 0.20
Nodes (11): IPaymentMethodStrategy, ChargeRequest, CancellationToken, Task, IPaymentService, PaymentResultDto, RefundRequest, RefundResultDto (+3 more)

### Community 34 - "Inventory Infrastructure"
Cohesion: 0.09
Nodes (11): ModularCommerce.Inventory.Infrastructure.Persistence.Migrations, MigrationBuilder, ModelBuilder, InitialInventorySchema, InitialInventorySchema, MigrationBuilder, ModelBuilder, AddReservationExpiryIndex (+3 more)

### Community 35 - "Catalog Infrastructure"
Cohesion: 0.12
Nodes (15): SaveChangesInterceptor, CancellationToken, DbContext, DbContextEventData, InterceptionResult, JsonSerializerOptions, ValueTask, DomainEventToOutboxInterceptor (+7 more)

### Community 36 - "Cart Infrastructure"
Cohesion: 0.16
Nodes (14): CancellationToken, Cart, Guid, JsonSerializerOptions, Task, TimeSpan, RedisCartCache, Cart (+6 more)

### Community 37 - "Notification Infrastructure"
Cohesion: 0.09
Nodes (18): MassTransit.RabbitMQ, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk (+10 more)

### Community 38 - "Ordering Application"
Cohesion: 0.09
Nodes (19): CancellationToken, Guid, IReadOnlyList, Task, IOrderQueries, OrderLineResponse, OrderResponse, OrderStatusChangeResponse (+11 more)

### Community 39 - "Ordering Fragment"
Cohesion: 0.09
Nodes (20): FluentAssertions, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql, xunit, xunit.runner.visualstudio, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk (+12 more)

### Community 40 - "Payment Domain"
Cohesion: 0.18
Nodes (9): CancellationToken, Task, CancellationToken, Guid, Task, CancellationToken, Guid, Task (+1 more)

### Community 41 - "Discovery Application"
Cohesion: 0.19
Nodes (12): CancellationToken, Guid, IReadOnlyList, Task, IProductVectorRepository, CancellationToken, Task, IndexProductHandler (+4 more)

### Community 42 - "Cart Application"
Cohesion: 0.13
Nodes (13): ModularCommerce.Cart.Application.Carts.Common, Cart, Guid, string, CartItemResponse, CartResponse, CancellationToken, Guid (+5 more)

### Community 43 - "Cart Domain"
Cohesion: 0.22
Nodes (4): Fact, InlineData, Theory, CartTests

### Community 44 - "Identity Application"
Cohesion: 0.18
Nodes (11): Guid, AccessTokenResult, ITokenService, RefreshTokenResult, CancellationToken, Task, RefreshHandler, Fact (+3 more)

### Community 45 - "Identity Application"
Cohesion: 0.10
Nodes (11): AbstractValidator, LoginCommand, LoginCommandValidator, LogoutCommand, LogoutCommandValidator, RefreshCommand, RefreshCommandValidator, SignupCommand (+3 more)

### Community 46 - "Discovery Api"
Cohesion: 0.14
Nodes (13): ConsumerDefinition, ModularCommerce.Catalog.Contracts.IntegrationEvents, ModularCommerce.Discovery.Api.Consumers, ProductCreated, ProductUpdated, CancellationToken, ConsumeContext, Task (+5 more)

### Community 47 - "Catalog Application"
Cohesion: 0.11
Nodes (12): ModularCommerce.Catalog.Application.Products.CreateProduct, ModularCommerce.Catalog.Application.Products.UpdateProduct, ModularCommerce.Catalog.Api.Endpoints, IEndpointRouteBuilder, ProductEndpoints, UpdateProductRequest, CreateProductCommand, CreateProductCommandValidator (+4 more)

### Community 48 - "Identity Infrastructure"
Cohesion: 0.11
Nodes (15): Microsoft.Extensions.Identity.Core, Microsoft.IdentityModel.JsonWebTokens, FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL (+7 more)

### Community 49 - "Catalog Infrastructure"
Cohesion: 0.11
Nodes (15): FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, MassTransit.RabbitMQ, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, StackExchange.Redis (+7 more)

### Community 50 - "Identity Application"
Cohesion: 0.17
Nodes (10): CancellationToken, Guid, Task, TokenResponse, CancellationToken, Task, LoginHandler, Fact (+2 more)

### Community 51 - "Identity Application"
Cohesion: 0.17
Nodes (11): IEndpointRouteBuilder, AuthEndpoints, CancellationToken, Task, LogoutHandler, CancellationToken, Task, IRefreshTokenRepository (+3 more)

### Community 52 - "Catalog Infrastructure"
Cohesion: 0.18
Nodes (11): bool, CancellationToken, JsonSerializerOptions, Task, TimeSpan, RedisProductCache, Fact, IDatabase (+3 more)

### Community 53 - "Misc Fragment"
Cohesion: 0.11
Nodes (19): Mandatory Idempotency-Key Header, ProblemMapping / ResultExtensions.ToHttpResult, Retryable-409 Client Contract, 429 RateLimited Client Contract, Burst-Absorbing Checkout Policy, Layered Rate Limiting, ErrorType → HTTP Status Mapping, GlobalExceptionHandler (+11 more)

### Community 54 - "Ordering Infrastructure"
Cohesion: 0.15
Nodes (11): ModularCommerce.Ordering.IntegrationTests, ModularCommerce.Ordering.Infrastructure.Persistence, ModularCommerce.Ordering.IntegrationTests.Fixtures, ModularCommerce.Ordering.UnitTests.Outbox, ModularCommerce.Ordering.Infrastructure.Outbox, ModularCommerce.Ordering.Infrastructure.ContractAdapters, OrderingPostgresCollection, PostgresContainerFixture (+3 more)

### Community 55 - "Ordering Infrastructure"
Cohesion: 0.12
Nodes (9): ModularCommerce.Ordering.Infrastructure.Persistence.Migrations, MigrationBuilder, ModelBuilder, InitialOrderingSchema, InitialOrderingSchema, MigrationBuilder, ModelBuilder, AddOutboxMessages (+1 more)

### Community 56 - "Catalog Infrastructure"
Cohesion: 0.11
Nodes (9): ModularCommerce.Catalog.Infrastructure.Persistence.Migrations, ModelBuilder, InitialCatalogSchema, MigrationBuilder, ModelBuilder, AddCatalogOutbox, AddCatalogOutbox, ModelBuilder (+1 more)

### Community 57 - "Cart Application"
Cohesion: 0.20
Nodes (11): CancellationToken, Guid, Task, RemoveItemHandler, CancellationToken, Guid, Task, ICartRepository (+3 more)

### Community 58 - "Cart Infrastructure"
Cohesion: 0.12
Nodes (14): FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, StackExchange.Redis, Microsoft.NET.Sdk (+6 more)

### Community 59 - "Shared Domain"
Cohesion: 0.15
Nodes (8): Guid, ProductErrors, DiscoveryErrors, IdentityErrors, Guid, InventoryErrors, MoneyErrors, Error

### Community 60 - "Notification Fragment"
Cohesion: 0.12
Nodes (18): Catalog Transactional Outbox, Discovery Module (AI Semantic Search), IEmbeddingService (Fake / Http provider), IndexProductHandler (SHA-256 hash skip), Notification Module, Ordering Module, pgvector Embedding Storage, ProductChangedConsumer (Discovery) (+10 more)

### Community 61 - "Ordering Infrastructure"
Cohesion: 0.12
Nodes (14): Microsoft.Extensions.Logging.Abstractions, FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, MassTransit.RabbitMQ, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk (+6 more)

### Community 62 - "Payment Infrastructure"
Cohesion: 0.22
Nodes (10): string, PspOptions, Fact, Task, ChargeIdempotencyRaceTests, Fact, Task, DeclinedReplayTests (+2 more)

### Community 63 - "Inventory Fragment"
Cohesion: 0.16
Nodes (10): ModularCommerce.TestKit, ModularCommerce.Shared.IntegrationTests.Fixtures, ICollectionFixture, CartRedisCollection, RedisContainerFixture, PostgresCollection, PostgresContainerFixture, PostgresRedisCollection (+2 more)

### Community 64 - "Cart Fragment"
Cohesion: 0.12
Nodes (17): ChangeTracker.Clear Before Compensation, Secondary Empty-Cart Race Guard, ICartService Contract, IProductReader.GetByIdsAsync Contract, IStockReservationService Contract, No Distributed Transaction (deliberate), Release Compensation with Jittered Retry, StockItem.Commit Primitive (+9 more)

### Community 65 - "Shipping Contracts"
Cohesion: 0.12
Nodes (17): Beş Projeli Modül Anatomisi (Domain/Application/Infrastructure/Api/Contracts), Health Checks (liveness ≠ readiness), IModule composition root deseni, Modüler Monolit Mimarisi, Modül Sınır Kuralı (yalnız Contracts), ModuleBoundaryTests (NetArchTest mimari testleri), Şema İzolasyonu (modül başına PostgreSQL şeması), Üç Katmanlı Sınır Savunması (test + şema + review) (+9 more)

### Community 66 - "Identity Infrastructure"
Cohesion: 0.20
Nodes (9): JsonWebTokenHandler, Guid, JwtTokenService, int, string, JwtOptions, Fact, Task (+1 more)

### Community 67 - "Ordering Domain"
Cohesion: 0.21
Nodes (7): DateTime, Dictionary, Guid, int, IReadOnlyList, List, Order

### Community 68 - "Test Fragment"
Cohesion: 0.24
Nodes (15): addToCart(), authenticate(), checkoutLatency(), checkoutOnce(), checkouts201, checkoutUntilTerminal(), customerIdFromToken(), idempotencyBurst() (+7 more)

### Community 69 - "Discovery Application"
Cohesion: 0.13
Nodes (10): ModularCommerce.Discovery.Api.Endpoints, ModularCommerce.Discovery.Application.Search, ModularCommerce.Discovery.Application.Common, IEndpointRouteBuilder, SearchEndpoints, SearchRequest, SearchResultResponse, SearchQuery (+2 more)

### Community 70 - "Notification Api"
Cohesion: 0.17
Nodes (10): ModularCommerce.Notification.Api, ModularCommerce.Notification.Infrastructure, ModularCommerce.Notification.IntegrationTests, ModularCommerce.Notification.IntegrationTests.Fixtures, ModularCommerce.Notification.Api.Endpoints, ModularCommerce.Notification.Infrastructure.Persistence, IEndpointRouteBuilder, NotificationDevEndpoints (+2 more)

### Community 71 - "Discovery Infrastructure"
Cohesion: 0.21
Nodes (8): ModularCommerce.Discovery.UnitTests.Embedding, CancellationToken, IEnumerable, Task, FakeEmbeddingService, Fact, Task, FakeEmbeddingServiceTests

### Community 72 - "Catalog Application"
Cohesion: 0.16
Nodes (10): PagedResponse, ProductSummaryResponse, CancellationToken, Task, GetProductsHandler, GetProductsQuery, CancellationToken, Guid (+2 more)

### Community 73 - "Discovery Application"
Cohesion: 0.23
Nodes (10): CancellationToken, Task, IEmbeddingService, CancellationToken, IReadOnlyList, Task, SearchProductsHandler, Fact (+2 more)

### Community 74 - "Test Fragment"
Cohesion: 0.17
Nodes (13): addToCart(), authenticate(), checkoutOnce(), checkouts201, checkoutUntilTerminal(), flashSale(), HOT_STOCK, options (+5 more)

### Community 75 - "Payment Fragment"
Cohesion: 0.19
Nodes (13): addToCart(), authenticate(), breakerLoad(), checkouts201, classify(), declined409, declinedLeak(), inflight409 (+5 more)

### Community 76 - "Catalog Infrastructure"
Cohesion: 0.19
Nodes (8): ModularCommerce.Catalog.UnitTests.Outbox, Dictionary, IntegrationEvent, string, Type, CatalogIntegrationEventRegistry, Fact, CatalogIntegrationEventRegistryTests

### Community 77 - "Payment Infrastructure"
Cohesion: 0.14
Nodes (7): ModularCommerce.Payment.Infrastructure.Persistence.Migrations, ModelBuilder, InitialPaymentSchema, MigrationBuilder, ModelBuilder, AddPaymentRefund, AddPaymentRefund

### Community 78 - "Inventory Infrastructure"
Cohesion: 0.13
Nodes (13): Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, StackExchange.Redis, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk, NSubstitute, StackExchange.Redis (+5 more)

### Community 79 - "Payment Application"
Cohesion: 0.17
Nodes (10): CancellationToken, Task, IPspClient, PspResult, CancellationToken, Task, TimeSpan, FakePspClient (+2 more)

### Community 80 - "Payment Application"
Cohesion: 0.15
Nodes (10): Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk, NSubstitute, xunit (+2 more)

### Community 81 - "Notification Infrastructure"
Cohesion: 0.15
Nodes (7): ModularCommerce.Notification.Infrastructure.Migrations, MigrationBuilder, ModelBuilder, InitialNotification, InitialNotification, ModelBuilder, NotificationDbContextModelSnapshot

### Community 82 - "Cart Infrastructure"
Cohesion: 0.15
Nodes (7): ModularCommerce.Cart.Infrastructure.Migrations, MigrationBuilder, ModelBuilder, InitialCartSchema, InitialCartSchema, ModelBuilder, CartDbContextModelSnapshot

### Community 83 - "Identity Infrastructure"
Cohesion: 0.15
Nodes (7): ModularCommerce.Identity.Infrastructure.Persistence.Migrations, MigrationBuilder, ModelBuilder, InitialIdentitySchema, InitialIdentitySchema, ModelBuilder, IdentityDbContextModelSnapshot

### Community 84 - "Inventory Application"
Cohesion: 0.15
Nodes (10): FluentValidation.DependencyInjectionExtensions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk, NSubstitute, xunit (+2 more)

### Community 85 - "Ordering Infrastructure"
Cohesion: 0.23
Nodes (7): Dictionary, IntegrationEvent, string, Type, OrderingIntegrationEventRegistry, Fact, OrderingIntegrationEventRegistryTests

### Community 86 - "Payment Application"
Cohesion: 0.24
Nodes (9): CancellationToken, IReadOnlyList, Task, PspAttemptLog, PspChargeOutcome, PspChargeRequest, CancellationToken, List (+1 more)

### Community 87 - "Inventory Domain"
Cohesion: 0.29
Nodes (4): Fact, InlineData, Theory, StockItemTests

### Community 88 - "Notification Fragment"
Cohesion: 0.25
Nodes (8): CancellationToken, Fact, Guid, int, string, Task, CountingChannel, ProcessorIdempotencyTests

### Community 89 - "Cart Fragment"
Cohesion: 0.15
Nodes (13): CachingCartRepository Decorator, CachingProductQueries Decorator, Cart Module, Catalog Module, PostgresCartRepository (cart source of truth), RedisProductCache / IProductCache, Redis Service, RedisHealthCheck (PING) (+5 more)

### Community 90 - "Shared Contracts"
Cohesion: 0.15
Nodes (13): Module Boundary Rule (Contracts-only), Shared.Kernel (Entity, Result, Error), Schema-per-Module Persistence Pattern, Reservation Entity (insert-only, TTL +5min), StockItem Aggregate (Reserved counter), Contracts_should_be_self_contained Architecture Test, Append-Only payment_attempts Audit, Outbox is Module-Local (ordering schema) (+5 more)

### Community 91 - "Payment Fragment"
Cohesion: 0.17
Nodes (13): Payment Module, AddEventBus-before-Register Startup Fix, Critical Finding: Business Key, Not MessageId, OrderPaidNotificationConsumerDefinition (retry policy), Dead-Letter Queue (_error queue), FaultInjectingChannel Decorator, Circuit Breaker Open/Close Log Proof, FakePspClient + Psp Knobs (+5 more)

### Community 92 - "Test Fragment"
Cohesion: 0.24
Nodes (6): ModularCommerce.ArchitectureTests, MemberData, string, Theory, TheoryData, ModuleBoundaryTests

### Community 93 - "Payment Fragment"
Cohesion: 0.28
Nodes (8): CustomerId, Key, Fact, Guid, PaymentDbContext, PaymentService, Task, RefundTests

### Community 94 - "Payment Domain"
Cohesion: 0.15
Nodes (13): AddModuleDbContext Extension, Order State Machine (AllowedTransitions matrix), order_status_history Audit Table, Domain Event ≠ Integration Event, OrderingIntegrationEventRegistry, DomainEventToOutboxInterceptor, Stable String Discriminator (not AssemblyQualifiedName), CancelOrderHandler (comprehensive cancel) (+5 more)

### Community 95 - "Ordering Contracts"
Cohesion: 0.17
Nodes (13): IOrderReservationReconciler, Middleware Pipeline (CorrelationId→Serilog→ExceptionHandler→Auth→RateLimiter), Katmanlı Rate Limiting (global + auth + checkout), ReservationTtlSweeper (self-healing bekçi), Ters Yön Bağımlılığı İstisnası (Inventory.Infrastructure → Ordering.Contracts), Genel NFR Hedefleri (p95, oversell=0, duplicate ödeme=0), Kanıt Yükümlülükleri (definition of done), Cross-cutting Altyapı Feature ile Gelir İlkesi (+5 more)

### Community 96 - "Ordering Domain"
Cohesion: 0.29
Nodes (5): HashSet, Fact, MemberData, Theory, OrderStateMachineTests

### Community 97 - "Shared Fragment"
Cohesion: 0.18
Nodes (9): IAsyncLifetime, NpgsqlDataSource, PostgreSqlContainer, Task, PgVectorFixture, PostgreSqlContainer, RedisContainer, Task (+1 more)

### Community 98 - "Catalog Application"
Cohesion: 0.21
Nodes (6): int, GetProductsQueryValidator, Fact, InlineData, Theory, GetProductsQueryValidatorTests

### Community 99 - "Inventory Application"
Cohesion: 0.21
Nodes (9): CancellationToken, Guid, Task, IInventoryQueries, CancellationToken, Guid, Task, GetStockHandler (+1 more)

### Community 100 - "Ordering Domain"
Cohesion: 0.35
Nodes (5): OrderLineDraft, Fact, InlineData, Theory, OrderCreateTests

### Community 101 - "Ordering Infrastructure"
Cohesion: 0.17
Nodes (9): DateTime, Guid, OutboxMessage, EntityTypeBuilder, OutboxMessageConfiguration, DbSet, ModelBuilder, string (+1 more)

### Community 102 - "Payment Infrastructure"
Cohesion: 0.15
Nodes (11): Microsoft.EntityFrameworkCore, Microsoft.Extensions.Resilience, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, FluentAssertions, Microsoft.NET.Test.Sdk, NSubstitute, Testcontainers.PostgreSql (+3 more)

### Community 103 - "Payment Domain"
Cohesion: 0.31
Nodes (4): Fact, InlineData, Theory, PaymentTests

### Community 104 - "Inventory Fragment"
Cohesion: 0.17
Nodes (12): Inventory Module, Honest Naive Path (no CHECK constraint), IReservationStrategy Port (Strategy pattern), Postgres xmin Concurrency Token, xmin as Second Defense Layer Under Lock, Payment.AmountMismatch Replay Guard, Crash Window Analysis P1-P4, Stale-Pending xmin Takeover (+4 more)

### Community 105 - "Ordering Fragment"
Cohesion: 0.27
Nodes (6): DbContextOptions, Action, DbContextOptionsBuilder, PostgreSqlContainer, Task, PostgresFixture

### Community 106 - "Inventory Infrastructure"
Cohesion: 0.45
Nodes (6): Func, CancellationToken, Guid, int, Task, StockReservationService

### Community 107 - "Ordering Infrastructure"
Cohesion: 0.17
Nodes (7): ModelSnapshot, ModelBuilder, DiscoveryDbContextModelSnapshot, ModelBuilder, OrderingDbContextModelSnapshot, ModelBuilder, PaymentDbContextModelSnapshot

### Community 108 - "Inventory Fragment"
Cohesion: 0.30
Nodes (8): OnHand, ReservationRows, Reserved, Fact, Guid, int, Task, ReservationConcurrencyTests

### Community 109 - "Cart Infrastructure"
Cohesion: 0.21
Nodes (9): DbSet, ModelBuilder, string, CartDbContext, DateTime, Guid, List, CartItemRecord (+1 more)

### Community 110 - "Identity Domain"
Cohesion: 0.24
Nodes (6): GeneratedRegex, Regex, Fact, InlineData, Theory, EmailTests

### Community 111 - "Inventory Domain"
Cohesion: 0.20
Nodes (3): ModularCommerce.Inventory.UnitTests.Domain, Fact, InventoryErrorContractTests

### Community 112 - "Discovery Infrastructure"
Cohesion: 0.20
Nodes (5): ModularCommerce.Discovery.Infrastructure.Migrations, MigrationBuilder, ModelBuilder, InitialDiscoverySchema, InitialDiscoverySchema

### Community 113 - "Cart Application"
Cohesion: 0.36
Nodes (6): CancellationToken, Task, AddItemHandler, Fact, Task, AddItemHandlerTests

### Community 114 - "Cart Application"
Cohesion: 0.31
Nodes (6): CancellationToken, Task, UpdateItemQuantityHandler, Fact, Task, UpdateItemQuantityHandlerTests

### Community 115 - "Inventory Domain"
Cohesion: 0.36
Nodes (4): Guid, Fact, Item, StockItemReleaseTests

### Community 116 - "Notification Application"
Cohesion: 0.20
Nodes (7): CancellationToken, Task, INotificationProcessor, NotificationInstruction, CancellationToken, Task, NotificationProcessor

### Community 117 - "Ordering Fragment"
Cohesion: 0.47
Nodes (5): Fact, Guid, List, Task, OutboxIntegrationTests

### Community 118 - "Shipping Api"
Cohesion: 0.20
Nodes (6): ModularCommerce.Shipping.Api, ModularCommerce.Shared.Infrastructure.Modules, IConfiguration, IEndpointRouteBuilder, IServiceCollection, ShippingModule

### Community 119 - "Cart Infrastructure"
Cohesion: 0.40
Nodes (6): CancellationToken, Cart, Exception, Guid, Task, PostgresCartRepository

### Community 120 - "Catalog Infrastructure"
Cohesion: 0.33
Nodes (7): CancellationToken, int, IPublishEndpoint, JsonSerializerOptions, Task, TimeSpan, CatalogOutboxDispatcher

### Community 121 - "Discovery Infrastructure"
Cohesion: 0.29
Nodes (7): string, EmbeddingOptions, EmbeddingProvider, CancellationToken, string, Task, HttpEmbeddingService

### Community 122 - "Inventory Application"
Cohesion: 0.20
Nodes (7): IEndpointRouteBuilder, ReservationEndpoints, ReservationResponse, CancellationToken, Guid, Task, GetReservationHandler

### Community 123 - "Ordering Infrastructure"
Cohesion: 0.33
Nodes (7): CancellationToken, int, IPublishEndpoint, JsonSerializerOptions, Task, TimeSpan, OutboxDispatcher

### Community 124 - "Catalog Domain"
Cohesion: 0.36
Nodes (4): Fact, InlineData, Theory, MoneyTests

### Community 125 - "Cart Fragment"
Cohesion: 0.49
Nodes (4): Fact, Guid, Task, PostgresCartRepositoryTests

### Community 126 - "Cart Fragment"
Cohesion: 0.51
Nodes (4): Fact, Guid, Task, RedisCartCacheIntegrationTests

### Community 127 - "Inventory Domain"
Cohesion: 0.42
Nodes (3): Fact, Item, StockItemCommitTests

### Community 128 - "Notification Fragment"
Cohesion: 0.28
Nodes (9): Bounded Context = Modül, İptal ve Telafi (Cancel + Return + Refund sıralaması), Dead Letter Queue (order-paid-notification_error), Idempotent Inbox (processed_messages), Money Value Object, Order Durum Makinesi (AllowedTransitions), Transactional Outbox, Notification Gereksinimleri (FR-8.x / NFR-8.x) (+1 more)

### Community 129 - "Cart Contracts"
Cohesion: 0.22
Nodes (9): CartLineDto (anti-corruption DTO), CartService (ICartService adaptörü), Checkout Akışı (dört modülün senkron koordinasyonu), CheckoutHandler (Ordering orkestratörü), Domain Event ≠ Integration Event, ICartService (Cart.Contracts sözleşmesi), OrderPaid olayı, Tasarım Revizyonu: Senkron Ödeme (Hafta 6 sonrası) (+1 more)

### Community 130 - "Inventory Fragment"
Cohesion: 0.25
Nodes (9): Üç Rezervasyon Stratejisi (Naive / OptimisticConcurrency / RedisLock), Inventory Gereksinimleri (FR-3.x / NFR-3.x), IReservationStrategy (Strategy pattern portu), Rezervasyon Akışı — Uçtan Uca Sekans, ReserveStockHandler, StockItem.Reserve (domain invariant), Host Restart Disiplini (dürüst strateji karşılaştırması), inventory-oversell.js senaryosu (+1 more)

### Community 131 - "Catalog Infrastructure"
Cohesion: 0.25
Nodes (6): IEntityTypeConfiguration, DateTime, Guid, OutboxMessage, EntityTypeBuilder, OutboxMessageConfiguration

### Community 132 - "Catalog Infrastructure"
Cohesion: 0.28
Nodes (5): Migration, MigrationBuilder, InitialCatalogSchema, MigrationBuilder, InitialPaymentSchema

### Community 133 - "Payment Infrastructure"
Cohesion: 0.25
Nodes (6): DbSet, ModelBuilder, string, PaymentDbContext, PaymentPostgresCollection, PostgresContainerFixture

### Community 134 - "Shared Infrastructure"
Cohesion: 0.25
Nodes (5): string, ProblemMapping, ErrorType, InlineData, Theory

### Community 135 - "Inventory Domain"
Cohesion: 0.44
Nodes (3): Fact, Item, StockItemExpireTests

### Community 136 - "Inventory Domain"
Cohesion: 0.42
Nodes (3): Fact, Item, StockItemReturnTests

### Community 137 - "Ordering Fragment"
Cohesion: 0.39
Nodes (5): ConcurrentBag, Fact, Guid, Task, CheckoutIdempotencyRaceTests

### Community 138 - "Shared Infrastructure"
Cohesion: 0.29
Nodes (5): IResult, HttpContext, Task, ProblemResult, ResultExtensions

### Community 139 - "Catalog Infrastructure"
Cohesion: 0.39
Nodes (4): CancellationToken, Task, CatalogCacheKeys, IProductCache

### Community 140 - "Ordering Infrastructure"
Cohesion: 0.50
Nodes (4): CancellationToken, Guid, Task, OrderRepository

### Community 141 - "Inventory Fragment"
Cohesion: 0.25
Nodes (5): concurrencyConflicts, lockTimeouts, options, soldOutRejections, successfulReservations

### Community 142 - "Cart Fragment"
Cohesion: 0.25
Nodes (7): FluentAssertions, Microsoft.NET.Test.Sdk, StackExchange.Redis, Testcontainers.Redis, xunit, xunit.runner.visualstudio, Microsoft.NET.Sdk

### Community 143 - "Shared Fragment"
Cohesion: 0.25
Nodes (7): FluentAssertions, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql, Testcontainers.Redis, xunit, xunit.runner.visualstudio, Microsoft.NET.Sdk

### Community 144 - "Misc Fragment"
Cohesion: 0.29
Nodes (7): IModule Self-Registration Contract, ModularCommerce Platform, Postgres Service (pgvector/pgvector:pg17), RabbitMQ Service (management), Liveness/Readiness Separation, PostgresHealthCheck (SELECT 1), Diagram 1: Big Picture (Host, modules, infra)

### Community 145 - "Catalog Fragment"
Cohesion: 0.48
Nodes (4): ConcurrentDictionary, CancellationToken, Task, FakeProductCache

### Community 146 - "Cart Fragment"
Cohesion: 0.38
Nodes (4): ModularCommerce.Cart.IntegrationTests.Fixtures, ModularCommerce.Cart.IntegrationTests, CartPostgresCollection, PostgresContainerFixture

### Community 147 - "Shared Fragment"
Cohesion: 0.33
Nodes (3): ModularCommerce.Shared.IntegrationTests, Fact, RateLimiterSizingTests

### Community 148 - "Cart Fragment"
Cohesion: 0.29
Nodes (7): CachingProductQueries (Decorator, graceful degradation), Cart Redis-Only AP Konumlanması, Polly Dayanıklılık Boru Hattı (timeout→retry→breaker→bulkhead), CAP Kararı Operasyon Bazında Verilir, Cart Gereksinimleri (FR-4.x / NFR-4.x), Catalog Gereksinimleri (FR-2.x / NFR-2.x), Payment Gereksinimleri (FR-6.x / NFR-6.x)

### Community 149 - "Shared Infrastructure"
Cohesion: 0.38
Nodes (4): IHostedService, CancellationToken, Task, MigrateAndSeedHostedService

### Community 150 - "Shared Infrastructure"
Cohesion: 0.29
Nodes (6): IServiceProvider, Action, DbContextOptionsBuilder, IConfiguration, IServiceCollection, ModuleDbContextExtensions

### Community 151 - "Shared Infrastructure"
Cohesion: 0.48
Nodes (3): ProblemDetails, Fact, ProblemMappingTests

### Community 152 - "Notification Fragment"
Cohesion: 0.48
Nodes (4): ServiceProvider, Fact, Task, OrderPaidConsumerHarnessTests

### Community 153 - "Cart Application"
Cohesion: 0.43
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, CartService

### Community 154 - "Discovery Api"
Cohesion: 0.38
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, DiscoveryModule

### Community 155 - "Ordering Domain"
Cohesion: 0.29
Nodes (4): OrderStatus, DateTime, OrderStatusChange, TheoryData

### Community 156 - "Payment Domain"
Cohesion: 0.33
Nodes (3): PaymentErrors, Fact, PaymentErrorContractTests

### Community 157 - "Cart Fragment"
Cohesion: 0.29
Nodes (4): cartReads, cartWrites, options, RUN_ID

### Community 158 - "Catalog Application"
Cohesion: 0.48
Nodes (4): Fact, IValidator, Task, GetProductsHandlerTests

### Community 159 - "Payment Fragment"
Cohesion: 0.52
Nodes (4): Fact, Guid, Task, StalePendingTakeoverTests

### Community 161 - "Cart Infrastructure"
Cohesion: 0.40
Nodes (4): ModularCommerce.Cart.Infrastructure.Persistence.Configurations, CartRecord, EntityTypeBuilder, CartConfiguration

### Community 162 - "Shared Infrastructure"
Cohesion: 0.33
Nodes (4): ModularCommerce.Shared.Infrastructure.Configuration, IConfiguration, IServiceCollection, OptionsExtensions

### Community 163 - "Catalog Infrastructure"
Cohesion: 0.33
Nodes (5): DbContext, DbSet, ModelBuilder, string, CatalogDbContext

### Community 164 - "Misc Fragment"
Cohesion: 0.33
Nodes (6): IP-Partitioned Auth Policy, flash-sale.js K6 Scenario, Testcontainers 100-Parallel Reservation Proof, Flash-Sale K6 Proof (oversell=0 under ramp), Oversell = 0 Proof (three strategies), Proof Obligations (oversell=0, duplicate payment=0, p95<500ms)

### Community 165 - "Identity Fragment"
Cohesion: 0.40
Nodes (6): Hakem Veritabanıdır (unique index arbiter), Zorunlu Idempotency-Key Sözleşmesi, Identity Gereksinimleri (FR-1.x / NFR-1.x), Check-Then-Act Yarışı (Naive'in oversell anatomisi), xmin Optimistic Concurrency, checkout-smoke.js senaryosu

### Community 166 - "Discovery Application"
Cohesion: 0.33
Nodes (4): Exception, EmbeddingTransientException, NotificationDeliveryException, PspTransientException

### Community 167 - "Test Fragment"
Cohesion: 0.40
Nodes (4): IConnectionMultiplexer, RedisContainer, Task, RedisFixture

### Community 168 - "Cart Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, CartModule

### Community 169 - "Catalog Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, CatalogModule

### Community 170 - "Catalog Infrastructure"
Cohesion: 0.40
Nodes (3): IntegrationEvent, Type, IIntegrationEventMapper

### Community 171 - "Discovery Infrastructure"
Cohesion: 0.33
Nodes (4): DbSet, ModelBuilder, string, DiscoveryDbContext

### Community 172 - "Identity Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, IdentityModule

### Community 173 - "Inventory Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, InventoryModule

### Community 174 - "Inventory Infrastructure"
Cohesion: 0.53
Nodes (4): CancellationToken, Guid, Task, InventoryQueries

### Community 175 - "Notification Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, NotificationModule

### Community 176 - "Ordering Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, OrderingModule

### Community 177 - "Ordering Infrastructure"
Cohesion: 0.40
Nodes (3): IntegrationEvent, Type, IIntegrationEventMapper

### Community 178 - "Payment Api"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, PaymentModule

### Community 179 - "Shared Infrastructure"
Cohesion: 0.33
Nodes (4): IConfiguration, IEndpointRouteBuilder, IServiceCollection, IModule

### Community 180 - "Ordering Fragment"
Cohesion: 0.40
Nodes (4): Fact, Guid, Task, OrderReservationReconcilerTests

### Community 181 - "Shared Infrastructure"
Cohesion: 0.40
Nodes (3): ClaimsPrincipal, Guid, ClaimsPrincipalExtensions

### Community 182 - "Payment Fragment"
Cohesion: 0.40
Nodes (5): Hata Sözleşmesi (ProblemDetails + retryable bayrağı), Result/Error Railway, ToHttpResult (merkezî hata→HTTP eşlemesi), Retryable vs Terminal 409 Ayrımı, payment-resiliency.js senaryosu

### Community 183 - "Ordering Infrastructure"
Cohesion: 0.40
Nodes (3): EntityTypeBuilder, string, OrderConfiguration

### Community 184 - "Shared Infrastructure"
Cohesion: 0.40
Nodes (3): IConfiguration, IServiceCollection, JwtAuthenticationExtensions

### Community 185 - "Inventory Fragment"
Cohesion: 0.40
Nodes (3): options, reservations201, retryable409

### Community 187 - "Misc Fragment"
Cohesion: 0.50
Nodes (4): Information-Leak Prevention Triad, PBKDF2 Iteration Count Trade-off, RefreshToken Aggregate + Rotation, Custom User Aggregate (no full ASP.NET Identity)

### Community 189 - "Catalog Fragment"
Cohesion: 0.50
Nodes (3): InlineData, Theory, Type

### Community 190 - "Inventory Fragment"
Cohesion: 0.50
Nodes (3): Fact, Task, MigrationTests

### Community 191 - "Payment Fragment"
Cohesion: 0.50
Nodes (3): Fact, Task, MigrationTests

### Community 192 - "Shipping Fragment"
Cohesion: 0.67
Nodes (3): Shipping Module (deferred shell), OpenTelemetry Deferred, Deliberate Deferrals / Evolution Path

## Knowledge Gaps
- **343 isolated node(s):** `Microsoft.EntityFrameworkCore.Design`, `Serilog.AspNetCore`, `Microsoft.NET.Sdk.Web`, `ModularCommerce.Cart.Api`, `AddItemRequest` (+338 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ModularCommerce.Shared.Kernel` connect `Domain Events` to `Cross-Module Contract Interfaces`, `C# Namespace Declarations`, `C# Namespace Declarations II`, `Shared Infrastructure`, `C# Namespace Declarations III`, `C# Namespace Declarations IV`, `Shared Infrastructure`, `Stock Reservation Aggregate`, `Cart Domain Aggregate`, `Refresh Token Persistence`, `Payment Dev Endpoints`, `Shared Fragment`, `Identity User & Email Domain`, `Discovery Indexing Pipeline`, `Cart API Endpoints`, `Catalog Infrastructure`, `Cart Application`, `Catalog Infrastructure`, `Catalog Application`, `Ordering Infrastructure`, `Cart Application`, `Shared Domain`, `Ordering Domain`, `Discovery Application`, `Notification Api`, `Catalog Infrastructure`, `Ordering Infrastructure`, `Cart Application`, `Notification Application`?**
  _High betweenness centrality (0.195) - this node is a cross-community bridge._
- **Why does `Result` connect `Payment Domain` to `Cross-Module Contract Interfaces`, `Catalog Product Domain`, `Shared Infrastructure`, `Ordering Fragment`, `Shared Infrastructure`, `Stock Reservation Aggregate`, `Catalog Queries & Cache Decorator`, `Ordering Infrastructure`, `Cart Domain Aggregate`, `Refresh Token Persistence`, `Stock Endpoints & Handlers`, `Product Embedding Persistence`, `Cart Cache & Repository Decorator`, `Identity Signup Flow`, `Cart Application`, `Identity User & Email Domain`, `Reservation Strategy Implementations`, `Payment Domain`, `Payment Contracts`, `Ordering Application`, `Discovery Application`, `Cart Application`, `Cart Domain`, `Identity Application`, `Identity Application`, `Identity Application`, `Cart Application`, `Shared Domain`, `Payment Infrastructure`, `Ordering Domain`, `Discovery Infrastructure`, `Catalog Application`, `Discovery Application`, `Inventory Application`, `Ordering Domain`, `Inventory Infrastructure`, `Identity Domain`, `Cart Application`, `Cart Application`, `Inventory Domain`, `Notification Application`, `Cart Infrastructure`, `Discovery Infrastructure`, `Inventory Application`, `Catalog Domain`?**
  _High betweenness centrality (0.170) - this node is a cross-community bridge._
- **Why does `ModularCommerce.Shared.Infrastructure.Modules` connect `Shipping Api` to `C# Namespace Declarations`, `Host Bootstrap & Error Handling`, `Notification Api`, `C# Namespace Declarations II`, `C# Namespace Declarations III`, `C# Namespace Declarations IV`, `Payment Dev Endpoints`, `Discovery Indexing Pipeline`, `Cart API Endpoints`?**
  _High betweenness centrality (0.041) - this node is a cross-community bridge._
- **What connects `Microsoft.EntityFrameworkCore.Design`, `Serilog.AspNetCore`, `Microsoft.NET.Sdk.Web` to the rest of the system?**
  _343 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Cross-Module Contract Interfaces` be split into smaller, more focused modules?**
  _Cohesion score 0.06227106227106227 - nodes in this community are weakly interconnected._
- **Should `Inventory TTL Sweeper & Persistence` be split into smaller, more focused modules?**
  _Cohesion score 0.06299603174603174 - nodes in this community are weakly interconnected._
- **Should `Catalog Product Domain` be split into smaller, more focused modules?**
  _Cohesion score 0.05009920634920635 - nodes in this community are weakly interconnected._