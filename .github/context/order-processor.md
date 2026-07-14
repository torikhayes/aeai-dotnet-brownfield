# OrderProcessor — Service Context

## Overview

**Project**: `src/OrderProcessor/`  
**Type**: .NET Worker Service (Background Service)  
**Protocol**: RabbitMQ (publish only)  
**Database**: PostgreSQL (`orderdb`) — read-only direct SQL  
**Framework**: .NET 10.0 (AOT publishing support)  

The OrderProcessor is a background worker that monitors the Ordering database for orders whose grace period has expired. When an order's grace period passes, it publishes a `GracePeriodConfirmedIntegrationEvent` to advance the order through the state machine.

## Architecture

- **Pattern**: `BackgroundService` (hosted service) with polling loop
- **Data Access**: Raw ADO.NET via `NpgsqlDataSource` (direct SQL, no EF Core)
- **Event Publishing**: RabbitMQ via EventBusRabbitMQ
- **No API endpoints**: Pure background worker

## How It's Called

This service is **not called by other services**. It runs autonomously as a background worker.

## What This Service Calls

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** (`orderdb`) | Direct SQL via Npgsql | Query for expired grace period orders |
| **RabbitMQ** | EventBusRabbitMQ | Publish `GracePeriodConfirmedIntegrationEvent` |
| **Ordering.API** (indirect) | RabbitMQ event | Triggers state transition via published events |

## Integration Events

### Published Events

| Event | Trigger | Payload |
|---|---|---|
| `GracePeriodConfirmedIntegrationEvent` | Order's grace period expired | `{ OrderId: int }` |

**Event Structure**:
```csharp
public record GracePeriodConfirmedIntegrationEvent(int OrderId) : IntegrationEvent;
```

### Consumed Events

**None.** OrderProcessor is a publish-only service.

## Database Access

**Connection**: `orderingdb` via `NpgsqlDataSource`  
**Access Pattern**: Direct SQL queries (no EF Core)

### SQL Query

The service queries the `ordering.orders` table for orders with:
- Status = `Submitted`
- `OrderDate` + `GracePeriodTime` ≤ current UTC time

```sql
-- Conceptual query (executed via Npgsql)
SELECT Id FROM ordering.orders
WHERE OrderDate + GracePeriodTime <= NOW()
AND OrderStatus = 'Submitted'
```

**Error handling**: Catches `NpgsqlException`, logs warning, returns empty list on DB failure.

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** | Aspire.Npgsql (NpgsqlDataSource) | Read order data |
| **RabbitMQ** | EventBusRabbitMQ | Event publishing |

### Project References

- `eShop.ServiceDefaults` — Telemetry, health checks
- `EventBusRabbitMQ` — RabbitMQ event bus

### NuGet Packages

- `Aspire.Npgsql` — Aspire-integrated Npgsql

## Configuration

**`BackgroundTaskOptions`** (`src/OrderProcessor/BackgroundTaskOptions.cs`):

| Option | Purpose |
|---|---|
| `CheckUpdateTime` | Polling interval in seconds |
| `GracePeriodTime` | Grace period duration before confirming order |

## Core Services & Classes

### GracePeriodManagerService (`src/OrderProcessor/Services/GracePeriodManagerService.cs`)

The single background service that drives all processing.

| Method | Purpose |
|---|---|
| `ExecuteAsync(CancellationToken)` | Main polling loop |
| `CheckConfirmedGracePeriodOrders()` | Fetch expired orders and publish events |
| `GetConfirmedGracePeriodOrders()` | SQL query returning order IDs |

## File Structure

```
src/OrderProcessor/
├── OrderProcessor.csproj                # Project file (Worker SDK)
├── Program.cs                           # Entry point
├── GlobalUsings.cs
├── BackgroundTaskOptions.cs             # Polling & grace period config
├── appsettings.json
├── appsettings.Development.json
├── Properties/
│   └── launchSettings.json
├── Events/
│   └── GracePeriodConfirmedIntegrationEvent.cs  # Event definition
├── Extensions/
│   └── Extensions.cs                    # DI setup
└── Services/
    └── GracePeriodManagerService.cs     # Background worker
```

## Related Test Projects

None dedicated. Grace period logic is tested indirectly through Ordering functional tests.
