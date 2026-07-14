# eShop Copilot Workspace Instructions

## Codebase Context Files — Consult Before Exploring Source Code

Before searching or reading source files for **any task**, consult the pre-generated context files in `.github/context/`. These files document the architecture, API endpoints, database schemas, integration events, core classes, and file structure for every service in the monorepo.

**Default lookup order:**
1. Read `.github/context/eshop-overview.md` — system architecture map, service directory, event flow diagram, and technology stack
2. Read the service-specific context file(s) relevant to the current task (see table below)
3. Only fall back to reading source files directly when the context files lack the specific detail needed for the task

**Exception:** If the user explicitly attaches or references a specific source file in the chat, use that file directly — context file pre-loading is not required.

### Service Context File Map

| Working on... | Read this context file |
|---|---|
| Products, catalog browsing, seller listings, ratings, search | `.github/context/catalog-api.md` |
| Shopping cart, basket | `.github/context/basket-api.md` |
| Orders, order lifecycle, CQRS/DDD | `.github/context/ordering-api.md` |
| Grace period, order state transitions | `.github/context/order-processor.md` |
| Payments, token wallet, token rewards | `.github/context/payment-processor.md` |
| Authentication, login, OAuth2, users | `.github/context/identity-api.md` |
| Frontend UI, Blazor pages, chatbot | `.github/context/webapp.md` |
| Webhook subscriptions, webhook delivery | `.github/context/webhooks-api.md` |
| Service wiring, infrastructure, Aspire | `.github/context/apphost.md` |
| Event bus, RabbitMQ, outbox pattern, shared libs | `.github/context/shared-libraries.md` |
| Full system, cross-service, event flows | `.github/context/eshop-overview.md` |

---

## Project Overview

eShop is a **.NET 10 microservices marketplace** orchestrated with **.NET Aspire**. See `.github/context/eshop-overview.md` for the full architecture diagram, event flow, and service map.

**Key technology decisions:**
- All services target `.NET 10.0`
- Databases: PostgreSQL (via Aspire + Npgsql EF Core) and Redis
- Message broker: RabbitMQ (`eshop_event_bus` direct exchange)
- Auth: Duende IdentityServer (OAuth2/OIDC)
- Frontend: Blazor Server SSR
- Observability: OpenTelemetry
- Resilience: Polly (via `Microsoft.Extensions.Http.Resilience`)

---

## Coding Conventions

- Follow existing patterns in each service before introducing new abstractions
- Minimal APIs are used in Catalog, Ordering, Webhooks, and PaymentProcessor — do not add MVC controllers
- Basket.API is gRPC only — do not add REST endpoints
- Use `record` types for integration events (they extend `IntegrationEvent`)
- EF Core migrations live in `Infrastructure/Migrations/` for each service
- Shared cross-cutting code belongs in `eShop.ServiceDefaults` or `src/Shared/`
- New integration events must be defined in both publisher and consumer services
