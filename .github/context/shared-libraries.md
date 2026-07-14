# Shared Libraries — Context

## Overview

This document covers the shared infrastructure libraries used across all eShop microservices:

- **eShop.ServiceDefaults** (`src/eShop.ServiceDefaults/`)
- **EventBus** (`src/EventBus/`)
- **EventBusRabbitMQ** (`src/EventBusRabbitMQ/`)
- **IntegrationEventLogEF** (`src/IntegrationEventLogEF/`)
- **Shared** (`src/Shared/`)

---

## eShop.ServiceDefaults

**Project**: `src/eShop.ServiceDefaults/`  
**Type**: Shared extension method library  
**Purpose**: Standardized cross-cutting concerns for all microservices

### What It Provides

#### 1. OpenTelemetry (Observability)

- **Logging**: Formatted messages + scopes
- **Metrics**: AspNetCore, HttpClient, Runtime instrumentation
- **Tracing**: AspNetCore, gRPC, HttpClient
- Conditional OTLP exporter if `OTEL_EXPORTER_OTLP_ENDPOINT` is set

#### 2. Health Checks

- `/health` → All checks must pass (readiness)
- `/alive` → Only "live" tag checks (liveness)

#### 3. Service Discovery & Resilience

- DNS-based service discovery
- Polly retry, circuit breaker, timeout policies on all HTTP clients

#### 4. Authentication

- JWT Bearer from Identity service
- Configurable audience per service
- Scope-based authorization

#### 5. OpenAPI Documentation

- Versioned API docs with Scalar UI
- Security scheme definitions
- Deprecated operation detection

### Key Files

| File | Purpose |
|---|---|
| `Extensions.cs` | `AddServiceDefaults()`, `AddBasicServiceDefaults()`, `ConfigureOpenTelemetry()`, `MapDefaultEndpoints()`, `AddDefaultHealthChecks()` |
| `AuthenticationExtensions.cs` | `AddDefaultAuthentication()` — JWT setup from config |
| `HttpClientExtensions.cs` | `AddAuthToken()` — Injects bearer token into outbound HTTP calls |
| `ClaimsPrincipalExtensions.cs` | `GetUserId()`, `GetUserName()` — Extract claims |
| `ConfigurationExtensions.cs` | `GetRequiredValue()` — Throws if config key missing |
| `OpenApi.Extensions.cs` | `AddDefaultOpenApi()`, `UseDefaultOpenApi()` |
| `OpenApiOptionsExtensions.cs` | Versioning, security, deprecation for OpenAPI |

### Usage Pattern

Every service calls `AddServiceDefaults()` in `Program.cs`:
```csharp
builder.AddServiceDefaults();
// ... service-specific setup
var app = builder.Build();
app.MapDefaultEndpoints();
```

### NuGet Packages

- `Asp.Versioning.Mvc.ApiExplorer`, `Asp.Versioning.OpenApi`
- `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery`
- `OpenTelemetry.*` (Logging, Metrics, Tracing, Exporter)

---

## EventBus

**Project**: `src/EventBus/`  
**Type**: Abstract event bus library  
**Purpose**: Defines the pub/sub contract and event base class

### Core Abstractions

#### IntegrationEvent (Base Class)

```csharp
public record IntegrationEvent
{
    public Guid Id { get; set; }              // Auto-generated UUID
    public DateTime CreationDate { get; set; } // UTC timestamp
}
```

All domain integration events inherit from this record.

#### IEventBus (Publisher)

```csharp
public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
```

#### IIntegrationEventHandler<TEvent> (Subscriber)

```csharp
public interface IIntegrationEventHandler<in TEvent> : IIntegrationEventHandler
    where TEvent : IntegrationEvent
{
    Task Handle(TEvent @event);
}
```

#### IEventBusBuilder

Builder pattern for fluent subscription registration.

#### EventBusSubscriptionInfo

Metadata tracking registered event types, names, and JSON serializer options.

### Subscription Registration Pattern

```csharp
eventBusBuilder.AddSubscription<OrderPlacedEvent, OrderPlacedEventHandler>();
// Registers handler as keyed DI service with event type as key
// Updates EventBusSubscriptionInfo.EventTypes map
```

### Extension Methods

- `AddSubscription<T, TH>()` — Register handler + event type mapping
- `ConfigureJsonOptions()` — Customize JSON serialization (AOT support)
- `GetGenericTypeName()` — Format generic type names for logging

### File Structure

```
src/EventBus/
├── EventBus.csproj
├── GlobalUsings.cs
├── Abstractions/
│   ├── IEventBus.cs
│   ├── IEventBusBuilder.cs
│   ├── IIntegrationEventHandler.cs
│   └── EventBusSubscriptionInfo.cs
├── Events/
│   └── IntegrationEvent.cs
└── Extensions/
    ├── EventBusBuilderExtensions.cs
    └── GenericTypeExtensions.cs
```

---

## EventBusRabbitMQ

**Project**: `src/EventBusRabbitMQ/`  
**Type**: RabbitMQ implementation of EventBus  
**Purpose**: Concrete pub/sub implementation using RabbitMQ

### RabbitMQ Configuration

- **Exchange**: `eshop_event_bus` (direct exchange)
- **Routing Key**: Event type name (e.g., `OrderStartedIntegrationEvent`)
- **Delivery Mode**: Persistent
- **Queue**: Per-service (configured via `EventBusOptions.SubscriptionClientName`)

### Publishing Flow

1. Create channel from RabbitMQ connection
2. Declare `eshop_event_bus` direct exchange
3. Serialize event to JSON
4. Start OpenTelemetry activity (inject trace context into headers)
5. Publish with persistent delivery mode + mandatory flag
6. Retry via Polly resiliency pipeline

### Consuming Flow

1. Declare exchange + queue, bind routing keys for all subscribed events
2. Extract trace context from message headers (OpenTelemetry propagation)
3. Deserialize event → look up handlers from keyed DI
4. Execute each handler sequentially
5. ACK message (always ACKs, even on error)

### Core Classes

#### RabbitMQEventBus

Implements `IEventBus`, `IHostedService`, `IDisposable`.

| Method | Purpose |
|---|---|
| `PublishAsync(IntegrationEvent)` | Serialize → RabbitMQ publish with tracing |
| `OnMessageReceived(BasicDeliverEventArgs)` | Deserialize → route to handlers → ACK |
| `ProcessEvent(eventName, message)` | Look up type, deserialize, invoke handlers |
| `StartAsync()` | Declare exchange/queue, bind keys, start consumer |

#### RabbitMQTelemetry

- Creates `ActivitySource` named `EventBusRabbitMQ`
- Provides `TextMapPropagator` for distributed trace context

#### EventBusOptions

```csharp
public class EventBusOptions
{
    public string SubscriptionClientName { get; set; }  // Queue name
    public int RetryCount { get; set; } = 10;            // Publish retry count
}
```

### Registration

```csharp
builder.AddRabbitMqEventBus("eventbus");
// Adds RabbitMQ connection, registers RabbitMQEventBus as IEventBus + IHostedService
// Configures OpenTelemetry tracing
```

### File Structure

```
src/EventBusRabbitMQ/
├── EventBusRabbitMQ.csproj
├── RabbitMQEventBus.cs                         # Main implementation
├── RabbitMQTelemetry.cs                        # OpenTelemetry integration
├── RabbitMqDependencyInjectionExtensions.cs    # DI registration
├── EventBusOptions.cs                          # Configuration
└── GlobalUsings.cs
```

---

## IntegrationEventLogEF

**Project**: `src/IntegrationEventLogEF/`  
**Type**: EF Core outbox pattern library  
**Purpose**: Reliable event publishing — persists events in DB before sending to message broker

### How It Works (Outbox Pattern)

1. Business transaction saves domain changes + event entry **in same DB transaction**
2. After commit, a separate process reads pending events and publishes to RabbitMQ
3. Events are marked as published/failed with retry tracking

### Event States

```csharp
enum EventStateEnum
{
    NotPublished = 0,    // Initial — waiting to be sent
    InProgress = 1,      // Currently being published
    Published = 2,       // Successfully published
    PublishedFailed = 3  // Failed after retries
}
```

### Core Classes

#### IntegrationEventLogEntry

Database entity representing a pending event.

| Field | Type | Purpose |
|---|---|---|
| `EventId` | `Guid` | PK — unique event ID |
| `EventTypeName` | `string` | Full type name |
| `State` | `EventStateEnum` | Publishing state |
| `TimesSent` | `int` | Retry counter |
| `CreationTime` | `DateTime` | Creation timestamp |
| `Content` | `string` | Full JSON serialization |
| `TransactionId` | `Guid` | Links to DB transaction |

#### IIntegrationEventLogService

```csharp
public interface IIntegrationEventLogService
{
    Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);
    Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);
    Task MarkEventAsPublishedAsync(Guid eventId);
    Task MarkEventAsInProgressAsync(Guid eventId);
    Task MarkEventAsFailedAsync(Guid eventId);
}
```

#### IntegrationLogExtensions

```csharp
// Called in DbContext.OnModelCreating()
builder.UseIntegrationEventLogs();
// Maps IntegrationEventLogEntry to "IntegrationEventLog" table
```

#### ResilientTransaction

Helper that wraps EF Core transaction execution with resilience strategy for transient failures.

### Usage Pattern

```csharp
// In service (e.g., Catalog.API):
var transaction = await dbContext.Database.BeginTransactionAsync();
// Save business changes + event entry atomically
await eventLogService.SaveEventAsync(integrationEvent, transaction);
await dbContext.SaveChangesAsync();
await transaction.CommitAsync();

// After commit, publish to RabbitMQ:
var pending = await eventLogService.RetrieveEventLogsPendingToPublishAsync(txId);
foreach (var evt in pending)
{
    await eventBus.PublishAsync(evt.IntegrationEvent);
    await eventLogService.MarkEventAsPublishedAsync(evt.EventId);
}
```

### Services Using This Library

- **Catalog.API** — Transactional price change events
- **Ordering.API** — Transactional order lifecycle events
- **Webhooks.API** — Referenced but minimal usage

### File Structure

```
src/IntegrationEventLogEF/
├── IntegrationEventLogEF.csproj
├── IntegrationEventLogEntry.cs      # Event log entity
├── EventStateEnum.cs                # State tracking
├── IntegrationLogExtensions.cs      # EF Fluent API
├── GlobalUsings.cs
├── Services/
│   ├── IIntegrationEventLogService.cs
│   └── IntegrationEventLogService.cs
└── Utilities/
    └── ResilientTransaction.cs
```

---

## Shared

**Project**: `src/Shared/`  
**Type**: Utility extensions (linked files, not a project reference)  
**Purpose**: Reduces duplication across services

### Files

#### ActivityExtensions.cs

```csharp
public static class ActivityExtensions
{
    public static void SetExceptionTags(this Activity activity, Exception ex)
    // Tags: exception.message, exception.stacktrace, exception.type
    // Sets Activity status to Error
}
```

Used by: `EventBusRabbitMQ`, migration services

#### MigrateDbContextExtensions.cs

```csharp
public static IServiceCollection AddMigration<TContext>(this IServiceCollection services)
public static IServiceCollection AddMigration<TContext, TDbSeeder>(this IServiceCollection services)
```

Registers a background `IHostedService` that runs EF Core migrations on startup. Optionally accepts a seeder implementing `IDbSeeder<TContext>`.

Used by: All services with databases (Catalog, Identity, Ordering, Webhooks, PaymentProcessor)

### Usage Pattern

These files are included via `<Compile Include>` in `.csproj` files (linked files, not project references):

```xml
<Compile Include="..\Shared\ActivityExtensions.cs" Link="Extensions\ActivityExtensions.cs" />
<Compile Include="..\Shared\MigrateDbContextExtensions.cs" Link="Extensions\MigrateDbContextExtensions.cs" />
```
