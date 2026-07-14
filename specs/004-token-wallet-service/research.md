# Research: PaymentProcessor Token Ledger Extension

**Feature**: 004-paymentprocessor-token-ledger  
**Date**: 2026-07-13

---

## Decision 1: HTTP Pipeline in PaymentProcessor

**Decision**: No restructuring needed. PaymentProcessor is already `Microsoft.NET.Sdk.Web` and calls `WebApplication.CreateBuilder` + `AddServiceDefaults()`. It already hosts an HTTP pipeline with health endpoints.

**Rationale**: `PaymentProcessor/Program.cs` shows `var builder = WebApplication.CreateBuilder(args)` and `app.MapDefaultEndpoints()`. Adding `TokensApi` minimal API endpoints is additive — same pattern as any other service in the repo.

**Alternatives considered**: Converting to a Worker Service + hosted HTTP was considered but rejected — the existing `SDK.Web` setup is simpler and already correct.

---

## Decision 2: JWT Authentication on Token Endpoints

**Decision**: Use `AddDefaultAuthentication()` from `eShop.ServiceDefaults`, keyed to Identity.API, consistent with all other APIs in the solution.

**Rationale**: `Catalog.API`, `Ordering.API`, and `Webhooks.API` all call `AddDefaultAuthentication()` with the Identity URL injected via `WithEnvironment("Identity__Url", identityEndpoint)` in the AppHost. PaymentProcessor must receive the same environment variable.

**AppHost change required**:
```csharp
builder.AddProject<Projects.PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(tokenDb)                                    // add
    .WithEnvironment("Identity__Url", identityEndpoint);      // add
```

**Alternatives considered**: A separate API key was considered but rejected per Clarification Q2 (Aspire mesh isolation is sufficient; API key adds credential management overhead).

---

## Decision 3: Internal Spend Endpoint Isolation

**Decision**: PaymentProcessor is not registered with `WithExternalHttpEndpoints()` in the AppHost, so none of its HTTP endpoints are reachable from outside the Aspire service mesh. No additional port-binding work is required.

**Rationale**: Reviewing `AppHost/Program.cs` — only `identity-api`, `mobile-bff`, `webapp`, `webhooksclient`, and `webhooks-api` use `WithExternalHttpEndpoints()`. `payment-processor` does not. All endpoints are already internal-mesh-only.

**Implementation note**: The `POST /api/tokens/spend` endpoint should additionally require the caller to be authenticated as a recognised service identity. Since Ordering.API is also inside the mesh, this is enforced at the JWT level (the caller must have a valid JWT from Identity.API). This is sufficient isolation for Phase 1.

---

## Decision 4: EF Core Optimistic Concurrency

**Decision**: Use EF Core's `IsRowVersion()` concurrency token on `TokenWallet`. Retry logic wraps balance mutations in a loop (max retries configurable via `TokenOptions:MaxConcurrencyRetries`, default 3).

**Rationale**: Per Clarification Q1. `IsRowVersion()` is the idiomatic EF Core + Postgres pattern using `xmin` system column or an explicit `byte[]` property. For Npgsql, use `HasDefaultValueSql("gen_random_uuid()")` on a `uint` xmin, or a standard `[Timestamp]` byte[] — both work. The `byte[]` approach is more portable.

**Pattern**:
```csharp
// TokenWallet.cs
[Timestamp]
public byte[] RowVersion { get; set; } = [];

// TokenDbContext.cs
modelBuilder.Entity<TokenWallet>()
    .Property(w => w.RowVersion)
    .IsRowVersion();
```

**Retry loop**:
```csharp
for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try { /* re-read, mutate, SaveChanges */ break; }
    catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
    { /* detach, continue */ }
}
```

---

## Decision 5: Lookup Table Storage

**Decision**: Store the token award lookup table in `tokendb` as a `TokenAwardLookupEntry` table seeded on startup via `TokenDbSeeder`. Each row is immutable once written; re-tuning inserts new rows with a higher `TableVersion`. The active entry for a (category, condition) pair is the row with the latest `EffectiveFrom ≤ UtcNow`.

**Rationale**: A DB table (vs config file) is auditable, survives container restarts, and allows the version history required by Principle III. `TokenDbSeeder` follows the `MigrateDbContextExtensions.AddMigration<TContext, TSeed>()` pattern already used by `Catalog.API`, `Identity.API`, `Ordering.API`, and `Webhooks.API`.

**Initial lookup table values (v1.0.0)** — placeholder values to be tuned by the team:

| Category      | New | Excellent | Good | Fair |
|---------------|-----|-----------|------|------|
| Driver        | 100 | 80        | 60   | 40   |
| Fairway Wood  |  80 | 65        | 50   | 30   |
| Hybrid        |  70 | 55        | 40   | 25   |
| Iron Set      | 120 | 95        | 70   | 45   |
| Wedge         |  60 | 48        | 35   | 20   |
| Putter        |  90 | 72        | 54   | 35   |
| Other         |  50 | 40        | 30   | 15   |

**TableVersion**: `"1.0.0"` | **EffectiveFrom**: seeded at migration time.

---

## Decision 6: Migration Pattern

**Decision**: Register `TokenDbContext` using `AddMigration<TokenDbContext, TokenDbSeeder>()` from `eShop.Shared` (same as every other EF-backed service). PaymentProcessor should `WaitFor` itself (i.e., the AppHost should start PaymentProcessor only after `tokendb` is ready — Aspire handles this automatically when `.WithReference(tokenDb)` is combined with `.WaitFor(tokenDb)` if needed).

**AppHost change**:
```csharp
var tokenDb = postgres.AddDatabase("tokendb");   // add

builder.AddProject<Projects.PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(tokenDb).WaitFor(tokenDb)     // add
    .WithEnvironment("Identity__Url", identityEndpoint);
```

---

## Decision 7: NuGet Packages Required

Add to `PaymentProcessor.csproj`:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
```

These are already in `Directory.Packages.props` (used by Catalog.API, Ordering.Infrastructure, Webhooks.API) — no new package version pins required.

---

## Decision 8: Test Project Pattern

**Decision**: Create `tests/PaymentProcessor.UnitTests/` following the `Ordering.UnitTests` project structure (xUnit, Microsoft.Testing.Platform, no integration dependencies — pure unit tests of `TokenLedgerService` using in-memory EF Core or mocks).

**TDD scope (Principle V)**: Tests must be written and reviewed BEFORE implementation for:
- `CreditTokens()` — idempotency (same EventId, same CatalogItemId)
- `DebitTokens()` — insufficient balance rejection, optimistic concurrency retry
- `GetBalance()` — returns 0 for missing wallet without DB write
- `GetActiveLookupEntry()` — returns correct version at processing time
