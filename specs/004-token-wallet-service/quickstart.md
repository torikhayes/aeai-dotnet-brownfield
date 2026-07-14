# Quickstart: PaymentProcessor Token Ledger Extension

**Feature**: 004-paymentprocessor-token-ledger

## Prerequisites

- Local environment set up per `.github/agents/local-setup.agent.md`
- Colima running (`colima status`)
- .NET 10 SDK (`dotnet --version`)

## Run the Application

```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

Aspire will automatically create the `tokendb` Postgres database and run EF Core migrations + seed the lookup table on startup. Watch the Aspire dashboard for PaymentProcessor health status.

## Test Token Endpoints Manually

Once the app is running, find the PaymentProcessor URL in the Aspire dashboard. All endpoints are internal-mesh-only — use the dashboard's "Invoke" feature or call from another service in the mesh.

**Get token balance** (replace `<token>` with a JWT from Identity.API):
```bash
curl -H "Authorization: Bearer <token>" \
  https://localhost:<paymentprocessor-port>/api/tokens/balance
```

**Preview reward for a Driver in Excellent condition**:
```bash
curl "https://localhost:<paymentprocessor-port>/api/tokens/reward-preview?category=Driver&condition=Excellent"
# Expected: { "tokenAmount": 80, "tableVersion": "1.0.0" }
```

**View transaction history**:
```bash
curl -H "Authorization: Bearer <token>" \
  "https://localhost:<paymentprocessor-port>/api/tokens/transactions?page=1&pageSize=20"
```

## Run Unit Tests (TDD — write tests first)

```bash
dotnet test tests/PaymentProcessor.UnitTests
```

Tests must be written before implementation per Principle V. Key test classes:

| Test Class | Covers |
|---|---|
| `TokenLedgerServiceTests` | Balance read (no wallet), award idempotency (EventId + CatalogItemId), spend rejection on insufficient balance, optimistic concurrency retry |
| `TokenAwardLookupTests` | Correct version selected at processing time, lookup returns expected seed values |

## Trigger a Token Award Locally

1. List a club as a seller via `POST /api/catalog/items` with sufficient photos.
2. Catalog.API will publish `ClubListingVerifiedIntegrationEvent` after verification passes.
3. PaymentProcessor subscribes and credits tokens.
4. Verify: `GET /api/tokens/balance` shows updated balance.

## Add a New Lookup Table Version

Insert new rows into `TokenAwardLookupEntry` with a higher `TableVersion` and `EffectiveFrom = DateTime.UtcNow`. Existing rows are never updated. The next `ClubListingVerifiedIntegrationEvent` processed after that timestamp will use the new values.

```sql
INSERT INTO "TokenAwardLookupEntries" 
  ("Id", "ClubCategory", "ConditionGrade", "TokenAmount", "TableVersion", "EffectiveFrom")
VALUES
  (gen_random_uuid(), 'Driver', 'New', 110, '1.1.0', now()),
  -- ... remaining rows
```

## EF Core Migrations

After modifying `TokenDbContext` or model classes, add a new migration:

```bash
cd src/PaymentProcessor
dotnet ef migrations add <MigrationName> \
  --context TokenDbContext \
  --output-dir TokenLedger/Infrastructure/Migrations
```

Migrations run automatically on startup via `AddMigration<TokenDbContext, TokenDbSeeder>()`.
