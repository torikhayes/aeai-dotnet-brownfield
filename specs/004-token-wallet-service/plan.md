# Implementation Plan: PaymentProcessor Token Ledger Extension

**Branch**: `004-paymentprocessor-token-ledger` | **Date**: 2026-07-13 | **Spec**: [spec.md](spec.md)

## Summary

Extend the existing `PaymentProcessor` ASP.NET Core service to act as the token wallet and ledger for the golf marketplace. PaymentProcessor already runs a full `WebApplication` pipeline (`SDK.Web`, `AddServiceDefaults()`) — no restructuring required. New work adds: a `tokendb` Postgres database, four EF Core entities, minimal API token endpoints, a `ClubListingVerifiedIntegrationEvent` subscriber, and a `TokenLedgerService` implementing award/spend/balance logic with optimistic concurrency. All token ledger code paths are TDD (Principle V).

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 10, Npgsql.EntityFrameworkCore.PostgreSQL, EventBusRabbitMQ (existing), eShop.ServiceDefaults (existing)  
**Storage**: PostgreSQL — new `tokendb` database on the existing `postgres` Aspire container  
**Testing**: xUnit + Microsoft.Testing.Platform (existing project pattern); TDD mandatory on ledger paths  
**Target Platform**: .NET Aspire microservices (Linux container, no external HTTP exposure)  
**Performance Goals**: `GET /api/tokens/balance` < 200ms p95  
**Constraints**: Optimistic concurrency (`RowVersion`); idempotent award (EventId + CatalogItemId); at-least-once event delivery via RabbitMQ; no external port exposure  
**Scale/Scope**: Per-user wallets, append-only transaction log, 28-row lookup table (7 categories × 4 conditions)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Evidence |
|---|---|---|
| I. Microservices Continuity | ✅ PASS | Extends `PaymentProcessor`; no new service created |
| II. Token Ledger Integrity & Non-Convertibility | ✅ PASS | Idempotency (FR-008), `LookupTableVersion` audit trail (FR-006), non-convertibility in assumptions |
| III. Attribute-Based, Anti-Fraud Valuation | ✅ PASS | Category × condition lookup table (FR-007); award on `ClubListingVerifiedIntegrationEvent` (not instant submit, not human-gated) |
| IV. Trust & Safety in Physical Trades | N/A | No dispute/freeze logic in this spec |
| V. Risk-Based Testing Discipline | ✅ PASS | FR-013 mandates TDD on all ledger/award/mutation paths |
| VI. Marketplace Scope Boundary | ✅ PASS | Spec ends at token transaction; no fulfillment tracking |
| Tech Constraints | ✅ PASS | .NET Aspire, EventBus, durable Postgres storage (not cache-only) |

**Gate result: PASS — proceed to Phase 0.**

## Project Structure

### Documentation (this feature)

```text
specs/004-token-wallet-service/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── tokens-api.md    ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit-tasks — not created here)
```

### Source Code Layout

```text
src/PaymentProcessor/
├── Program.cs                          ← UPDATE: tokendb ref, identity env, token endpoints wiring
├── PaymentProcessor.csproj             ← UPDATE: add EF Core + Npgsql packages
├── appsettings.json                    ← UPDATE: add TokenOptions (retry count)
├── GlobalUsings.cs                     ← UPDATE: add TokenLedger namespaces
├── TokenLedger/
│   ├── Apis/
│   │   └── TokensApi.cs               ← NEW: GET /balance, GET /transactions, GET /reward-preview, POST /spend
│   ├── Infrastructure/
│   │   ├── TokenDbContext.cs          ← NEW: EF Core DbContext
│   │   ├── TokenDbSeeder.cs           ← NEW: seeds lookup table v1.0.0 on startup
│   │   └── Migrations/               ← NEW: EF Core migrations
│   ├── Model/
│   │   ├── TokenWallet.cs             ← NEW
│   │   ├── TokenTransaction.cs        ← NEW
│   │   ├── TokenAwardLookupEntry.cs   ← NEW
│   │   └── TokenAwardedListing.cs     ← NEW
│   └── Services/
│       ├── TokenLedgerService.cs      ← NEW: award, spend, balance, preview logic
│       └── TokenLedgerExtensions.cs   ← NEW: AddTokenLedger() DI registration
├── IntegrationEvents/
│   ├── ClubListingVerifiedIntegrationEvent.cs           ← NEW
│   └── EventHandling/
│       ├── OrderStatusChangedToStockConfirmedIntegrationEventHandler.cs  ← EXISTING (no change)
│       └── ClubListingVerifiedIntegrationEventHandler.cs                 ← NEW

src/eShop.AppHost/
└── Program.cs            ← UPDATE: add tokendb, add identity + tokendb refs to payment-processor

tests/PaymentProcessor.UnitTests/      ← NEW project
├── PaymentProcessor.UnitTests.csproj
└── TokenLedger/
    ├── TokenLedgerServiceTests.cs     ← TDD: balance read, award, spend, idempotency, concurrent debit
    └── TokenAwardLookupTests.cs       ← TDD: lookup table version selection, seeded values
```

**Structure Decision**: Nested `TokenLedger/` subfolder within `PaymentProcessor` keeps the token domain organised without creating a new project, consistent with how `Ordering.API` organises its `Application/`, `Domain/`, and `Infrastructure/` layers within one service.

## Complexity Tracking

No constitution violations — table empty.
