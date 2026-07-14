# Identity.API — Service Context

## Overview

**Project**: `src/Identity.API/`  
**Type**: ASP.NET Core MVC + Duende IdentityServer  
**Protocol**: HTTP (OAuth2/OpenID Connect)  
**Database**: PostgreSQL (`identitydb`)  
**Framework**: .NET 10.0  

The Identity.API is the central authentication and authorization service using Duende IdentityServer 7+ (OAuth2/OIDC). It manages user accounts, JWT token generation, credential validation, and consent flows for all other services.

## Architecture

- **Pattern**: MVC with Razor Views (Duende IdentityServer quickstart UI)
- **Auth Framework**: Duende IdentityServer for OAuth2/OIDC
- **User Management**: ASP.NET Core Identity with EF Core
- **Configuration**: In-memory client, API resource, scope, and identity resource definitions

## How It's Called

| Caller | Protocol | Purpose |
|---|---|---|
| **WebApp** | OpenID Connect (browser redirect) | User login/logout |
| **WebhookClient** | OpenID Connect (browser redirect) | User login/logout |
| **All services** | HTTP (`.well-known/openid-configuration`) | Discovery document |
| **All services** | HTTP (`/connect/token`) | Token validation/exchange |

## What This Service Calls

Identity.API does not call other services. It is a standalone auth provider.

## API Endpoints

### IdentityServer Core Endpoints (automatic)

| Route | Purpose |
|---|---|
| `/.well-known/openid-configuration` | OpenID Connect discovery document |
| `/connect/authorize` | Authorization endpoint |
| `/connect/token` | Token endpoint |
| `/connect/userinfo` | User info endpoint |
| `/connect/revocation` | Token revocation |
| `/connect/endsession` | Logout endpoint |

### MVC Controllers

| Controller | Routes | Purpose |
|---|---|---|
| `AccountController` | `GET/POST /account/login`, `/account/logout`, `/account/access-denied` | User authentication |
| `ExternalController` | `POST /external/challenge`, `/external/callback` | External auth providers |
| `ConsentController` | `GET/POST /consent` | OAuth consent screen |
| `DeviceController` | `GET/POST /device/index`, `/device/success` | Device authorization flow |
| `GrantsController` | `GET/POST /grants/index`, `/grants/revoke` | User grant management |
| `DiagnosticsController` | `GET /diagnostics` | Debug info (dev only) |
| `HomeController` | `GET /` | Home page |

## Database Schema

**DbContext**: `ApplicationDbContext` (`src/Identity.API/Data/ApplicationDbContext.cs`)  
**Extends**: `IdentityDbContext<ApplicationUser>`  
**Database**: PostgreSQL (`identitydb`)

### Tables

Standard ASP.NET Identity tables plus Duende IdentityServer operational tables:

| Table Group | Purpose |
|---|---|
| `AspNetUsers` | User accounts (`ApplicationUser`) |
| `AspNetRoles` | Role definitions |
| `AspNetUserRoles` | User-role mappings |
| `AspNetUserClaims` | User claims |
| `AspNetUserLogins` | External login providers |
| `AspNetUserTokens` | User tokens |
| `PersistedGrants` | IdentityServer grants (tokens, codes) |
| `DeviceFlowCodes` | Device authorization codes |
| `Keys` | Signing keys |

### ApplicationUser Entity

Extended `IdentityUser` with marketplace-specific properties:

| Field | Type | Notes |
|---|---|---|
| Standard Identity fields | Various | Email, PasswordHash, etc. |
| `CardNumber` | `string` | Default card for checkout |
| `SecurityNumber` | `string` | Card CVV |
| `Expiration` | `string` | Card expiration |
| `CardHolderName` | `string` | Name on card |
| `CardType` | `int` | Card type ID |
| `Street` | `string` | Address |
| `City` | `string` | Address |
| `State` | `string` | Address |
| `Country` | `string` | Address |
| `ZipCode` | `string` | Address |
| `Name` | `string` | Display name |
| `LastName` | `string` | Last name |

### Data Modification

- User management through ASP.NET Identity `UserManager<ApplicationUser>`
- Seed data via `UsersSeed` class on startup

## Integration Events

### Published Events

**None.** Identity.API does not participate in the event bus.

### Consumed Events

**None.** Identity.API does not subscribe to any events.

## Configuration

### OAuth Clients (`src/Identity.API/Configuration/Config.cs`)

Defines all authorized OAuth2 clients, their allowed scopes, redirect URIs, and grant types. Clients include:
- `webapp` — WebApp frontend
- `webhooksclient` — WebhookClient app
- `basketswaggerui` — Basket API Swagger
- `orderingswaggerui` — Ordering API Swagger
- `webhooksswaggerui` — Webhooks API Swagger

### Scopes & Resources

- **API Scopes**: `orders`, `basket`, `webhooks`
- **Identity Resources**: `openid`, `profile`

## Dependencies

### Service Dependencies

| Service | How | Purpose |
|---|---|---|
| **PostgreSQL** | Aspire.Npgsql.EFCore | User and grant storage |

### NuGet Packages

- `Duende.IdentityServer.AspNetIdentity` — IdentityServer + ASP.NET Identity integration
- `Duende.IdentityServer.EntityFramework` — EF Core operational store
- `Duende.IdentityServer.Storage` — Storage abstractions
- `Duende.IdentityServer` — Core IdentityServer
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` — Identity EF Core
- `Microsoft.AspNetCore.Identity.UI` — Identity UI
- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` — PostgreSQL + Aspire
- `Microsoft.EntityFrameworkCore.Tools` — Migrations

### Project References

- `eShop.ServiceDefaults` — Telemetry, health checks

## Core Services & Classes

### ProfileService (`src/Identity.API/Services/ProfileService.cs`)

Implements `IProfileService`. Extracts user claims (name, email, etc.) and includes them in access/identity tokens.

### EFLoginService (`src/Identity.API/Services/EFLoginService.cs`)

Implements `ILoginService<ApplicationUser>`. Validates user credentials by:
1. Finding user by email
2. Calling `SignInManager.CheckPasswordSignInAsync()`

### RedirectService (`src/Identity.API/Services/IRedirectService.cs`)

Handles post-login redirect URL validation.

### UsersSeed (`src/Identity.API/UsersSeed.cs`)

Seeds demo user accounts on application startup. Implements `IDbSeeder<ApplicationDbContext>`.

### Config (`src/Identity.API/Configuration/Config.cs`)

Static configuration for IdentityServer clients, API resources, scopes, and identity resources.

## File Structure

```
src/Identity.API/
├── Identity.API.csproj
├── Program.cs                              # Entry point (IdentityServer, Identity, DbContext)
├── GlobalUsings.cs
├── appsettings.json
├── tempkey.jwk                             # Dev-only signing key (NOT for production)
├── Configuration/
│   └── Config.cs                           # OAuth clients, resources, scopes
├── Data/
│   ├── ApplicationDbContext.cs             # EF DbContext (extends IdentityDbContext)
│   └── Migrations/                         # EF migrations
├── Models/
│   ├── ApplicationUser.cs                  # Extended Identity user
│   └── Various ViewModels                  # Login, consent, grant view models
├── Services/
│   ├── ProfileService.cs                   # IProfileService implementation
│   ├── EFLoginService.cs                   # ILoginService implementation
│   └── IRedirectService.cs                 # Redirect validation
├── UsersSeed.cs                            # Demo user seeding
├── Quickstart/                             # Duende IdentityServer quickstart UI
│   ├── Account/                            # Login/logout controllers
│   ├── Consent/                            # OAuth consent
│   ├── Device/                             # Device flow
│   ├── Grants/                             # Grant management
│   ├── Home/                               # Home controller
│   └── Diagnostics/                        # Debug endpoint
└── Views/                                  # Razor views for all controllers
```

## Security Notes

- `tempkey.jwk` is a development-only signing key. Production deployments must use a secure key store.
- Client secrets are configured in `Config.cs` — review for production hardening.
- Password hashing uses ASP.NET Identity defaults (PBKDF2).
