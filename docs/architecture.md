# Architecture

## What this is

This repo is Microsoft's **dotnet/eShop** reference application — a services-based e-commerce sample ("AdventureWorks") built on **.NET 10** and orchestrated with **.NET Aspire**. It's designed as a canonical example of .NET microservice patterns (DDD, CQRS, event-driven integration, BFF), not a from-scratch app — so most future feature work here means *extending an existing service* or *adding a new one into the Aspire graph*, following the conventions already in place.

Repo-level architecture diagram: `img/eshop_architecture.png`.

## Orchestration: .NET Aspire

Everything is composed in `src/eShop.AppHost/Program.cs`. This is the single source of truth for "what services exist and how they're wired" — read it first when onboarding to a change.

Containers/resources declared here:
- **Redis** — basket cache
- **RabbitMQ** — event bus transport (persistent container lifetime, so it survives AppHost restarts in dev)
- **Postgres** (`ankane/pgvector` image) — hosts four logical databases: `catalogdb`, `identitydb`, `orderingdb`, `webhooksdb`

Projects are added with `.WithReference(...)` to inject connection strings/service-discovery endpoints, and `.WaitFor(...)` to sequence startup (e.g. `order-processor` waits for `ordering-api` because that's the project that owns the EF Core migrations for `orderingdb`).

There's also a YARP reverse proxy (`mobile-bff`) that fronts Catalog/Ordering/Identity for the mobile clients — see `ConfigureMobileBffRoutes` in `src/eShop.AppHost/Extensions.cs`.

Optional AI wiring exists but is off by default: `useOpenAI` / `useOllama` flags in `Program.cs` gate `builder.AddOpenAI(...)` / `builder.AddOllama(...)` for Catalog.API's AI-assisted search (pgvector is already in the Postgres image for this reason).

## Cross-cutting: eShop.ServiceDefaults

Every service calls `builder.AddServiceDefaults()` (or `AddBasicServiceDefaults()` for workers that make no outgoing HTTP calls). This one call wires up, uniformly, across all services:
- OpenTelemetry logging/metrics/tracing, with OTLP exporter auto-enabled if `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- `/health` and `/alive` endpoints (dev-only)
- Service discovery + standard HTTP resilience (Polly) for outgoing `HttpClient`s

If you add a new service, start from an existing `Program.cs` (e.g. `Webhooks.API`) and call the same extension — don't hand-roll telemetry/health/resilience per service.

## Communication patterns

- **Synchronous**: ASP.NET Core minimal APIs, versioned via `Asp.Versioning` (`app.NewVersionedApi(...)`), consumed over HTTP with service discovery (`https+http://catalog-api` style URIs) and the standard resilience handler. Basket.API also exposes a **gRPC** service (`src/Basket.API/Grpc/BasketService.cs`) for basket ops.
- **Asynchronous**: an `IEventBus` abstraction (`src/EventBus`) with a RabbitMQ implementation (`src/EventBusRabbitMQ`). Services publish `IntegrationEvent` subclasses to a single topic exchange (`eshop_event_bus`); each subscriber has its own named queue. Registration is declarative, e.g. in `PaymentProcessor/Program.cs`:
  ```csharp
  builder.AddRabbitMqEventBus("EventBus")
      .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
  ```
- **Reliable publish (outbox pattern)**: `src/IntegrationEventLogEF` persists outgoing integration events in the same DB transaction as the business change, then a background step publishes them — avoids the dual-write problem between "save order" and "publish OrderStarted". Used by Ordering.API and Webhooks.API.
- **Auth**: Identity.API is a Duende IdentityServer instance backed by ASP.NET Core Identity (Postgres). Other services validate JWTs issued from it; the AppHost wires each service's public URL into Identity's config for OIDC redirect/callback URIs (see the "cyclic reference" section at the bottom of `eShop.AppHost/Program.cs`).

## Tech stack summary

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| Orchestration | .NET Aspire |
| APIs | ASP.NET Core Minimal APIs + Asp.Versioning |
| Web frontend | Blazor Server (WebApp) + shared Razor component library (WebAppComponents) |
| Mobile | .NET MAUI (ClientApp), .NET MAUI Hybrid/Blazor (HybridApp) |
| Data | EF Core + PostgreSQL (Catalog, Ordering, Identity, Webhooks); Redis (Basket cache) |
| Messaging | RabbitMQ via custom EventBus abstraction |
| CQRS/mediation | MediatR (Ordering.API — commands/queries/behaviors) |
| Auth | Duende IdentityServer + ASP.NET Core Identity |
| Reverse proxy | YARP (mobile BFF) |
| Observability | OpenTelemetry (traces/metrics/logs), Aspire dashboard |
| Testing | xUnit/MSTest (Microsoft Testing Platform), Playwright (e2e, `e2e/`) |
