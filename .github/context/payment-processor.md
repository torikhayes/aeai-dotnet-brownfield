# PaymentProcessor — Service Context

## Overview

**Project**: `src/PaymentProcessor/`  
**Type**: ASP.NET Core Web API + Event Processor  
**Protocol**: HTTP REST + RabbitMQ  
**Database**: PostgreSQL (`tokendb`)  
**Framework**: .NET 10.0  

The PaymentProcessor has dual responsibilities:
1. **Payment Simulation**: Consumes stock-confirmed events, simulates payment (configurable success/failure), and publishes payment result events
2. **Token Ledger**: Manages a club token rewards system — awards tokens to sellers for verified listings, tracks balances, and handles token spending during checkout

## Architecture

- **Pattern**: Event-driven microservice + REST API
- **Data Access**: Entity Framework Core (`TokenDbContext`)
- **Event-Driven**: Consumes RabbitMQ events and publishes payment results
- **Concurrency**: Optimistic locking on `TokenWallet` using PostgreSQL `xmin` column
- **Idempotency**: Checks `TokenTransactions.RelatedEventId` and `TokenAwardedListings.CatalogItemId` before awarding

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **Ordering.API** (indirect) | RabbitMQ event | Stock confirmed → simulate payment |
| **Catalog.API** (indirect) | RabbitMQ event | Listing verified → award tokens |
| **WebApp** (or internal services) | HTTP REST | Token balance, transactions, spending |

## What This Service Calls

| Service | How | Purpose |
|---|---|---|
| **Ordering.API** (indirect) | RabbitMQ event publishing | Publishes payment succeeded/failed events |

## API Endpoints

All token endpoints under `/api/tokens`:

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `GET` | `/api/tokens/balance` | **Required** | None (user from auth context) | `{ balance: int }` |
| `GET` | `/api/tokens/transactions?page=X&pageSize=Y` | **Required** | Query params | `{ totalCount, page, pageSize, items[] }` |
| `GET` | `/api/tokens/reward-preview?category=X&condition=Y` | No | Query params | `{ tokenAmount: int, tableVersion: string }` |
| `POST` | `/api/tokens/spend` | **Required** | `{ userId, amount, orderId }` | `{ newBalance: int }` |

**Endpoint definitions**: `src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs`

## Database Schema

**DbContext**: `TokenDbContext` (`src/PaymentProcessor/TokenLedger/Infrastructure/TokenDbContext.cs`)  
**Database**: PostgreSQL (`tokendb`)

### Tables

| Table | Entity | Purpose |
|---|---|---|
| `TokenWallets` | `TokenWallet` | User token balances |
| `TokenTransactions` | `TokenTransaction` | Transaction history (credits/debits) |
| `TokenAwardLookupEntries` | `TokenAwardLookupEntry` | Token amounts by category/condition |
| `TokenAwardedListings` | `TokenAwardedListing` | Tracks which listings have been awarded (idempotency) |

### TokenWallet Entity

| Field | Type | Notes |
|---|---|---|
| `UserId` | `string` | PK |
| `Balance` | `int` | Current token balance |
| `xmin` | `uint` | PostgreSQL system column for optimistic concurrency |

### TokenTransaction Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `string` | FK (conceptual) |
| `Amount` | `int` | Positive=credit, Negative=debit |
| `Reason` | `string` | Human-readable description |
| `RelatedEventId` | `Guid?` | Unique index — idempotency key |
| `CreatedAt` | `DateTime` | Timestamp |

Index: `(UserId, CreatedAt)`  
Unique index: `RelatedEventId`

### TokenAwardLookupEntry Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `ClubCategory` | `string` | e.g., "Driver" |
| `ConditionGrade` | `string` | e.g., "Excellent" |
| `TokenAmount` | `int` | Tokens to award |
| `TableVersion` | `string` | Version identifier |
| `EffectiveDate` | `DateTime` | When entry becomes active |
| `IsActive` | `bool` | Whether entry is active |

Index: `(ClubCategory, ConditionGrade, TableVersion)`

### TokenAwardedListing Entity

| Field | Type | Notes |
|---|---|---|
| `CatalogItemId` | `string` | PK — prevents double-awarding |

### Data Modification

- Token wallet updates use optimistic concurrency with retry (up to `MaxConcurrencyRetries` = 3)
- `DbUpdateConcurrencyException` triggers retry loop
- Idempotency checked via `RelatedEventId` unique constraint and `TokenAwardedListings`

### Migrations

- `src/PaymentProcessor/TokenLedger/Infrastructure/Migrations/`

### Seed Data

- `src/PaymentProcessor/TokenLedger/Infrastructure/TokenDbSeeder.cs`

## Integration Events

### Published Events

| Event | Trigger | Payload |
|---|---|---|
| `OrderPaymentSucceededIntegrationEvent` | `PaymentSucceeded` config = true | `{ OrderId: int }` |
| `OrderPaymentFailedIntegrationEvent` | `PaymentSucceeded` config = false | `{ OrderId: int }` |

### Consumed Events

| Event | Source | Handler | Action |
|---|---|---|---|
| `OrderStatusChangedToStockConfirmedIntegrationEvent` | Ordering.API | `OrderStatusChangedToStockConfirmedIntegrationEventHandler` | Simulate payment → publish result |
| `ClubListingVerifiedIntegrationEvent` | Catalog.API | `ClubListingVerifiedIntegrationEventHandler` | Award tokens to seller |

### Event Structures

```csharp
// Consumed
public record OrderStatusChangedToStockConfirmedIntegrationEvent(int OrderId) : IntegrationEvent;

public record ClubListingVerifiedIntegrationEvent(
    string SellerId,
    string CatalogItemId,
    string Category,
    string Condition
) : IntegrationEvent;

// Published
public record OrderPaymentSucceededIntegrationEvent(int OrderId) : IntegrationEvent;
public record OrderPaymentFailedIntegrationEvent(int OrderId) : IntegrationEvent;
```

## Configuration

**`PaymentOptions`** (`src/PaymentProcessor/PaymentOptions.cs`):

| Option | Purpose |
|---|---|
| `PaymentSucceeded` | `bool` — controls whether payment simulation succeeds or fails |

**`TokenOptions`** (`src/PaymentProcessor/TokenOptions.cs`):

| Option | Purpose |
|---|---|
| `MaxConcurrencyRetries` | Max retry count for optimistic concurrency conflicts (default: 3) |

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** | Aspire.Npgsql.EFCore | Token ledger data |
| **RabbitMQ** | EventBusRabbitMQ | Event consumption and publishing |
| **Identity.API** | JWT Bearer validation | Authentication for token endpoints |

### Project References

- `eShop.ServiceDefaults` — Auth, telemetry, health checks
- `EventBusRabbitMQ` — RabbitMQ event bus

### Linked Files

- `src/Shared/ActivityExtensions.cs` → `Extensions/ActivityExtensions.cs`
- `src/Shared/MigrateDbContextExtensions.cs` → `Extensions/MigrateDbContextExtensions.cs`

### NuGet Packages

- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` — PostgreSQL + EF Core
- `Microsoft.EntityFrameworkCore.Tools` — Migrations CLI

## Core Services & Classes

### TokenLedgerService (`src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs`)

Main service managing token wallet lifecycle and transactions.

| Method | Purpose |
|---|---|
| `AwardTokens(ClubListingVerifiedIntegrationEvent)` | Award tokens to seller (idempotent, with concurrency retry) |
| `GetBalance(string userId)` | Get user's token balance (0 if no wallet) |
| `GetTransactions(string userId, int page, int pageSize)` | Paginated transaction history (max 100 per page) |
| `GetRewardPreview(string category, string condition)` | Preview token amount for category/condition |
| `SpendTokens(string userId, int amount, string orderId)` | Debit tokens (idempotent by orderId, with concurrency retry) |

**SpendResult enum**: `Success`, `InsufficientBalance`, `RetriesExhausted`, `InvalidAmount`

### OrderStatusChangedToStockConfirmedIntegrationEventHandler

Simulates payment based on `PaymentOptions.PaymentSucceeded` flag. Publishes `OrderPaymentSucceededIntegrationEvent` or `OrderPaymentFailedIntegrationEvent`.

### ClubListingVerifiedIntegrationEventHandler

Delegates to `TokenLedgerService.AwardTokens()`.

## File Structure

```
src/PaymentProcessor/
├── PaymentProcessor.csproj
├── Program.cs                                     # Entry point
├── GlobalUsings.cs
├── PaymentOptions.cs                              # Payment simulation config
├── TokenOptions.cs                                # Token concurrency config
├── appsettings.json
├── appsettings.Development.json
├── Properties/
│   └── launchSettings.json
├── IntegrationEvents/
│   ├── EventHandling/
│   │   ├── OrderStatusChangedToStockConfirmedIntegrationEventHandler.cs
│   │   └── ClubListingVerifiedIntegrationEventHandler.cs
│   └── Events/
│       ├── ClubListingVerifiedIntegrationEvent.cs
│       ├── OrderPaymentFailedIntegrationEvent.cs
│       ├── OrderPaymentSucceededIntegrationEvent.cs
│       └── OrderStatusChangedToStockConfirmedIntegrationEvent.cs
└── TokenLedger/
    ├── Apis/
    │   └── TokensApi.cs                           # REST endpoints
    ├── Infrastructure/
    │   ├── TokenDbContext.cs                       # EF DbContext
    │   ├── TokenDbSeeder.cs                        # Seed data
    │   └── Migrations/                             # EF migrations
    ├── Model/
    │   ├── TokenWallet.cs                          # Wallet entity
    │   ├── TokenTransaction.cs                     # Transaction entity
    │   ├── TokenAwardLookupEntry.cs                # Lookup table entity
    │   └── TokenAwardedListing.cs                  # Idempotency entity
    └── Services/
        ├── TokenLedgerService.cs                   # Core business logic
        └── TokenLedgerExtensions.cs                # DI registration
```

## Related Test Projects

- `tests/PaymentProcessor.UnitTests/` — Unit tests for token ledger logic
