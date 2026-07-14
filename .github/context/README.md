# eShop Project Context Files

This directory contains standalone context files describing each sub-project within the eShop monorepo. Use these files to understand the architecture, purpose, dependencies, and code structure of each service.

## How to Use

- **Start here** → Read [eshop-overview.md](eshop-overview.md) for a high-level map of all services
- **Dive deeper** → Each service has its own context file with detailed architecture, endpoints, events, and database schemas

## Summary

| Context File | Project | Type |
|---|---|---|
| [eshop-overview.md](eshop-overview.md) | Full system | Summary & service map |
| [catalog-api.md](catalog-api.md) | `src/Catalog.API` | REST API microservice |
| [basket-api.md](basket-api.md) | `src/Basket.API` | gRPC microservice |
| [ordering-api.md](ordering-api.md) | `src/Ordering.API` + Domain + Infrastructure | REST API microservice (DDD/CQRS) |
| [order-processor.md](order-processor.md) | `src/OrderProcessor` | Background worker |
| [payment-processor.md](payment-processor.md) | `src/PaymentProcessor` | Web API + Event processor |
| [identity-api.md](identity-api.md) | `src/Identity.API` | OAuth2/OIDC auth server |
| [webapp.md](webapp.md) | `src/WebApp` + `src/WebAppComponents` | Blazor Server frontend |
| [webhooks-api.md](webhooks-api.md) | `src/Webhooks.API` | REST API microservice |
| [webhook-client.md](webhook-client.md) | `src/WebhookClient` | Blazor demo app |
| [apphost.md](apphost.md) | `src/eShop.AppHost` | Aspire orchestrator |
| [shared-libraries.md](shared-libraries.md) | EventBus, EventBusRabbitMQ, IntegrationEventLogEF, ServiceDefaults, Shared | Shared infrastructure |
