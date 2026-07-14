# Basket.API — Service Context

## Overview

**Project**: `src/Basket.API/`  
**Type**: gRPC Service  
**Protocol**: gRPC (Protocol Buffers)  
**Database**: Redis (in-memory key-value store)  
**Framework**: .NET 10.0 (AOT publishing support)  

The Basket.API is a lightweight shopping cart microservice that manages customer baskets during the checkout flow. It stores baskets in Redis and clears them when orders are placed.

## Architecture

- **Pattern**: gRPC-only service (no HTTP/REST endpoints)
- **Data Access**: Redis via `RedisBasketRepository`
- **Event-Driven**: Consumes events from RabbitMQ (does not publish any)
- **Authentication**: gRPC metadata-based auth via Identity.API

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **WebApp** | gRPC | Fetch/update/delete shopping basket |
| **Ordering.API** (indirect) | RabbitMQ event | Clears basket when order starts |

## What Calls This Service

- **WebApp** → gRPC client for basket CRUD
- **Ordering.API** → Publishes `OrderStartedIntegrationEvent` (consumed by Basket.API)

## API Endpoints (gRPC)

All endpoints defined in `src/Basket.API/Proto/basket.proto` and implemented in `src/Basket.API/Grpc/BasketService.cs`.

| gRPC Method | Full Route | Request | Response | Auth |
|---|---|---|---|---|
| `GetBasket` | `BasketApi.Basket/GetBasket` | `GetBasketRequest {}` | `CustomerBasketResponse` | Required |
| `UpdateBasket` | `BasketApi.Basket/UpdateBasket` | `UpdateBasketRequest { items[] }` | `CustomerBasketResponse` | Required |
| `DeleteBasket` | `BasketApi.Basket/DeleteBasket` | `DeleteBasketRequest {}` | `DeleteBasketResponse {}` | Required |

### Protocol Buffer Definitions

```protobuf
service Basket {
  rpc GetBasket(GetBasketRequest) returns (CustomerBasketResponse);
  rpc UpdateBasket(UpdateBasketRequest) returns (CustomerBasketResponse);
  rpc DeleteBasket(DeleteBasketRequest) returns (DeleteBasketResponse);
}

message GetBasketRequest {}
message UpdateBasketRequest { repeated BasketItem items = 2; }
message DeleteBasketRequest {}
message DeleteBasketResponse {}
message CustomerBasketResponse { repeated BasketItem items = 1; }
message BasketItem { int32 product_id = 2; int32 quantity = 6; }
```

**Proto file**: `src/Basket.API/Proto/basket.proto`

## Database

**Store**: Redis (in-memory key-value)  
**Key Format**: `/basket/{userId}` (UTF-8 byte array prefix)  
**Data Format**: JSON serialization of `CustomerBasket` objects  

### Data Operations

| Operation | Redis Command | Method |
|---|---|---|
| Get basket | `GET /basket/{userId}` | `RedisBasketRepository.GetBasketAsync(customerId)` |
| Set basket | `SET /basket/{userId}` | `RedisBasketRepository.UpdateBasketAsync(basket)` |
| Delete basket | `DEL /basket/{userId}` | `RedisBasketRepository.DeleteBasketAsync(id)` |

### Domain Models

**CustomerBasket** (`src/Basket.API/Model/CustomerBasket.cs`):
```csharp
public class CustomerBasket
{
    public string BuyerId { get; set; }
    public List<BasketItem> Items { get; set; }
}
```

**BasketItem** (`src/Basket.API/Model/BasketItem.cs`):
```csharp
public class BasketItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    // Additional: ProductName, UnitPrice, OldUnitPrice, PictureUrl, Id
}
```

## Integration Events

### Published Events

**None.** Basket.API is a consumer-only service.

### Consumed Events

| Event | Source | Handler | Action |
|---|---|---|---|
| `OrderStartedIntegrationEvent` | Ordering.API | `OrderStartedIntegrationEventHandler` | Deletes customer's basket from Redis |

**Event Structure**:
```csharp
public record OrderStartedIntegrationEvent(string UserId) : IntegrationEvent;
```

When an order is placed, the Ordering.API publishes this event, and Basket.API clears the user's basket.

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **Redis** | Aspire.StackExchange.Redis | Session/basket storage |
| **RabbitMQ** | EventBusRabbitMQ | Event consumption |
| **Identity.API** | OpenID Connect (via gRPC context) | Authentication |

### Project References

- `eShop.ServiceDefaults` — Auth, telemetry, health checks
- `EventBusRabbitMQ` — RabbitMQ event bus

### NuGet Packages

- `Aspire.StackExchange.Redis` — Redis client via Aspire
- `Grpc.AspNetCore` — gRPC framework

## Core Services & Classes

### BasketService (`src/Basket.API/Grpc/BasketService.cs`)

gRPC service implementing all basket operations. Extracts user identity from gRPC `ServerCallContext`.

| Method | Purpose |
|---|---|
| `GetBasket()` | Retrieve basket for authenticated user |
| `UpdateBasket()` | Create/update basket with items |
| `DeleteBasket()` | Remove basket |

### IBasketRepository / RedisBasketRepository

- Interface: `src/Basket.API/Repositories/IBasketRepository.cs`
- Implementation: `src/Basket.API/Repositories/RedisBasketRepository.cs`

| Method | Purpose |
|---|---|
| `GetBasketAsync(customerId)` | Redis GET → deserialize JSON |
| `UpdateBasketAsync(basket)` | Serialize JSON → Redis SET |
| `DeleteBasketAsync(id)` | Redis DEL |

### OrderStartedIntegrationEventHandler

`src/Basket.API/IntegrationEvents/EventHandling/OrderStartedIntegrationEventHandler.cs`

Handles `OrderStartedIntegrationEvent` by calling `DeleteBasketAsync(userId)`.

### ServerCallContextIdentityExtensions

`src/Basket.API/Extensions/ServerCallContextIdentityExtensions.cs`

Extracts user identity from gRPC `ServerCallContext` metadata.

## File Structure

```
src/Basket.API/
├── Basket.API.csproj                              # Project file
├── Program.cs                                     # Entry point (gRPC, auth, Redis, RabbitMQ)
├── GlobalUsings.cs
├── appsettings.json
├── appsettings.Development.json
├── Model/
│   ├── BasketItem.cs                              # Cart item model
│   └── CustomerBasket.cs                          # Basket aggregate
├── Grpc/
│   └── BasketService.cs                           # gRPC service implementation
├── Proto/
│   └── basket.proto                               # Protobuf definitions
├── Repositories/
│   ├── IBasketRepository.cs                       # Data access interface
│   └── RedisBasketRepository.cs                   # Redis implementation
├── Extensions/
│   ├── Extensions.cs                              # DI setup (Redis, RabbitMQ, Auth)
│   └── ServerCallContextIdentityExtensions.cs     # User ID extraction from gRPC
├── IntegrationEvents/
│   ├── Events/
│   │   └── OrderStartedIntegrationEvent.cs        # Event definition
│   └── EventHandling/
│       └── OrderStartedIntegrationEventHandler.cs # Clears basket on order start
└── Properties/
    └── launchSettings.json
```

## Related Test Projects

- `tests/Basket.UnitTests/` — Unit tests for basket logic
