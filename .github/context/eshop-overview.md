# eShop Monorepo — System Overview

## What is eShop?

eShop is a **.NET 10 microservices marketplace application** built with **.NET Aspire** orchestration. It demonstrates domain-driven design, event-driven architecture, and microservice patterns. The application is a club gear marketplace where users browse, buy, rate, favorite, and sell club equipment.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        eShop.AppHost (Aspire)                       │
│  Orchestrates all services, databases, caches, and message brokers  │
└─────────────────────────────────────────────────────────────────────┘

┌──────────┐    OIDC     ┌──────────────┐    HTTP/gRPC    ┌────────────┐
│  Browser │──────────── │ Identity.API │◄──────────────── │  WebApp    │
│          │             │ (OAuth2/OIDC)│                  │ (Blazor)   │
└──────────┘             └──────────────┘                  └─────┬──────┘
                                                                 │
                         ┌───────────────────────────────────────┤
                         │                   │                   │
                    HTTP REST          gRPC Client          HTTP REST
                         ▼                   ▼                   ▼
                  ┌─────────────┐   ┌──────────────┐   ┌──────────────┐
                  │ Catalog.API │   │  Basket.API  │   │ Ordering.API │
                  │ (Products)  │   │ (Shopping    │   │ (Orders,     │
                  │             │   │  Cart)       │   │  DDD/CQRS)   │
                  └──────┬──────┘   └──────┬───────┘   └──────┬───────┘
                         │                 │                   │
              ┌──────────┴─────────────────┴───────────────────┴──────────┐
              │                      RabbitMQ EventBus                     │
              └──────────┬─────────────────┬───────────────────┬──────────┘
                         │                 │                   │
                  ┌──────▼──────┐   ┌──────▼──────┐   ┌───────▼──────┐
                  │OrderProcessor│  │PaymentProc. │   │ Webhooks.API │
                  │(Grace Period)│  │(Payment +   │   │(Webhook Mgmt)│
                  │             │   │ Token Ledger)│   │              │
                  └─────────────┘   └─────────────┘   └──────┬───────┘
                                                              │ HTTP POST
                                                       ┌──────▼───────┐
                                                       │WebhookClient │
                                                       │(Demo App)    │
                                                       └──────────────┘

┌───────────────────────────────────────────────────────────────────────┐
│                          Data Stores                                  │
│  PostgreSQL: catalogdb, identitydb, orderdb, webhooksdb, tokendb     │
│  Redis: Basket session cache                                         │
│  RabbitMQ: "eshop_event_bus" direct exchange                         │
└───────────────────────────────────────────────────────────────────────┘
```

## Service Directory

| Service | Type | Protocol | Database | Context File |
|---|---|---|---|---|
| **Catalog.API** | Minimal API | HTTP REST | PostgreSQL (`catalogdb`) + pgvector | [catalog-api.md](catalog-api.md) |
| **Basket.API** | gRPC Service | gRPC | Redis | [basket-api.md](basket-api.md) |
| **Ordering.API** | Minimal API (DDD/CQRS) | HTTP REST | PostgreSQL (`orderdb`) | [ordering-api.md](ordering-api.md) |
| **OrderProcessor** | Background Worker | RabbitMQ (publish only) | PostgreSQL (`orderdb`) read-only | [order-processor.md](order-processor.md) |
| **PaymentProcessor** | Web API + Worker | HTTP REST + RabbitMQ | PostgreSQL (`tokendb`) | [payment-processor.md](payment-processor.md) |
| **Identity.API** | MVC + IdentityServer | HTTP (OIDC) | PostgreSQL (`identitydb`) | [identity-api.md](identity-api.md) |
| **WebApp** | Blazor Server SSR | Browser + HTTP/gRPC clients | None (stateless) | [webapp.md](webapp.md) |
| **Webhooks.API** | Minimal API | HTTP REST + RabbitMQ | PostgreSQL (`webhooksdb`) | [webhooks-api.md](webhooks-api.md) |
| **WebhookClient** | Blazor Server | Browser + HTTP client | In-memory only | [webhook-client.md](webhook-client.md) |
| **eShop.AppHost** | Aspire Orchestrator | N/A | N/A | [apphost.md](apphost.md) |

## Shared Libraries

| Library | Purpose | Context File |
|---|---|---|
| **eShop.ServiceDefaults** | Cross-cutting concerns (auth, telemetry, health checks, OpenAPI) | [shared-libraries.md](shared-libraries.md) |
| **EventBus** | Abstract pub/sub contract (`IEventBus`, `IntegrationEvent`) | [shared-libraries.md](shared-libraries.md) |
| **EventBusRabbitMQ** | RabbitMQ implementation of EventBus | [shared-libraries.md](shared-libraries.md) |
| **IntegrationEventLogEF** | EF Core outbox pattern for reliable event publishing | [shared-libraries.md](shared-libraries.md) |
| **WebAppComponents** | Shared Razor component library | [webapp.md](webapp.md) |
| **Shared** | Utility extensions (migrations, activity tracing) | [shared-libraries.md](shared-libraries.md) |

## Event Flow Map

All inter-service communication uses **RabbitMQ** with a direct exchange named `eshop_event_bus`. Events are routed by type name.

### Order Lifecycle Events

```
 CreateOrder (HTTP)
       │
       ▼
 Ordering.API ──► OrderStartedIntegrationEvent ──────────► Basket.API (clears cart)
       │
       ▼
 Ordering.API ──► OrderStatusChangedToSubmittedIntegrationEvent
       │
       ▼ (grace period expires)
 OrderProcessor ──► GracePeriodConfirmedIntegrationEvent ──► Ordering.API
       │
       ▼
 Ordering.API ──► OrderStatusChangedToAwaitingValidationIntegrationEvent ──► Catalog.API
       │                                                                         │
       │                                              ┌──────────────────────────┘
       │                                              ▼
       │                                     Catalog.API validates stock
       │                                              │
       │              ┌───────────────────────────────┤
       │              ▼                               ▼
       │  OrderStockConfirmedIntegrationEvent   OrderStockRejectedIntegrationEvent
       │              │                               │
       │              ▼                               ▼
       │       Ordering.API                    Ordering.API (cancel)
       │              │
       │              ▼
 Ordering.API ──► OrderStatusChangedToStockConfirmedIntegrationEvent ──► PaymentProcessor
       │                                                                       │
       │                    ┌──────────────────────────────────────────────────┘
       │                    ▼
       │  OrderPaymentSucceededIntegrationEvent / OrderPaymentFailedIntegrationEvent
       │                    │
       │                    ▼
       │             Ordering.API
       │                    │
       │                    ▼
 Ordering.API ──► OrderStatusChangedToPaidIntegrationEvent ──► Catalog.API (deduct stock)
       │                                                   ──► Webhooks.API (notify)
       │
       ▼ (admin ships)
 Ordering.API ──► OrderStatusChangedToShippedIntegrationEvent ──► Webhooks.API (notify)
```

### Catalog Events

```
 Catalog.API ──► ProductPriceChangedIntegrationEvent ──► Webhooks.API (notify)
```

### Token Reward Events

```
 Catalog.API ──► ClubListingVerifiedIntegrationEvent ──► PaymentProcessor (award tokens)
```

## Technology Stack

- **Runtime**: .NET 10.0
- **Orchestration**: .NET Aspire
- **Databases**: PostgreSQL (with pgvector for embeddings), Redis
- **Message Broker**: RabbitMQ (direct exchange, persistent delivery)
- **Auth**: Duende IdentityServer (OAuth2/OIDC)
- **Frontend**: Blazor Server SSR
- **AI**: Azure OpenAI / Ollama (optional, for semantic search & chatbot)
- **ORM**: Entity Framework Core
- **CQRS**: MediatR (Ordering domain)
- **Validation**: FluentValidation
- **Observability**: OpenTelemetry (traces, metrics, logs)
- **Resilience**: Polly (via Microsoft.Extensions.Http.Resilience)

## Agent Recommendations

When working on a specific service, load the relevant context file:

| Task | Recommended Context | Recommended Agent |
|---|---|---|
| Catalog endpoints, products, ratings, listings | [catalog-api.md](catalog-api.md) | `Explore` |
| Shopping cart, basket operations | [basket-api.md](basket-api.md) | `Explore` |
| Order lifecycle, DDD, CQRS commands | [ordering-api.md](ordering-api.md) | `Explore` |
| Grace period processing | [order-processor.md](order-processor.md) | `Explore` |
| Payment simulation, token wallet | [payment-processor.md](payment-processor.md) | `Explore` |
| User auth, OAuth, login flows | [identity-api.md](identity-api.md) | `Explore` |
| Frontend UI, Blazor pages | [webapp.md](webapp.md) | `Explore` |
| Webhook subscriptions | [webhooks-api.md](webhooks-api.md) | `Explore` |
| Service wiring, infrastructure | [apphost.md](apphost.md) | `Explore` |
| Event bus, shared plumbing | [shared-libraries.md](shared-libraries.md) | `Explore` |
| Code quality review | Any relevant context file | `Code Review Team` |
| Running tests | N/A | `Run Unit Tests` / `QA Team` |
| Database migrations | N/A | `DB Lifecycle` |
| Local dev setup | N/A | `Local Setup Guide` |

## Project Structure

```
eShop/
├── src/
│   ├── eShop.AppHost/          # Aspire orchestrator
│   ├── eShop.ServiceDefaults/  # Shared cross-cutting concerns
│   ├── Catalog.API/            # Product catalog service
│   ├── Basket.API/             # Shopping cart service (gRPC)
│   ├── Ordering.API/           # Order management (DDD/CQRS)
│   ├── Ordering.Domain/        # Order domain model
│   ├── Ordering.Infrastructure/# Order data access
│   ├── OrderProcessor/         # Grace period background worker
│   ├── PaymentProcessor/       # Payment + token ledger
│   ├── Identity.API/           # OAuth2/OIDC auth server
│   ├── WebApp/                 # Blazor Server frontend
│   ├── WebAppComponents/       # Shared Razor components
│   ├── Webhooks.API/           # Webhook management
│   ├── WebhookClient/          # Webhook demo receiver
│   ├── EventBus/               # Event bus abstractions
│   ├── EventBusRabbitMQ/       # RabbitMQ implementation
│   ├── IntegrationEventLogEF/  # Outbox pattern
│   ├── Shared/                 # Utility extensions
│   ├── ClientApp/              # (Mobile/MAUI client)
│   └── HybridApp/              # (Hybrid mobile app)
├── tests/
│   ├── Basket.UnitTests/
│   ├── Catalog.FunctionalTests/
│   ├── Ordering.UnitTests/
│   ├── Ordering.FunctionalTests/
│   ├── PaymentProcessor.UnitTests/
│   ├── ClientApp.UnitTests/
│   └── Security.Tooling.UnitTests/
├── e2e/                        # Playwright E2E tests
├── specs/                      # Feature specifications
└── docs/                       # Architecture documentation
```
