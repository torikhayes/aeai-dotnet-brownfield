# Running, Testing, and Adding a Feature

## Prerequisites

- Docker running (Postgres/Redis/RabbitMQ are all containers spun up by Aspire)
- .NET 10 SDK (`global.json` pins the exact version)

## Run everything

```sh
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

Console output prints an Aspire dashboard login URL — that dashboard is the best place to see every service's logs/traces/env vars/health at once, and to find each service's dynamically-assigned URL. On Windows/Visual Studio, open `eShop.Web.slnf`, set `eShop.AppHost` as startup project, F5.

Solution files:
- `eShop.slnx` — everything (all APIs, workers, WebApp, MAUI apps)
- `eShop.Web.slnf` — filtered to the web-only subset (lighter to load if you're not touching mobile)

## Tests

- Unit tests: `Basket.UnitTests`, `Ordering.UnitTests` — no Docker required, run directly (`dotnet test`).
- Functional tests: `Catalog.FunctionalTests`, `Ordering.FunctionalTests` — **require Docker running**; they boot the Aspire host with test containers (`CatalogApiFixture`/`OrderingApiFixture`).
- `ClientApp.UnitTests` — MAUI ViewModel/service layer, no device/emulator needed.
- e2e: `e2e/*.spec.ts` via Playwright (`playwright.config.ts` at repo root) — exercises the running WebApp in a browser; see `.github/workflows/playwright.yml` for how CI drives it.

Test projects use xUnit/MSTest on the Microsoft Testing Platform (MTP) — run via `dotnet test` per project, same as any modern .NET test project.

## Conventions worth matching

- **New service**: copy the shape of an existing minimal-API service (`Webhooks.API` is a good small template) — `Program.cs` calling `AddServiceDefaults()`, `Apis/` for versioned minimal-API endpoint groups, `Extensions/` for `AddApplicationServices()`. Register it in `eShop.AppHost/Program.cs` with the right `.WithReference()`/`.WaitFor()` calls, and add it to `eShop.slnx`.
- **New DB-backed service**: add a database via `postgres.AddDatabase("xdb")` in AppHost, use `builder.AddNpgsqlDbContext<TContext>("xdb")`, and use the `AddMigration<TContext, TSeed>()` pattern from `src/Shared` for auto-migrate-on-startup in dev.
- **New order-related side effect**: follow the outbox/event pattern in [event-flow.md](event-flow.md) — don't publish integration events directly from a command handler.
- **New endpoint on an existing API**: add to the relevant `Apis/*.cs` file, keep it inside the existing `NewVersionedApi(...)` group, use `RequireAuthorization()` unless it's intentionally public.
- **Cross-cutting behavior (logging/validation/transactions) for Ordering.API commands**: add a `IPipelineBehavior<,>` under `Application/Behaviors/` rather than duplicating logic in each handler.
- Code style is enforced by `.editorconfig` (4-space indent for C#, 2-space for XML/project files) — no separate linter config for C#.

## Where the "brownfield" framing matters

This is treated as an existing, working, opinionated codebase — the project's own `CONTRIBUTING.md` explicitly asks for architectural integrity and justification for large refactors rather than showcase-driven rewrites. Default to extending existing patterns (DDD/CQRS in Ordering, minimal APIs elsewhere, event-driven cross-service integration) over introducing new architectural styles, unless there's a clear reason tied to the feature at hand.
