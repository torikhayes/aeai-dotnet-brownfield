# Webhooks.API — Service Context

## Overview

**Project**: `src/Webhooks.API/`  
**Type**: ASP.NET Core Minimal API  
**Protocol**: HTTP REST + RabbitMQ  
**Database**: PostgreSQL (`webhooksdb`)  
**Framework**: .NET 10.0  

The Webhooks.API is a subscription management service for webhook notifications. Users register webhook URLs that are triggered when specific events occur (catalog price changes, order shipment, order payment).

## Architecture

- **Pattern**: Minimal APIs with API versioning (`/api/webhooks/v1.0`)
- **Data Access**: Entity Framework Core (`WebhooksContext`)
- **Event-Driven**: Consumes RabbitMQ events and sends HTTP POST notifications to registered webhook URLs
- **Authentication**: Bearer token via Identity.API

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **WebhookClient** | HTTP REST | Register/manage webhook subscriptions |
| **Catalog.API** (indirect) | RabbitMQ event | Price change notifications |
| **Ordering.API** (indirect) | RabbitMQ events | Order shipped/paid notifications |

## What This Service Calls

| Service | Protocol | Purpose |
|---|---|---|
| **Registered webhook URLs** | HTTP POST | Deliver webhook payloads to subscriber endpoints |
| **Webhook grant URLs** | HTTP GET | Validate webhook URLs before subscription |

## API Endpoints

All endpoints require Bearer token authorization and are versioned under `/api/webhooks` (v1.0).

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `GET` | `/api/webhooks` | **Required** | None | `WebhookSubscription[]` |
| `GET` | `/api/webhooks/{id}` | **Required** | Path: `int id` | `WebhookSubscription` or `404` |
| `POST` | `/api/webhooks` | **Required** | `WebhookSubscriptionRequest` body | `201 Created` |
| `DELETE` | `/api/webhooks/{id}` | **Required** | Path: `int id` | `202 Accepted` |

### OpenAPI Documentation

- `/openapi/v1.json` — OpenAPI spec
- `/swagger/ui` — Swagger UI

**Endpoint definitions**: `src/Webhooks.API/Apis/WebHooksApi.cs`

### WebhookSubscriptionRequest

```json
{
  "Url": "https://example.com/webhook-receiver",
  "Token": "optional-auth-token",
  "Event": "CatalogItemPriceChange"  // or "OrderShipped" or "OrderPaid"
  "GrantUrl": "https://example.com/check"  // URL for pre-registration validation
}
```

## Database Schema

**DbContext**: `WebhooksContext` (`src/Webhooks.API/Infrastructure/WebhooksContext.cs`)  
**Database**: PostgreSQL (`webhooksdb`)

### Tables

| Table | Entity | Purpose |
|---|---|---|
| `Subscriptions` | `WebhookSubscription` | Registered webhook subscriptions |

### WebhookSubscription Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Type` | `WebhookType` | Enum (see below) |
| `Date` | `DateTime` | Registration date |
| `DestUrl` | `string` | Webhook delivery URL |
| `Token` | `string?` | Optional auth token sent with webhook |
| `UserId` | `string` | Owner user ID |

Indices on `UserId` and `Type`.

### WebhookType Enum

```csharp
public enum WebhookType
{
    CatalogItemPriceChange = 1,
    OrderShipped = 2,
    OrderPaid = 3
}
```

### Data Modification

Direct EF Core `DbContext.SaveChangesAsync()`.

## Integration Events

### Published Events

**None.** Webhooks.API does not publish events; it delivers HTTP webhook notifications.

### Consumed Events

| Event | Source | Handler | Webhook Type |
|---|---|---|---|
| `ProductPriceChangedIntegrationEvent` | Catalog.API | `ProductPriceChangedIntegrationEventHandler` | `CatalogItemPriceChange` |
| `OrderStatusChangedToShippedIntegrationEvent` | Ordering.API | `OrderStatusChangedToShippedIntegrationEventHandler` | `OrderShipped` |
| `OrderStatusChangedToPaidIntegrationEvent` | Ordering.API | `OrderStatusChangedToPaidIntegrationEventHandler` | `OrderPaid` |

### Webhook Delivery Payload

```csharp
public class WebhookData
{
    public DateTime When { get; set; }
    public string Payload { get; set; }  // JSON string of event data
    public string Type { get; set; }     // Event type name
}
```

Delivered via HTTP POST to `DestUrl` with optional `Authorization: Bearer {Token}` header.

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** | Aspire.Npgsql.EFCore | Subscription storage |
| **RabbitMQ** | EventBusRabbitMQ | Event consumption |
| **Identity.API** | JWT Bearer | Authentication |

### Project References

- `eShop.ServiceDefaults` — Auth, telemetry, health checks
- `EventBusRabbitMQ` — Event bus
- `IntegrationEventLogEF` — Event logging

### NuGet Packages

- `Asp.Versioning.Http` — API versioning
- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` — PostgreSQL + EF Core
- `Microsoft.EntityFrameworkCore.Tools` — Migrations

## Core Services & Classes

### WebhooksRetriever (`src/Webhooks.API/Services/WebhooksRetriever.cs`)

Queries the database for subscriptions matching a given `WebhookType`.

### WebhooksSender (`src/Webhooks.API/Services/WebhooksSender.cs`)

Sends HTTP POST requests with `WebhookData` payloads to all registered webhook URLs for a given event type. Includes auth token if configured.

### GrantUrlTesterService (`src/Webhooks.API/Services/GrantUrlTesterService.cs`)

Validates webhook URLs before allowing subscription. Sends a test request to the `GrantUrl` to verify the endpoint is reachable and returns expected response.

### Event Handlers

| Handler | Location | Action |
|---|---|---|
| `ProductPriceChangedIntegrationEventHandler` | `src/Webhooks.API/IntegrationEvents/` | Retrieves subscriptions → sends webhooks |
| `OrderStatusChangedToShippedIntegrationEventHandler` | `src/Webhooks.API/IntegrationEvents/` | Retrieves subscriptions → sends webhooks |
| `OrderStatusChangedToPaidIntegrationEventHandler` | `src/Webhooks.API/IntegrationEvents/` | Retrieves subscriptions → sends webhooks |

## File Structure

```
src/Webhooks.API/
├── Webhooks.API.csproj
├── Program.cs                                          # Entry point
├── appsettings.json
├── Apis/
│   └── WebHooksApi.cs                                  # Minimal API endpoints
├── Infrastructure/
│   └── WebhooksContext.cs                              # EF DbContext
├── Model/
│   ├── WebhookSubscription.cs                          # Subscription entity
│   ├── WebhookSubscriptionRequest.cs                   # Creation DTO
│   ├── WebhookType.cs                                  # Enum
│   └── WebhookData.cs                                  # Delivery payload
├── IntegrationEvents/
│   ├── ProductPriceChangedIntegrationEventHandler.cs
│   ├── OrderStatusChangedToShippedIntegrationEventHandler.cs
│   └── OrderStatusChangedToPaidIntegrationEventHandler.cs
├── Services/
│   ├── WebhooksRetriever.cs                            # Query subscriptions
│   ├── WebhooksSender.cs                               # Deliver webhooks
│   └── GrantUrlTesterService.cs                        # URL validation
├── Extensions/
│   └── Extensions.cs                                   # DI, EventBus subscriptions
├── Exceptions/
│   └── WebhooksDomainException.cs
└── Migrations/                                         # EF migrations
```
