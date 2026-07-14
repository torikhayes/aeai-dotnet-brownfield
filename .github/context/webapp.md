# WebApp & WebAppComponents — Service Context

## Overview

**Projects**:
- `src/WebApp/` — Blazor Server SSR application (primary frontend)
- `src/WebAppComponents/` — Shared Razor component library

**Type**: Blazor Server (Server-Side Rendering)  
**Protocol**: Browser (HTTP) + HTTP/gRPC clients to backend services  
**Database**: None (stateless frontend)  
**Framework**: .NET 10.0  

The WebApp is the primary end-user frontend for the eShop marketplace. It displays products, manages shopping carts, handles checkout, shows order history, supports seller listings, and includes an AI-powered chatbot. WebAppComponents is a shared Razor class library providing reusable UI components.

## Architecture

- **Pattern**: Blazor Server with `AddInteractiveServerComponents()` (SSR)
- **Auth**: OpenID Connect via Identity.API
- **Backend Calls**: gRPC (Basket), HTTP REST (Catalog, Ordering)
- **Event Consumption**: RabbitMQ for order status notifications
- **State Management**: In-memory `BasketState` for client-side cart

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **Browser** | HTTP | End users navigate the app |
| **Identity.API** | OIDC callback | Post-login redirect |

## What This Service Calls

| Service | Protocol | Client | Purpose |
|---|---|---|---|
| **Basket.API** | gRPC | `BasketService` (gRPC client) | Basket CRUD |
| **Catalog.API** | HTTP REST | `CatalogService` (HttpClient) | Product browsing, search, ratings, listings |
| **Ordering.API** | HTTP REST | `OrderingService` (HttpClient) | Order creation, history |
| **Identity.API** | OpenID Connect | OIDC middleware | Authentication |
| **RabbitMQ** | AMQP | EventBusRabbitMQ | Order status event consumption |
| **OpenAI / Ollama** | HTTP | Aspire integration | AI chatbot (optional) |

## Pages & Routes

| Route | Page | Purpose |
|---|---|---|
| `/` | `Catalog.razor` | Home — browse product catalog |
| `/item/{id}` | `ItemPage.razor` | Product detail page |
| `/cart` | `CartPage.razor` | Shopping cart |
| `/checkout` | `Checkout.razor` | Order checkout form |
| `/user/login` | `LogIn.razor` | OpenID Connect redirect |
| `/user/logout` | `LogOut.razor` | Sign out |
| `/user/orders` | `Orders.razor` | Order history |
| `/user/my-listings` | `MyListings.razor` | Seller's club item listings |
| `/user/sell-my-club` | `SellMyClub.razor` | Create new listing form |
| `/product-images/{id}` | Proxy | Forwarded to Catalog.API (`/api/catalog/items/{id}/pic`) |
| `/Error` | `Error.razor` | Error page |

## Integration Events

### Consumed Events (via RabbitMQ)

| Event | Source | Purpose |
|---|---|---|
| `OrderStatusChangedToSubmittedIntegrationEvent` | Ordering.API | Update UI order status |
| `OrderStatusChangedToAwaitingValidationIntegrationEvent` | Ordering.API | Update UI order status |
| `OrderStatusChangedToStockConfirmedIntegrationEvent` | Ordering.API | Update UI order status |
| `OrderStatusChangedToPaidIntegrationEvent` | Ordering.API | Update UI order status |
| `OrderStatusChangedToShippedIntegrationEvent` | Ordering.API | Update UI order status |
| `OrderStatusChangedToCancelledIntegrationEvent` | Ordering.API | Update UI order status |

These events drive the `OrderStatusNotificationService` which updates the UI in real-time.

### Published Events

**None.** WebApp is a consumer-only service.

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **Basket.API** | gRPC client | Shopping cart |
| **Catalog.API** | HTTP REST client | Product catalog |
| **Ordering.API** | HTTP REST client | Orders |
| **Identity.API** | OpenID Connect | Authentication |
| **RabbitMQ** | EventBusRabbitMQ | Order status events |
| **OpenAI / Ollama** | Optional | AI chatbot |

### Project References (WebApp)

- `eShop.ServiceDefaults` — Auth, telemetry, health checks
- `EventBusRabbitMQ` — Event bus
- `WebAppComponents` — Shared Razor components

### NuGet Packages (WebApp)

- `Asp.Versioning.Http.Client` — Versioned API client
- `Aspire.Azure.AI.OpenAI` — Azure OpenAI
- `CommunityToolkit.Aspire.OllamaSharp` — Ollama
- `Microsoft.Extensions.ServiceDiscovery.Yarp` — Service discovery + reverse proxy
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` — OIDC auth
- `Grpc.Net.ClientFactory` — gRPC client factory
- `Grpc.Tools` — Proto compilation

### NuGet Packages (WebAppComponents)

- `Microsoft.AspNetCore.Components.Web` — Razor components

## Core Services & Classes

### WebApp Services

| Service | Location | Purpose |
|---|---|---|
| `BasketService` | `src/WebApp/Services/BasketService.cs` | gRPC client wrapping Basket.API calls |
| `BasketState` | `src/WebApp/Services/BasketState.cs` | In-memory cart state with change notifications |
| `OrderingService` | `src/WebApp/Services/OrderingService.cs` | HTTP client for Ordering.API (`GetOrders()`, `CreateOrder()`) |
| `LogOutService` | `src/WebApp/Services/LogOutService.cs` | OIDC logout flow handler |
| `ProductImageUrlProvider` | `src/WebApp/Services/ProductImageUrlProvider.cs` | Generates product image URLs |
| `OrderStatusNotificationService` | `src/WebApp/Services/OrderStatus/OrderStatusNotificationService.cs` | Listens to EventBus, updates UI on order status changes |

### WebAppComponents Services

| Service | Location | Purpose |
|---|---|---|
| `CatalogService` | `src/WebAppComponents/Services/CatalogService.cs` | HTTP client for Catalog.API (search, pagination, semantic search, listings) |
| `ICatalogService` | `src/WebAppComponents/Services/ICatalogService.cs` | Catalog service interface |
| `IProductImageUrlProvider` | `src/WebAppComponents/Services/IProductImageUrlProvider.cs` | Image URL provider interface |

### WebAppComponents UI Components

| Component | Location | Purpose |
|---|---|---|
| `CatalogListItem.razor` | `src/WebAppComponents/Catalog/` | Product card component |
| `CatalogSearch.razor` | `src/WebAppComponents/Catalog/` | Search bar component |
| `ListClubForm.razor` | `src/WebAppComponents/Catalog/` | Seller listing creation form |
| `MyListingsPage.razor` | `src/WebAppComponents/Catalog/` | Seller listings page component |

### WebAppComponents Models

| Model | Location | Purpose |
|---|---|---|
| `CatalogItem` | `src/WebAppComponents/Catalog/CatalogItem.cs` | Product model |
| `CreateSellerListingRequest` | `src/WebAppComponents/Catalog/CreateSellerListingRequest.cs` | DTO for creating seller listings |

### Chatbot Components

| Component | Location | Purpose |
|---|---|---|
| `Chatbot.razor` | `src/WebApp/Components/Chatbot/` | AI chatbot UI |
| `ChatState.cs` | `src/WebApp/Components/Chatbot/` | Chat conversation state |
| `MessageProcessor.cs` | `src/WebApp/Components/Chatbot/` | AI message processing integration |
| `ShowChatbotButton.razor` | `src/WebApp/Components/Chatbot/` | Toggle button |

## File Structure

### WebApp

```
src/WebApp/
├── WebApp.csproj
├── Program.cs                              # Entry point
├── GlobalUsings.cs
├── appsettings.json
├── Components/
│   ├── App.razor                           # Root component
│   ├── Routes.razor                        # Route definitions
│   ├── Pages/
│   │   ├── Catalog/Catalog.razor           # Home / product browse
│   │   ├── Item/ItemPage.razor             # Product detail
│   │   ├── Cart/CartPage.razor             # Shopping cart
│   │   ├── Checkout/Checkout.razor         # Checkout form
│   │   ├── User/Orders.razor              # Order history
│   │   ├── User/MyListings.razor          # Seller listings
│   │   ├── User/SellMyClub.razor          # Create listing
│   │   ├── User/LogIn.razor               # OIDC redirect
│   │   ├── User/LogOut.razor              # Sign out
│   │   └── Error.razor                    # Error page
│   ├── Chatbot/                            # AI chatbot components
│   └── Layout/                             # Layout components
├── Services/
│   ├── BasketService.cs                    # gRPC → Basket.API
│   ├── BasketState.cs                      # Cart state management
│   ├── OrderingService.cs                  # HTTP → Ordering.API
│   ├── LogOutService.cs                    # Logout handler
│   ├── ProductImageUrlProvider.cs          # Image URLs
│   └── OrderStatus/
│       └── OrderStatusNotificationService.cs  # EventBus consumer
├── Extensions/
│   └── Extensions.cs                       # DI, auth, EventBus, HTTP clients
└── Properties/
    └── launchSettings.json
```

### WebAppComponents

```
src/WebAppComponents/
├── WebAppComponents.csproj
├── _Imports.razor                          # Global imports
├── Catalog/
│   ├── CatalogItem.cs                     # Product model
│   ├── CatalogListItem.razor              # Product card component
│   ├── CatalogSearch.razor                # Search component
│   ├── CreateSellerListingRequest.cs      # DTO
│   ├── ListClubForm.razor                 # Seller listing form
│   └── MyListingsPage.razor               # Listings page component
├── Item/
│   └── ItemHelper.cs                      # Item utilities
└── Services/
    ├── CatalogService.cs                   # HTTP → Catalog.API
    ├── ICatalogService.cs                  # Interface
    └── IProductImageUrlProvider.cs         # Image URL interface
```

## Related Test Projects

- `tests/ClientApp.UnitTests/` — Unit tests for client app components
- `e2e/` — Playwright end-to-end tests for WebApp UI flows
