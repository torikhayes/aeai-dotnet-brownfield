# eShop Docs Index

Internal quick-start docs for this repo, written to speed up future work (ours and any AI assistant's). This is the Microsoft **dotnet/eShop** reference app (.NET Aspire-orchestrated microservices), forked here as `aeai-dotnet-brownfield`.

> Note: `/home/kenny/CLAUDE.md` (user-level, auto-loaded) currently describes an unrelated "Customer Responsiveness Icon" Next.js/React/MUI feature with a `specs/014-...` folder. That content does not apply to this repo — there's no such spec, stack, or folder here. Treat it as stale/leftover from a different project until it's updated.

## Read in this order

1. [architecture.md](architecture.md) — system overview, tech stack, how Aspire wires everything together
2. [services.md](services.md) — per-project breakdown (what each one does, its data store, its folders)
3. [event-flow.md](event-flow.md) — order lifecycle state machine and the integration events that drive it
4. [dev-workflow.md](dev-workflow.md) — running, testing, and adding a new feature end-to-end

## At a glance

- **Orchestration**: .NET Aspire (`src/eShop.AppHost`) — defines every service, container dependency (Postgres, Redis, RabbitMQ), and wiring
- **Sync APIs**: Catalog.API, Ordering.API, Identity.API (Duende IdentityServer) — ASP.NET Core minimal APIs, versioned
- **Async messaging**: EventBus abstraction + RabbitMQ implementation, integration events, outbox pattern via IntegrationEventLogEF
- **Frontends**: WebApp (Blazor Server, the main storefront), ClientApp (.NET MAUI mobile), HybridApp (.NET MAUI Hybrid/Blazor)
- **Background workers**: OrderProcessor (grace-period order confirmation), PaymentProcessor (simulated payment)
- **Cross-cutting**: eShop.ServiceDefaults (OpenTelemetry, health checks, service discovery, resilience) applied to every service
