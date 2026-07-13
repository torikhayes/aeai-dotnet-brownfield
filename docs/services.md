# Services &amp; Projects

All paths relative to `src/`. Grouped by role.

## APIs (own their data)

### Catalog.API
- **Purpose**: product catalog — items, brands, types, images, pricing. Optional AI-assisted semantic search (pgvector + OpenAI/Ollama embeddings, off by default).
- **DB**: `catalogdb` (Postgres)
- **Key folders**: `Apis/CatalogApi.cs` (minimal API endpoints), `Model/`, `Infrastructure/` (EF Core), `IntegrationEvents/EventHandling/` (reacts to `OrderStatusChangedToAwaitingValidation`, `OrderStatusChangedToPaid` — i.e. stock checks/decrements), `Setup/catalog.json` (seed data), `Pics/` (seed product images), `Services/` (AI search).
- **Publishes**: `ProductPriceChangedIntegrationEvent` (consumed by Webhooks.API to demo webhook fan-out).

### Ordering.API
- **Purpose**: order lifecycle — the most architecturally rich service. Full DDD + CQRS.
- **DB**: `orderingdb` (Postgres), owns the EF Core migrations other services wait on.
- **Layering**: `Ordering.Domain` (aggregates, domain events, no infra deps) → `Ordering.Infrastructure` (EF Core `OrderingContext`, repositories, migrations) → `Ordering.API` (HTTP surface + application layer).
- **Ordering.API/Application**: MediatR `Commands/` (CreateOrder, CancelOrder, ShipOrder, SetXOrderStatus...), `Queries/`, `Behaviors/` (Logging, Validation, Transaction-per-request), `DomainEventHandlers/`, `IntegrationEvents/` (publishes/consumes order-status integration events), `Validations/` (FluentValidation).
- **Domain**: `Ordering.Domain/AggregatesModel/OrderAggregate` (Order, OrderItem, OrderStatus, Address) and `BuyerAggregate`. Domain events (`Order.Domain/Events/`) drive same-process side effects; integration events drive cross-service side effects. See [event-flow.md](event-flow.md).
- **Endpoints**: `Apis/OrdersApi.cs`, versioned, `RequireAuthorization()`.

### Identity.API
- **Purpose**: authentication/authorization for the whole system — Duende IdentityServer + ASP.NET Core Identity.
- **DB**: `identitydb` (Postgres)
- **Notes**: in-memory IdentityServer config (`Config.cs` under `Configuration/`) defines clients/scopes/resources; clients for every other app are registered here, with redirect URIs injected at runtime via env vars set in `eShop.AppHost/Program.cs` (`BasketApiClient`, `OrderingApiClient`, `WebAppClient`, etc.) — this is the one deliberate cyclic wiring point in the AppHost. Uses developer signing credentials — **not production-ready as configured** (there are `// TODO` comments to that effect in `Program.cs`).

### Webhooks.API
- **Purpose**: demo webhook subscription/delivery system — lets external subscribers register a callback URL and receive events like order-shipped or price-changed.
- **DB**: `webhooksdb` (Postgres)
- **Key folders**: `IntegrationEvents/` (subscribes to `OrderStatusChangedToShipped`, `OrderStatusChangedToPaid`, `ProductPriceChanged`), `Migrations/`.

### Basket.API
- **Purpose**: shopping basket, per-user, cache-backed (not durably persisted — it's a cart, not an order).
- **Store**: Redis
- **Surface**: gRPC (`Grpc/BasketService.cs`, `Proto/`) rather than REST — WebApp/ClientApp call it via generated gRPC client.
- **Key folders**: `Model/` (CustomerBasket, BasketItem), `Repositories/` (`IBasketRepository` / `RedisBasketRepository`), `IntegrationEvents/EventHandling/OrderStartedIntegrationEventHandler` (clears the basket once an order is placed).

## Background workers (no public HTTP surface beyond health checks)

### OrderProcessor
- **Purpose**: implements the "grace period" step of the order state machine — after an order is submitted, waits a configurable delay then raises `GracePeriodConfirmedIntegrationEvent` so Ordering.API can move it to `AwaitingValidation`. See `Services/GracePeriodManagerService.cs`.
- Uses `AddBasicServiceDefaults()` (no outbound HTTP resilience needed — it's a pure event consumer/producer).

### PaymentProcessor
- **Purpose**: simulates a payment gateway. Subscribes to `OrderStatusChangedToStockConfirmedIntegrationEvent`, decides pay/fail (configurable via `PaymentOptions`), publishes the result back onto the bus.

## Frontends

### WebApp
- **Purpose**: the main storefront — Blazor Server app, the human-facing entry point most feature work targets.
- Talks to Basket.API/Catalog.API/Ordering.API over HTTP/gRPC with service discovery; proxies product images through `MapForwarder` to Catalog.API.
- `Services/OrderStatus/IntegrationEvents/EventHandling/` — subscribes to order-status-changed events to update the UI live (this is the pattern to follow if a new order status needs to surface in the storefront).
- Shares Razor components with `WebAppComponents` (catalog cards, item display, etc. — reusable across WebApp/HybridApp).

### ClientApp
- **Purpose**: .NET MAUI native mobile client, talks to APIs through the `mobile-bff` YARP proxy. Uses `IdentityModel.OidcClient` for OIDC login against Identity.API.

### HybridApp
- **Purpose**: .NET MAUI Hybrid app — native shell hosting Blazor components (`wwwroot/`, `Components/`), an alternative mobile delivery model to ClientApp.

### WebhookClient
- **Purpose**: a minimal demo app that registers itself as a webhook subscriber against Webhooks.API and displays received events — exists to prove out the webhook feature end-to-end.

## Shared libraries

- **eShop.ServiceDefaults** — see [architecture.md](architecture.md#cross-cutting-eshopservicedefaults).
- **EventBus** — transport-agnostic pub/sub abstraction (`IEventBus`, `IntegrationEvent`, `IIntegrationEventHandler<T>`).
- **EventBusRabbitMQ** — the RabbitMQ `IEventBus` implementation, topic exchange `eshop_event_bus`, one queue per subscribing service, OpenTelemetry-instrumented, Polly retry on publish.
- **IntegrationEventLogEF** — EF Core-based outbox/event log used by services that need reliable at-least-once publish (Ordering.API, Webhooks.API).
- **WebAppComponents** — shared Razor component library (`Catalog/`, `Item/`, `Services/`) used by WebApp and HybridApp.
- **Shared** — small grab-bag (`ActivityExtensions`, `MigrateDbContextExtensions` for the `AddMigration<TContext, TSeed>()` pattern used by every EF-backed service on startup).

## Tests

- `Basket.UnitTests`, `Ordering.UnitTests` — unit tests for domain/application logic.
- `Catalog.FunctionalTests`, `Ordering.FunctionalTests` — spin up the API in-memory (`Program.Testing.cs` partials exist in Catalog.API/Ordering.API for this) and hit real endpoints.
- `ClientApp.UnitTests` — MAUI ViewModel/service tests.
- `e2e/` (repo root) — Playwright browser tests against the running WebApp.
