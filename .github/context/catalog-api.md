# Catalog.API — Service Context

## Overview

**Project**: `src/Catalog.API/`  
**Type**: ASP.NET Core Minimal API  
**Protocol**: HTTP REST  
**Database**: PostgreSQL (`catalogdb`) with pgvector extension  
**Framework**: .NET 10.0  

The Catalog.API is the product catalog microservice for the eShop marketplace. It manages club item listings, product browsing and filtering, seller listings, ratings, favorites, tags, stock management, and AI-powered semantic search with vector embeddings.

## Architecture

- **Pattern**: Minimal APIs — no MVC controllers
- **Data Access**: Direct Entity Framework Core (`CatalogContext`)
- **Event-Driven**: Publishes and consumes integration events via RabbitMQ
- **AI Integration**: Optional Azure OpenAI or Ollama for embedding generation (semantic search)
- **Authentication**: Custom `CatalogAuthenticationHandler` supporting Bearer tokens and `X-Test-User-Id` header for testing

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **WebApp** | HTTP REST | Product browsing, search, ratings, favorites, seller listings |
| **OrderProcessor** (indirect) | RabbitMQ events | Stock validation and inventory deduction |
| **Browser** (via WebApp proxy) | HTTP (proxied) | Product images (`/api/catalog/items/{id}/pic`) |

## What Calls This Service

- **WebApp** → HTTP REST for all catalog operations
- **Ordering.API** → Publishes events consumed by Catalog.API (stock validation, payment)

## API Endpoints

### Item Querying

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `GET` | `/api/catalog/items` | No | `?pageSize&pageIndex&name?&type?&brand?&tag?` | `PaginatedItems<CatalogItem>` |
| `GET` | `/api/catalog/items/{id}` | No | Path: `int id` | `CatalogItem` or `404` |
| `GET` | `/api/catalog/items/by?ids={ids}` | No | Query: `int[] ids` | `List<CatalogItem>` |
| `GET` | `/api/catalog/items/by/{name}` | No | Path: `string name` | `PaginatedItems<CatalogItem>` |
| `GET` | `/api/catalog/items/{id}/pic` | No | Path: `int id` | Binary image file |
| `GET` | `/api/catalog/items/withsemanticrelevance?text={text}` | No | Query: `string text` (min 1 char) | `PaginatedItems<CatalogItem>` |
| `GET` | `/api/catalog/items/type/{typeId}/brand/{brandId?}` | No | Path: `int typeId, int? brandId` | `PaginatedItems<CatalogItem>` |
| `GET` | `/api/catalog/items/type/all/brand/{brandId}` | No | Path: `int? brandId` | `PaginatedItems<CatalogItem>` |

### Ratings & Favorites

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `POST` | `/api/catalog/items/{id}/rate` | **Required** | `{ Stars: 1-5 }` | `204` or `404` |
| `POST` | `/api/catalog/items/{id}/favorite` | **Required** | Empty body | `204` or `404` |
| `PATCH` | `/api/catalog/items/{id}/tags` | **Required** | `{ Tags: string[]? }` | `204` or `403` (if not owner) |

### Catalog Metadata

| Method | Route | Auth | Response |
|---|---|---|---|
| `GET` | `/api/catalog/catalogtypes` | No | `List<CatalogType>` |
| `GET` | `/api/catalog/catalogbrands` | No | `List<CatalogBrand>` |

### Item CRUD (Admin/Owner)

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `PUT` | `/api/catalog/items/{id}` | **Required** | `CatalogItem` (full object) | `201` |
| `POST` | `/api/catalog/items` | **Required** | `CatalogItem` | `201` |
| `DELETE` | `/api/catalog/items/{id}` | **Required** | None | `204` or `404` |

### Seller Club Listings

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `POST` | `/api/catalog/items/listings` | **Required** | `CreateSellerListingRequest` | `201` |
| `GET` | `/api/catalog/items/by-seller/{sellerId}` | No | Path: `string sellerId` | `PaginatedItems<CatalogItem>` |
| `GET` | `/api/catalog/items/my-listings` | **Required** | None | `PaginatedItems<CatalogItem>` |
| `DELETE` | `/api/catalog/items/listings/{id}` | **Required** | None | `204` / `403` / `404` |

**CreateSellerListingRequest**:
```json
{
  "Name": "string",
  "Price": 0.00,
  "TypeId": 0,
  "BrandId": 0,
  "Condition": "string",
  "PhotoUrls": ["string"],
  "Description": "string?",
  "ManufactureYear": 0,
  "Tags": ["string"]
}
```

### OpenAPI Documentation

- Endpoint definitions: `src/Catalog.API/Apis/CatalogApi.cs`
- Generated OpenAPI specs: `src/Catalog.API/Catalog.API.json`, `Catalog.API_v2.json`

## Database Schema

**DbContext**: `CatalogContext` (`src/Catalog.API/Infrastructure/CatalogContext.cs`)  
**Database**: PostgreSQL (`catalogdb`)

### Tables

| Table | Entity | Purpose |
|---|---|---|
| `Catalog` | `CatalogItem` | Main product items |
| `CatalogItemRating` | `CatalogItemRating` | User ratings (1-5 stars) |
| `CatalogItemFavorite` | `CatalogItemFavorite` | User favorites |
| `CatalogType` | `CatalogType` | Product categories |
| `CatalogBrand` | `CatalogBrand` | Product brands |
| `IntegrationEventLog` | `IntegrationEventLogEntry` | Outbox pattern event tracking |

### CatalogItem Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string(50)` | Required |
| `Price` | `decimal` | |
| `Description` | `string` | |
| `CatalogTypeId` | `int` | FK → CatalogType |
| `CatalogBrandId` | `int` | FK → CatalogBrand |
| `AvailableStock` | `int` | |
| `RestockThreshold` | `int` | |
| `MaxStockThreshold` | `int` | |
| `ViewCount` | `int` | |
| `FavoriteCount` | `int` | |
| `AverageRating` | `decimal` | |
| `RatingCount` | `int` | |
| `Tags` | `string(500)` | Comma-separated |
| `Embedding` | `Vector(384)` | pgvector for semantic search |
| `OnReorder` | `bool` | |
| `SellerId` | `string(100)` | Owner user ID |
| `Condition` | `string` | Item condition |
| `ManufactureYear` | `int?` | |
| `PhotoUrls` | `string[]` | |

### CatalogItemRating Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `CatalogItemId` | `int` | FK → CatalogItem |
| `UserId` | `string(100)` | Required |
| `Stars` | `int` | 1-5 |
| `CreatedAt` | `timestamp with TZ` | |

Unique index on `(CatalogItemId, UserId)` — one rating per user per item.

### CatalogItemFavorite Entity

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `CatalogItemId` | `int` | FK → CatalogItem |
| `UserId` | `string(100)` | Required |
| `CreatedAt` | `timestamp with TZ` | |

Unique index on `(CatalogItemId, UserId)` — one favorite per user per item.

### Data Modification

Direct EF Core `DbContext.SaveChangesAsync()`. Uses transactional outbox for events via `SaveEventAndCatalogContextChangesAsync()`.

### Entity Configurations

- `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogItemEntityTypeConfiguration.cs`
- `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogItemRatingEntityTypeConfiguration.cs`
- `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogItemFavoriteEntityTypeConfiguration.cs`
- `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogTypeEntityTypeConfiguration.cs`
- `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogBrandEntityTypeConfiguration.cs`

### Migrations

- `src/Catalog.API/Infrastructure/Migrations/`

### Seed Data

- `src/Catalog.API/Infrastructure/CatalogContextSeed.cs`
- `src/Catalog.API/Setup/catalog.json`

## Integration Events

### Published Events

| Event | Trigger | Payload |
|---|---|---|
| `ProductPriceChangedIntegrationEvent` | PUT `/api/catalog/items/{id}` (price change) | `{ ProductId: int, NewPrice: decimal, OldPrice: decimal }` |
| `OrderStockConfirmedIntegrationEvent` | Stock validation passes | `{ OrderId: int }` |
| `OrderStockRejectedIntegrationEvent` | Stock validation fails | `{ OrderId: int, OrderStockItems: [] }` |

### Consumed Events

| Event | Source | Handler | Action |
|---|---|---|---|
| `OrderStatusChangedToAwaitingValidationIntegrationEvent` | Ordering.API | `OrderStatusChangedToAwaitingValidationIntegrationEventHandler` | Validates stock availability; publishes Confirmed or Rejected |
| `OrderStatusChangedToPaidIntegrationEvent` | Ordering.API | `OrderStatusChangedToPaidIntegrationEventHandler` | Decrements `AvailableStock` via `CatalogItem.RemoveStock()` |

### Event Payload: OrderStockItem

```csharp
public record OrderStockItem(int ProductId, int Units);
```

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **RabbitMQ** | EventBusRabbitMQ | Event publishing/consuming |
| **PostgreSQL** | Aspire.Npgsql.EFCore | Data storage |
| **Identity.API** | JWT Bearer validation | Authentication for write endpoints |
| **Azure OpenAI / Ollama** | Optional | Embedding generation for semantic search |

### Project References

- `EventBusRabbitMQ` — RabbitMQ event bus
- `IntegrationEventLogEF` — Outbox pattern
- `eShop.ServiceDefaults` — Auth, telemetry, health checks

### NuGet Packages

- `Asp.Versioning.Http` — API versioning
- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` — PostgreSQL + EF Core
- `Pgvector` + `Pgvector.EntityFrameworkCore` — Vector embeddings
- `Aspire.Azure.AI.OpenAI` — Azure OpenAI
- `CommunityToolkit.Aspire.OllamaSharp` — Ollama integration
- `Microsoft.EntityFrameworkCore.Tools` — Migrations CLI

## Core Services & Classes

### CatalogAI (`src/Catalog.API/Services/CatalogAI.cs`)

AI embedding generation for semantic search.

| Method | Purpose |
|---|---|
| `IsEnabled` | Returns true if embedding generator is configured |
| `GetEmbeddingAsync(string text)` | Generate 384-dim embedding for search text |
| `GetEmbeddingAsync(CatalogItem item)` | Generate embedding for item (name + description) |
| `GetEmbeddingsAsync(IEnumerable<CatalogItem>)` | Batch embedding generation |

Configuration: `OllamaEnabled=true` → Ollama; else `textEmbeddingModel` connection → Azure OpenAI; else disabled.

### CatalogIntegrationEventService (`src/Catalog.API/IntegrationEvents/CatalogIntegrationEventService.cs`)

| Method | Purpose |
|---|---|
| `SaveEventAndCatalogContextChangesAsync(IntegrationEvent)` | Save event + DbContext in single transaction (outbox) |
| `PublishThroughEventBusAsync(IntegrationEvent)` | Publish event to RabbitMQ after marking in-progress |

### CatalogItem (`src/Catalog.API/Model/CatalogItem.cs`)

| Method | Purpose |
|---|---|
| `RemoveStock(int qty)` | Decrement stock; throws if insufficient |
| `AddStock(int qty)` | Increment stock (capped at MaxStockThreshold) |
| `NormalizeTag(string tag)` | Lowercase & trim |
| `NormalizeTags(IEnumerable<string>)` | Normalize to comma-separated |
| `GetTags()` | Parse stored tags to collection |

### CatalogServices (`src/Catalog.API/Model/CatalogServices.cs`)

DI container record for injection into endpoint handlers:
```csharp
public class CatalogServices(
    CatalogContext context,
    ICatalogAI catalogAI,
    IOptions<CatalogOptions> options,
    ILogger<CatalogServices> logger,
    ICatalogIntegrationEventService eventService)
```

## File Structure

```
src/Catalog.API/
├── Apis/
│   └── CatalogApi.cs                          # All endpoint definitions
├── Services/
│   ├── CatalogAI.cs                           # AI embedding generation
│   └── ICatalogAI.cs                          # Interface
├── Model/
│   ├── CatalogItem.cs                         # Main entity + stock methods
│   ├── CatalogItemRating.cs                   # Rating entity
│   ├── CatalogItemFavorite.cs                 # Favorite entity
│   ├── CatalogType.cs                         # Category entity
│   ├── CatalogBrand.cs                        # Brand entity
│   ├── CatalogServices.cs                     # DI container
│   └── PaginatedItems.cs                      # Pagination DTO
├── Infrastructure/
│   ├── CatalogContext.cs                      # EF Core DbContext
│   ├── CatalogContextSeed.cs                  # Seed data loader
│   ├── CatalogAuthenticationHandler.cs        # Custom auth (test + Bearer)
│   ├── Exceptions/
│   │   └── CatalogDomainException.cs
│   ├── EntityConfigurations/                  # EF Fluent API configs
│   └── Migrations/                            # EF migrations
├── IntegrationEvents/
│   ├── CatalogIntegrationEventService.cs      # Event publishing (outbox)
│   ├── ICatalogIntegrationEventService.cs
│   ├── Events/                                # Event record definitions
│   └── EventHandling/                         # Event handlers
├── Extensions/
│   ├── Extensions.cs                          # DI setup
│   └── HostEnvironmentExtensions.cs
├── Setup/
│   └── catalog.json                           # Seed data
├── Pics/                                      # Product images
├── Program.cs                                 # Entry point
├── CatalogOptions.cs                          # Config options
├── Catalog.API.csproj
└── appsettings.json
```

## Related Test Projects

- `tests/Catalog.FunctionalTests/` — Functional/integration tests
