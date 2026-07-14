# eShop.AppHost — Service Context

## Overview

**Project**: `src/eShop.AppHost/`  
**Type**: .NET Aspire Distributed Application Orchestrator  
**Protocol**: N/A (orchestration layer)  
**Database**: N/A (provisions databases for other services)  
**Framework**: .NET 10.0  

The AppHost is the Aspire orchestrator that defines, configures, and starts all microservices, databases, caches, and message brokers as a cohesive system for local development. It is the single entry point for running the entire eShop application.

## Architecture

- **Pattern**: Aspire `DistributedApplication` builder
- **Purpose**: Service composition, infrastructure provisioning, dependency wiring
- **Execution**: Runs as a console app that bootstraps all services with proper references
- **Dashboard**: Provides the Aspire dashboard for monitoring all services

## Infrastructure Provisioned

### Databases (PostgreSQL with pgvector)

| Database | Used By |
|---|---|
| `catalogdb` | Catalog.API |
| `identitydb` | Identity.API |
| `orderdb` | Ordering.API, OrderProcessor |
| `webhooksdb` | Webhooks.API |
| `tokendb` | PaymentProcessor |

### Cache

| Store | Used By |
|---|---|
| **Redis** (persistent) | Basket.API |

### Message Broker

| Broker | Used By |
|---|---|
| **RabbitMQ** (persistent) | All event-driven services |

### Optional AI

| Provider | Purpose |
|---|---|
| **Azure OpenAI** | Catalog embeddings, chatbot |
| **Ollama** | Local AI alternative |

## Service Wiring

The AppHost wires services together via `WithReference()` and `GetEndpoint()` calls:

```
Identity.API ←──── All services (auth)
PostgreSQL  ←──── Catalog, Identity, Ordering, Webhooks, Payment
Redis       ←──── Basket
RabbitMQ    ←──── Catalog, Basket, Ordering, OrderProcessor, Payment, Webhooks, WebApp
```

### Service Dependencies Graph

```
eShop.AppHost
├── Infrastructure
│   ├── PostgreSQL (pgvector)
│   │   ├── catalogdb
│   │   ├── identitydb
│   │   ├── orderdb
│   │   ├── webhooksdb
│   │   └── tokendb
│   ├── Redis
│   └── RabbitMQ
│
├── Services
│   ├── Identity.API        → PostgreSQL (identitydb)
│   ├── Basket.API          → Redis, RabbitMQ, Identity.API
│   ├── Catalog.API         → PostgreSQL (catalogdb), RabbitMQ, Identity.API, [OpenAI/Ollama]
│   ├── Ordering.API        → PostgreSQL (orderdb), RabbitMQ, Identity.API
│   ├── OrderProcessor      → PostgreSQL (orderdb), RabbitMQ
│   ├── PaymentProcessor    → PostgreSQL (tokendb), RabbitMQ, Identity.API
│   ├── Webhooks.API        → PostgreSQL (webhooksdb), RabbitMQ, Identity.API
│   └── WebhookClient       → Webhooks.API, Identity.API
│
├── Frontends
│   ├── WebApp              → Basket.API, Catalog.API, Ordering.API, Identity.API, RabbitMQ, [OpenAI/Ollama]
│   └── mobile-bff (YARP)   → Catalog.API, Ordering.API, Basket.API, Identity.API
│
└── Optional
    ├── OpenAI              → Azure Cognitive Services
    └── Ollama              → Local AI runtime
```

## Configuration

### Environment Variables

| Variable | Purpose |
|---|---|
| `ESHOP_USE_HTTP_ENDPOINTS` | When set, uses HTTP instead of HTTPS (for CI/Playwright tests) |

### HTTP/HTTPS Mode

The AppHost toggles between HTTP and HTTPS endpoints based on `ESHOP_USE_HTTP_ENDPOINTS` environment variable. When set:
- External endpoints use HTTP
- Callback URLs are configured with HTTP scheme
- Used primarily for CI/test environments

### Identity.API Callback Configuration

Identity.API is configured with callback URLs for all client applications. These are dynamically constructed from service endpoint references.

## Dependencies

### Project References

All service projects are referenced by AppHost:
- `Basket.API`, `Catalog.API`, `Identity.API`, `Ordering.API`
- `OrderProcessor`, `PaymentProcessor`
- `Webhooks.API`, `WebhookClient`
- `WebApp`

### NuGet Packages

- `Aspire.Hosting.RabbitMQ` — RabbitMQ hosting
- `Aspire.Hosting.Redis` — Redis hosting
- `Aspire.Hosting.PostgreSQL` — PostgreSQL hosting
- `Aspire.Hosting.Azure.CognitiveServices` — Azure OpenAI hosting
- `Aspire.Hosting.Yarp` — YARP reverse proxy hosting
- `CommunityToolkit.Aspire.Hosting.Ollama` — Ollama hosting

## Core Classes

### Program.cs (`src/eShop.AppHost/Program.cs`)

Main entry point. Builds the `DistributedApplication` with all resources and services.

### Extensions.cs (`src/eShop.AppHost/Extensions.cs`)

Helper methods:
- `AddForwardedHeaders()` — Configures forwarded headers for reverse proxy
- `AddOpenAI()` — Configures OpenAI (Azure or connection string)
- `AddOllama()` — Configures Ollama with embedding model
- `OpenAITarget` enum — Azure vs. connection string vs. Ollama

## File Structure

```
src/eShop.AppHost/
├── eShop.AppHost.csproj         # Project file (references all services)
├── Program.cs                   # Orchestration logic
├── Extensions.cs                # OpenAI/Ollama/header helpers
├── appsettings.json             # Configuration
└── Properties/
    └── launchSettings.json
```

## Running the Application

The AppHost is started via:
```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

Or via the VS Code task: `run: AppHost (All Services)`

This starts all services, databases, caches, and the Aspire dashboard.
