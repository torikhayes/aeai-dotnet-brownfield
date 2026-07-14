# WebhookClient — Service Context

## Overview

**Project**: `src/WebhookClient/`  
**Type**: Blazor Server SSR  
**Protocol**: Browser (HTTP) + HTTP REST client to Webhooks.API  
**Database**: In-memory only (data lost on restart)  
**Framework**: .NET 10.0  

The WebhookClient is a standalone demo/test application that receives and displays webhooks. Users register webhook subscriptions pointing to this service, then see incoming webhook events displayed in real-time.

## Architecture

- **Pattern**: Blazor Server SSR + Minimal API endpoints for webhook receiving
- **Auth**: OpenID Connect via Identity.API
- **Storage**: In-memory `HooksRepository` (observable pattern)
- **Purpose**: Demo/test tool — not a production service

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **Browser** | HTTP | Users navigate to register and view webhooks |
| **Webhooks.API** | HTTP POST | Delivers webhook payloads to `/webhook-received` |
| **Webhooks.API** | HTTP OPTIONS | Validates grant URL via `/check` |
| **Identity.API** | OIDC callback | Post-login redirect |

## What This Service Calls

| Service | Protocol | Purpose |
|---|---|---|
| **Webhooks.API** | HTTP REST | Register/manage webhook subscriptions |
| **Identity.API** | OpenID Connect | User authentication |

## API Endpoints

### Webhook Receiving (No Auth)

| Method | Route | Purpose |
|---|---|---|
| `OPTIONS` | `/check` | Webhook validation handshake — returns token if valid |
| `POST` | `/webhook-received` | Receives webhook payload, stores in memory |

### Pages (Auth Required)

| Route | Purpose |
|---|---|
| `/` | Home — displays received webhooks list |
| `/login` | OpenID Connect redirect |
| `/logout` | Sign out (OIDC end-session) |
| `/add-webhook` | Form to register a new webhook subscription |

### Authentication Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/logout` | Sign out via OIDC |

## Integration Events

### Published Events

**None.**

### Consumed Events

**None.** WebhookClient receives webhooks via HTTP POST, not RabbitMQ events.

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **Webhooks.API** | HTTP REST client | Subscription management |
| **Identity.API** | OpenID Connect | Authentication |

### Project References

- `eShop.ServiceDefaults` — Telemetry, health checks

### NuGet Packages

- `Asp.Versioning.Http.Client` — Versioned API client
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` — OIDC auth
- `Microsoft.AspNetCore.Components.QuickGrid` — Data grid component

## Core Services & Classes

### WebhooksClient (`src/WebhookClient/Services/WebhooksClient.cs`)

HTTP client for Webhooks.API.

| Method | Purpose |
|---|---|
| `AddWebHookAsync(subscriptionRequest)` | Register new webhook subscription |
| `LoadWebhooks()` | Load existing subscriptions |

### HooksRepository (`src/WebhookClient/Services/HooksRepository.cs`)

In-memory store for received webhooks with observable pattern (subscriber notification).

| Method | Purpose |
|---|---|
| `AddNew(hook)` | Store received webhook, notify subscribers |
| `GetAll()` | Retrieve all stored webhooks |

### WebhookEndpoints (`src/WebhookClient/Endpoints/WebhookEndpoints.cs`)

Minimal API endpoints for `/check` and `/webhook-received`.

### AuthenticationEndpoints (`src/WebhookClient/Endpoints/AuthenticationEndpoints.cs`)

Minimal API endpoint for `/logout`.

### Models

| Model | Location | Purpose |
|---|---|---|
| `WebHookReceived` | `Services/WebHookReceived.cs` | Received hook model |
| `WebhookData` | `Services/WebhookData.cs` | Webhook payload (When, Payload, Type) |
| `WebhookSubscriptionRequest` | `Services/WebhookSubscriptionRequest.cs` | DTO for creating subscriptions |
| `WebhookResponse` | `Services/WebhookResponse.cs` | Response DTO |
| `WebhookClientOptions` | `Services/WebhookClientOptions.cs` | Config (token, URL validation) |
| `WebhookType` | `Services/WebhookType.cs` | Webhook type enum |

## File Structure

```
src/WebhookClient/
├── WebhookClient.csproj
├── Program.cs                              # Entry point
├── appsettings.json
├── Components/
│   ├── App.razor                           # Root component
│   ├── Routes.razor                        # Route definitions
│   ├── Pages/
│   │   ├── Home/                           # Webhook list display
│   │   ├── LogIn.razor                     # OIDC redirect
│   │   ├── Error.razor                     # Error page
│   │   └── AddWebhook.razor               # Register new webhook
│   └── Layout/                             # Layout components
├── Services/
│   ├── WebhooksClient.cs                   # HTTP → Webhooks.API
│   ├── HooksRepository.cs                  # In-memory store + observable
│   ├── WebHookReceived.cs                  # Model
│   ├── WebhookData.cs                      # Payload model
│   ├── WebhookSubscriptionRequest.cs       # Creation DTO
│   ├── WebhookResponse.cs                  # Response DTO
│   ├── WebhookClientOptions.cs             # Config
│   └── WebhookType.cs                      # Enum
├── Endpoints/
│   ├── WebhookEndpoints.cs                 # /check, /webhook-received
│   └── AuthenticationEndpoints.cs          # /logout
└── Properties/
    └── launchSettings.json
```
