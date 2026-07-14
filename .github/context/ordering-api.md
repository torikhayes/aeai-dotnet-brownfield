# Ordering.API — Service Context

## Overview

**Projects**:
- `src/Ordering.API/` — Application layer (API, commands, queries, handlers)
- `src/Ordering.Domain/` — Domain model (aggregates, entities, value objects, domain events)
- `src/Ordering.Infrastructure/` — Data access (EF Core, repositories, migrations)

**Type**: ASP.NET Core Minimal API  
**Protocol**: HTTP REST  
**Database**: PostgreSQL (`orderdb`)  
**Framework**: .NET 10.0  

The Ordering.API manages the complete order lifecycle. It implements Domain-Driven Design (DDD) with CQRS pattern, MediatR for command/query mediation, and an event-driven architecture for inter-service coordination.

## Architecture

- **DDD (Domain-Driven Design)**: Aggregate roots (`Order`, `Buyer`), value objects (`Address`), domain events
- **CQRS**: Separate command handlers (writes) and query handlers (reads)
- **MediatR**: Pipeline for command dispatching, validation, logging, and transactions
- **Unit of Work**: `OrderingContext.SaveEntitiesAsync()` dispatches domain events within transactions
- **Idempotency**: `IdentifiedCommand<T,R>` wrapper prevents duplicate request processing via `x-requestid` header
- **Outbox Pattern**: Integration events persisted to DB before publishing to RabbitMQ
- **Repository Pattern**: `IOrderRepository`, `IBuyerRepository`

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **WebApp** | HTTP REST | Create orders, view order history, cancel/ship |
| **OrderProcessor** | RabbitMQ event | Grace period confirmation |
| **Catalog.API** | RabbitMQ event | Stock confirmed/rejected |
| **PaymentProcessor** | RabbitMQ event | Payment succeeded/failed |

## What Calls This Service

- **WebApp** → HTTP REST for order creation and history
- **OrderProcessor** → `GracePeriodConfirmedIntegrationEvent`
- **Catalog.API** → `OrderStockConfirmedIntegrationEvent` / `OrderStockRejectedIntegrationEvent`
- **PaymentProcessor** → `OrderPaymentSucceededIntegrationEvent` / `OrderPaymentFailedIntegrationEvent`

## API Endpoints

All endpoints require authorization and are versioned under `api/orders` (v1.0).

| Method | Route | Command/Query | Request | Response |
|---|---|---|---|---|
| `POST` | `/api/orders` | `CreateOrderCommand` (via `IdentifiedCommand`) | `CreateOrderRequest` + `x-requestid` header | `200 OK` or `400` |
| `POST` | `/api/orders/draft` | `CreateOrderDraftCommand` | `CreateOrderDraftCommand` body | `OrderDraftDTO` |
| `PUT` | `/api/orders/cancel` | `CancelOrderCommand` (via `IdentifiedCommand`) | `CancelOrderCommand` + `x-requestid` | `200 OK` or `400` |
| `PUT` | `/api/orders/ship` | `ShipOrderCommand` (via `IdentifiedCommand`) | `ShipOrderCommand` + `x-requestid` | `200 OK` or `400` |
| `GET` | `/api/orders/{orderId:int}` | `IOrderQueries.GetOrderAsync()` | Path: `int orderId` | `Order` or `404` |
| `GET` | `/api/orders` | `IOrderQueries.GetOrdersFromUserAsync()` | Auth context | `IEnumerable<OrderSummary>` |
| `GET` | `/api/orders/cardtypes` | `IOrderQueries.GetCardTypesAsync()` | None | `IEnumerable<CardType>` |

### Request Types

**CreateOrderRequest**:
```json
{
  "UserId": "string",
  "UserName": "string",
  "City": "string",
  "Street": "string",
  "State": "string",
  "Country": "string",
  "ZipCode": "string",
  "CardNumber": "string",
  "CardHolderName": "string",
  "CardExpiration": "datetime",
  "CardSecurityNumber": "string",
  "CardTypeId": 0,
  "Buyer": "string",
  "Items": [{ "ProductId": 0, "ProductName": "string", "UnitPrice": 0, "Quantity": 0, "PictureUrl": "string" }]
}
```

**Endpoint definitions**: `src/Ordering.API/Apis/OrdersApi.cs`

## Database Schema

**DbContext**: `OrderingContext` (`src/Ordering.Infrastructure/OrderingContext.cs`)  
**Database**: PostgreSQL (`orderdb`)  
**Schema**: `ordering`

### Tables

| Table | Entity | Key Generation | Purpose |
|---|---|---|---|
| `orders` | `Order` | HiLo sequence (`orderseq`) | Main order records |
| `orderitems` | `OrderItem` | Auto | Line items in orders |
| `buyers` | `Buyer` | HiLo sequence (`buyerseq`) | Customer entities |
| `paymentmethods` | `PaymentMethod` | HiLo sequence (`paymentseq`) | Payment card info |
| `cardtypes` | `CardType` | Auto | Lookup (Visa, MC, Amex) |
| `ClientRequest` | `ClientRequest` | GUID | Idempotency tracking |
| `IntegrationEventLog` | `IntegrationEventLogEntry` | GUID | Outbox event log |

### Order Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK (HiLo) |
| `OrderDate` | `DateTime` | |
| `Address` | `Address` (owned) | Value object: Street, City, State, Country, ZipCode |
| `BuyerId` | `int?` | FK → Buyer |
| `OrderStatus` | `OrderStatus` | Enum: Submitted → AwaitingValidation → StockConfirmed → Paid → Shipped → Cancelled |
| `Description` | `string` | |
| `PaymentId` | `int?` | FK → PaymentMethod |
| `OrderItems` | Collection | Private; accessed via aggregate methods |

### OrderItem Entity

| Field | Type |
|---|---|
| `ProductName` | `string` |
| `PictureUrl` | `string` |
| `UnitPrice` | `decimal` |
| `Discount` | `decimal` |
| `Units` | `int` |
| `ProductId` | `int` |

### Data Modification

- Write operations through repository pattern (`OrderRepository`, `BuyerRepository`)
- `SaveEntitiesAsync()` dispatches domain events before committing
- `TransactionBehavior` wraps commands in DB transactions and publishes integration events post-commit

### Entity Configurations

- `src/Ordering.Infrastructure/EntityConfigurations/OrderEntityTypeConfiguration.cs`
- `src/Ordering.Infrastructure/EntityConfigurations/OrderItemEntityTypeConfiguration.cs`
- `src/Ordering.Infrastructure/EntityConfigurations/BuyerEntityTypeConfiguration.cs`
- `src/Ordering.Infrastructure/EntityConfigurations/PaymentMethodEntityTypeConfiguration.cs`
- `src/Ordering.Infrastructure/EntityConfigurations/CardTypeEntityTypeConfiguration.cs`
- `src/Ordering.Infrastructure/EntityConfigurations/ClientRequestEntityTypeConfiguration.cs`

### Migrations

- `src/Ordering.Infrastructure/Migrations/`

## Order State Machine

```
Submitted
    │
    ▼ (grace period expires → GracePeriodConfirmedIntegrationEvent)
AwaitingValidation
    │
    ├──► StockConfirmed (stock available)
    │       │
    │       ▼ (payment processed)
    │     Paid
    │       │
    │       ▼ (admin ships)
    │     Shipped ✓
    │
    └──► Cancelled ✗ (stock rejected, payment failed, or user cancel)
```

## Integration Events

### Published Events

| Event | Trigger | Payload |
|---|---|---|
| `OrderStartedIntegrationEvent` | Order created | `{ UserId }` |
| `OrderStatusChangedToSubmittedIntegrationEvent` | Buyer/payment validated | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid }` |
| `OrderStatusChangedToAwaitingValidationIntegrationEvent` | Grace period confirmed | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid, OrderStockItems[] }` |
| `OrderStatusChangedToStockConfirmedIntegrationEvent` | Stock confirmed | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid }` |
| `OrderStatusChangedToPaidIntegrationEvent` | Payment succeeded | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid, OrderStockItems[] }` |
| `OrderStatusChangedToShippedIntegrationEvent` | Order shipped | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid }` |
| `OrderStatusChangedToCancelledIntegrationEvent` | Order cancelled | `{ OrderId, OrderStatus, BuyerName, BuyerIdentityGuid }` |

### Consumed Events

| Event | Source | Handler | Action |
|---|---|---|---|
| `GracePeriodConfirmedIntegrationEvent` | OrderProcessor | `GracePeriodConfirmedIntegrationEventHandler` | Transition to AwaitingValidation |
| `OrderStockConfirmedIntegrationEvent` | Catalog.API | `OrderStockConfirmedIntegrationEventHandler` | Transition to StockConfirmed |
| `OrderStockRejectedIntegrationEvent` | Catalog.API | `OrderStockRejectedIntegrationEventHandler` | Cancel order |
| `OrderPaymentSucceededIntegrationEvent` | PaymentProcessor | `OrderPaymentSucceededIntegrationEventHandler` | Transition to Paid |
| `OrderPaymentFailedIntegrationEvent` | PaymentProcessor | `OrderPaymentFailedIntegrationEventHandler` | Handle payment failure |

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** | Aspire.Npgsql.EFCore | Order data storage |
| **RabbitMQ** | EventBusRabbitMQ | Event publishing/consuming |
| **Identity.API** | JWT Bearer validation | Authentication |

### Project References (Ordering.API)

- `Ordering.Domain` — Domain model
- `Ordering.Infrastructure` — Data access
- `EventBusRabbitMQ` — Event bus
- `IntegrationEventLogEF` — Outbox pattern
- `eShop.ServiceDefaults` — Auth, telemetry

### NuGet Packages

**Ordering.API**: `Asp.Versioning.Http`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.EntityFrameworkCore.Tools`  
**Ordering.Domain**: `MediatR`, `System.Reflection.TypeExtensions`  
**Ordering.Infrastructure**: `Npgsql.EntityFrameworkCore.PostgreSQL`

## Core Services & Classes

### Domain Aggregates (Ordering.Domain)

| Class | Type | Location |
|---|---|---|
| `Order` | Aggregate Root | `src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs` |
| `OrderItem` | Entity | `src/Ordering.Domain/AggregatesModel/OrderAggregate/OrderItem.cs` |
| `Address` | Value Object | `src/Ordering.Domain/AggregatesModel/OrderAggregate/Address.cs` |
| `OrderStatus` | Enum | `src/Ordering.Domain/AggregatesModel/OrderAggregate/OrderStatus.cs` |
| `Buyer` | Aggregate Root | `src/Ordering.Domain/AggregatesModel/BuyerAggregate/Buyer.cs` |
| `PaymentMethod` | Entity | `src/Ordering.Domain/AggregatesModel/BuyerAggregate/PaymentMethod.cs` |
| `CardType` | Entity | `src/Ordering.Domain/AggregatesModel/BuyerAggregate/CardType.cs` |

### DDD Seed Work (Ordering.Domain/SeedWork)

| Class | Purpose |
|---|---|
| `Entity` | Base entity with domain event support |
| `ValueObject` | Base value object (equality by values) |
| `IAggregateRoot` | Marker interface |
| `IRepository<T>` | Generic repository interface |
| `IUnitOfWork` | Unit of work pattern interface |

### Commands (Ordering.API/Application/Commands)

| Command | Handler | Purpose |
|---|---|---|
| `CreateOrderCommand` | `CreateOrderCommandHandler` | Create order with items and buyer info |
| `CreateOrderDraftCommand` | `CreateOrderDraftCommandHandler` | Create draft (preview) |
| `CancelOrderCommand` | `CancelOrderCommandHandler` | Cancel order |
| `ShipOrderCommand` | `ShipOrderCommandHandler` | Mark as shipped |
| `SetAwaitingValidationOrderStatusCommand` | Handler | Transition to AwaitingValidation |
| `SetStockConfirmedOrderStatusCommand` | Handler | Transition to StockConfirmed |
| `SetPaidOrderStatusCommand` | Handler | Transition to Paid |
| `SetStockRejectedOrderStatusCommand` | Handler | Handle stock rejection |
| `IdentifiedCommand<T,R>` | `IdentifiedCommandHandler<T,R>` | Idempotency wrapper |

### Domain Event Handlers (Ordering.API/Application/DomainEventHandlers)

| Domain Event | Handler | Action |
|---|---|---|
| `OrderStartedDomainEvent` | `ValidateOrAddBuyerAggregate...` | Create/validate buyer, verify payment |
| `BuyerAndPaymentMethodVerifiedDomainEvent` | `UpdateOrderWhenBuyer...` | Link buyer + payment to order |
| `OrderStatusChangedToPaidDomainEvent` | Handler | Publish paid integration event |
| `OrderStatusChangedToAwaitingValidationDomainEvent` | Handler | Publish awaiting validation event |
| `OrderStatusChangedToStockConfirmedDomainEvent` | Handler | Publish stock confirmed event |
| `OrderShippedDomainEvent` | Handler | Publish shipped event |
| `OrderCancelledDomainEvent` | Handler | Publish cancelled event |

### MediatR Pipeline Behaviors

| Behavior | Purpose |
|---|---|
| `LoggingBehavior<T,R>` | Log all commands/queries |
| `ValidatorBehavior<T,R>` | FluentValidation before handler |
| `TransactionBehavior<T,R>` | DB transaction + event publishing after commit |

### Queries (Ordering.API/Application/Queries)

| Method | Returns |
|---|---|
| `GetOrderAsync(id)` | Single order with items |
| `GetOrdersFromUserAsync(userId)` | All orders for user |
| `GetCardTypesAsync()` | Payment card types |

### Repositories (Ordering.Infrastructure)

| Repository | Interface | Key Methods |
|---|---|---|
| `OrderRepository` | `IOrderRepository` | `Add()`, `Update()`, `GetAsync()` |
| `BuyerRepository` | `IBuyerRepository` | `Add()`, `Update()`, `FindAsync(identity)`, `FindByIdAsync(id)` |

## Execution Flow

```
1. POST /api/orders + x-requestid header
2. IdentifiedCommandHandler checks idempotency
3. CreateOrderCommandHandler:
   - Creates Order aggregate with Address and OrderItems
   - Raises OrderStartedDomainEvent
   - Saves via OrderRepository
4. OrderingContext.SaveEntitiesAsync():
   - Dispatches OrderStartedDomainEvent
   - ValidateOrAddBuyerAggregateHandler: creates Buyer, publishes OrderStatusChangedToSubmittedIntegrationEvent
   - Commits transaction
5. TransactionBehavior publishes integration events to RabbitMQ
6. OrderStartedIntegrationEvent → Basket.API (clears cart)
7. Downstream events drive state machine transitions
```

## File Structure

```
src/Ordering.API/
├── Apis/
│   ├── OrdersApi.cs                        # Endpoint definitions
│   └── OrderServices.cs                    # DI container (IMediator, IOrderQueries, etc.)
├── Application/
│   ├── Behaviors/                          # MediatR pipeline (Logging, Validation, Transaction)
│   ├── Commands/                           # All command + handler pairs
│   ├── DomainEventHandlers/                # Domain event → integration event handlers
│   ├── IntegrationEvents/
│   │   ├── Events/                         # All integration event definitions
│   │   └── EventHandling/                  # Integration event handlers
│   ├── Models/                             # BasketItem, CustomerBasket DTOs
│   ├── Queries/                            # IOrderQueries + implementation + ViewModels
│   └── Validations/                        # FluentValidation validators
├── Extensions/                             # DI setup, LINQ helpers, tracing
├── Infrastructure/Services/
│   └── IdentityService.cs                  # Extract user from HttpContext
├── Program.cs
└── appsettings.json

src/Ordering.Domain/
├── AggregatesModel/
│   ├── BuyerAggregate/                     # Buyer, PaymentMethod, CardType, IBuyerRepository
│   └── OrderAggregate/                     # Order, OrderItem, Address, OrderStatus, IOrderRepository
├── Events/                                 # All domain event definitions
├── Exceptions/
│   └── OrderingDomainException.cs
└── SeedWork/                               # Entity, ValueObject, IAggregateRoot, IRepository, IUnitOfWork

src/Ordering.Infrastructure/
├── OrderingContext.cs                      # DbContext + SaveEntitiesAsync
├── OrderingContextSeed.cs                  # Seed data
├── Repositories/                           # OrderRepository, BuyerRepository
├── EntityConfigurations/                   # All EF Fluent API configs
├── Idempotency/                            # ClientRequest, IRequestManager, RequestManager
├── Migrations/                             # EF migrations
└── MediatorExtension.cs                    # Domain event dispatching
```

## Related Test Projects

- `tests/Ordering.UnitTests/` — Unit tests for domain logic and commands
- `tests/Ordering.FunctionalTests/` — Functional/integration tests
