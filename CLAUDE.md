# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ModularCommerce is a .NET 10 modular-monolith e-commerce platform, built as a working, measurable companion to the author's Medium articles on race-condition handling, idempotency, API resiliency, and K6 load testing. The requirements, CAP positioning per operation, and "proof obligations" (definition of done) live in [docs/requirements.md](docs/requirements.md) — read it before implementing any module's behavior; it is the source of truth for functional/non-functional requirements. Documentation and code comments are written in Turkish.

**Current state (Week 9 done):** Catalog (product list/detail, EF Core schema-per-module), Inventory (three reservation strategies: Naive / OptimisticConcurrency via xmin / RedisLock — switched by `Inventory:ReservationStrategy` config; reservations support Release for compensation, Commit for permanent decrement — Commit lowers OnHand and Reserved together so Available never changes — plus **Expire** (Active→Expired, Reserved-=q; TTL sweeper) and **Return** (Committed→Returned, OnHand+=q; cancel refund of committed stock); a `ReservationTtlSweeper` BackgroundService (30s poll) finds expired Active reservations and asks Ordering via `IOrderReservationReconciler` (Ordering.Contracts) — reservations bound to a Paid order are Committed (P2 reconcile, Available unchanged so NO oversell), orphans are Expired; a classify error leaves the batch untouched), Identity (custom User aggregate + JWT access/refresh with rotation; NOT full ASP.NET Identity), Cart (Redis-only, no DbContext, TTL 7d), Ordering (checkout with mandatory `Idempotency-Key` header, order state machine with full transition matrix, `order_status_history` audit table, **handmade transactional outbox**), and Payment (payments + append-only payment_attempts in "payment" schema; fake PSP behind a Polly resilience pipeline — total timeout 3s → retry+jitter → circuit breaker → bulkhead → per-attempt timeout 1s; simulation knobs in `Payment:Psp` config) are implemented. **Payment is synchronous inside checkout** (design revision, roadmap note 5): reserve → charge (`IPaymentService`, payments unique index = single-charge arbiter) → persist order as `Paid` (orders unique index = single-order arbiter) → commit reservations; a declined payment releases reservations and no order row is written. Retryable-409 client contract: `Inventory.ConcurrencyConflict`/`LockTimeout`/`Payment.InFlight`/`Payment.PspUnavailable` → retry with the SAME key; `Payment.Declined`/`Payment.Timeout` are terminal (same key replays the copy, FR-6.2). **Outbox (Week 8, NFR-5.2):** `Order.MarkPaid` raises an `OrderPaid` *domain* event; `DomainEventToOutboxInterceptor` (wired ONLY onto OrderingDbContext via the opt-in `configure` param of `AddModuleDbContext`) maps registered events through `IIntegrationEventMapper` and writes an `ordering.outbox_messages` row in the SAME `SaveChanges` transaction as the order (atomic). `OutboxDispatcher` (BackgroundService, ~1s poll) publishes pending rows via MassTransit `IPublishEndpoint.Publish(obj, Type)` and marks `ProcessedOnUtc`; at-least-once. The dispatched contract is the separate POCO `Ordering.Contracts.IntegrationEvents.OrderPaid` (domain event ≠ integration event). MassTransit/RabbitMQ is registered ONCE via `AddEventBus` in Shared.Infrastructure (Program.cs); consumers are injected from the composition root. Only `OrderPaid` is published so far (Payment events + real consumers + idempotent-consumer/DLQ are Week 10). A temporary log-only `OrderPaidLoggingConsumer` lives in Notification.Api. **Cancel (Week 9):** `POST /api/ordering/orders/{id}/cancel` runs `CancelOrderHandler` — comprehensive cancellation: `Order.Cancel` (matrix now allows Paid→Cancelled) raises `OrderCancelled`; then Inventory `ReturnAsync` per line (best-effort, restores OnHand) and Payment `RefundAsync` (critical — if refund fails the cancel is NOT persisted, order stays Paid), then `IOrderRepository.SaveChangesAsync` writes Cancelled + OrderCancelled outbox atomically. `OrderCancelled` is now the registry's second entry (OCP). Cross-module sync calls go through `.Contracts` only (`ICartService`, `IProductReader`, `IStockReservationService` — now with Expire/Return, `IPaymentService` — now with Refund, `IOrderReservationReconciler`); each module registers its own contract adapter in its `Register`. Note: `Inventory.Infrastructure` references `Ordering.Contracts` (the sweeper's P2 reconcile query — Contracts-only, no project cycle; the first "reverse-direction" module dependency). Note: the `Payment` aggregate class collides with the `ModularCommerce.Payment` namespace segment — files outside the Domain use a `PaymentAggregate` alias. JWT validation is host-wide (`AddJwtAuthentication` in Shared.Infrastructure/Auth, `Jwt` config section). Shipping and Notification are still (mostly) health-endpoint shells. When adding behavior, you are filling in the skeleton, not restructuring it.

## Commands

```bash
docker compose up -d                                   # Postgres, Redis, RabbitMQ (required at runtime)
dotnet run --project src/Bootstrapper/ModularCommerce.Host
dotnet build ModularCommerce.sln                       # TreatWarningsAsErrors=true — warnings fail the build
dotnet test                                            # all tests
dotnet test tests/ModularCommerce.ArchitectureTests    # architecture boundary tests only
dotnet test --filter "FullyQualifiedName~Domain_should_not_depend_on_infrastructure"   # single test
```

The host listens on `https://localhost:49821` / `http://localhost:49822` (see `launchSettings.json`); the README's `:5000` is illustrative. `GET /` returns the live module list.

## Architecture

**8 modules** — Identity, Catalog, Cart, Inventory, Ordering, Payment, Shipping, Notification — each split into 5 layered projects under `src/Modules/<Module>/`:

- `Domain` — pure domain model. References only `Shared.Kernel`; an architecture test forbids any dependency here on Infrastructure, Application, **or EF Core**.
- `Application` — use cases. References `Domain` + `Contracts`.
- `Infrastructure` — persistence/adapters. References `Application`.
- `Api` — the module's composition (`<Module>Module : IModule`) and endpoints. References `Infrastructure` + `Shared.Infrastructure`.
- `Contracts` — **the only project other modules may reference.** Public interfaces, integration events, and DTOs live here. Its only allowed project reference is `Shared.Kernel` (for `Result`/`Error`); referencing any module's internal layers is forbidden and enforced by the `Contracts_should_be_self_contained` architecture test.

Reference direction within a module: `Api → Infrastructure → Application → {Domain, Contracts}`, and `Domain → Shared.Kernel`.

**Composition root** — `src/Bootstrapper/ModularCommerce.Host/Program.cs` holds a static `IModule[]`, calls `Register(services, config)` on each, then `MapEndpoints(app)`. To wire a new module, add it to that array. Each module self-registers via `IModule` ([Shared.Infrastructure/Modules/IModule.cs](src/Shared/ModularCommerce.Shared.Infrastructure/Modules/IModule.cs)); the Host never references a module's internals.

**Shared.Kernel** — `Entity` (Guid id + `Raise`/`DomainEvents` for domain events), `IDomainEvent`, and a `Result` / `Result<T>` / `Error` railway type. Domain code returns `Result` rather than throwing for expected failures.

### The module-boundary rule (central invariant)

A module may reference **only** another module's `Contracts` project — never its internal layers. This is enforced in three places (per README): NetArchTest at build time, separate PostgreSQL schema + per-module DB user at runtime, and code review. When touching cross-module code, preserve this: synchronous cross-module calls go through `Contracts` interfaces (in-process today, extractable to HTTP/gRPC later); facts propagate as integration events via outbox → RabbitMQ (MassTransit). [tests/ModularCommerce.ArchitectureTests/ModuleBoundaryTests.cs](tests/ModularCommerce.ArchitectureTests/ModuleBoundaryTests.cs) drives these checks off the `Modules`/`InternalLayers` arrays — extend those arrays, not the individual tests, when the module set changes.

## Conventions

- **Central package management**: all versions in [Directory.Packages.props](Directory.Packages.props); csproj files use `<PackageReference>` with no `Version`. Add a new dependency by declaring its `<PackageVersion>` there first.
- Global settings in [Directory.Build.props](Directory.Build.props): net10.0, `LangVersion=latest`, nullable + implicit usings enabled, warnings-as-errors.
- Intended stack (from requirements, not yet wired): EF Core + Npgsql, StackExchange.Redis, MassTransit/RabbitMQ, Polly-style resilience via `Microsoft.Extensions.Http.Resilience`, FluentValidation, Serilog + OpenTelemetry, ASP.NET Identity + JWT. Integration tests use Testcontainers.PostgreSql; load tests (K6) belong under `tests/LoadTests`.
