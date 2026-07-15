# Implementation Plan: Token-Based Checkout

**Branch**: `feature/001-create-token-adjusted-checkout` | **Date**: 2026-07-15 | **Spec**: [005-token-adjusted-checkout.spec.md](005-token-adjusted-checkout.spec.md)
**Input**: Feature specification from `/specs/005-token-adjusted-checkout/005-token-adjusted-checkout.spec.md`

## Summary

Add a single-currency checkout mode where each order is paid entirely in either cash or tokens. The implementation extends basket, ordering, and payment-processor flows with an explicit order-level payment method, token debit orchestration at order creation time, and compensating token refund on order-creation failure. The design preserves existing microservice boundaries and keeps token ledger logic centralized in `PaymentProcessor`.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (Aspire microservices)  
**Primary Dependencies**: ASP.NET Core Minimal APIs, gRPC (Basket), MediatR CQRS (Ordering), EF Core + Npgsql, EventBusRabbitMQ  
**Storage**: Redis (`Basket.API`), PostgreSQL (`orderdb`, `tokendb`)  
**Testing**: xUnit unit tests + existing functional/integration suites (`Ordering.FunctionalTests`, `PaymentProcessor.UnitTests`)  
**Target Platform**: Linux containers orchestrated by .NET Aspire  
**Project Type**: Distributed microservices web application  
**Performance Goals**: Token checkout latency comparable to cash checkout (SC-004), no additional eventual-consistency delay before order confirmation  
**Constraints**: No token-to-currency conversion, no mixed tender per order, idempotent spend/refund semantics, do not invoke card payment gateway for token-paid orders  
**Scale/Scope**: Checkout flow changes across Basket, WebApp, Ordering.API/Domain/Infrastructure, and PaymentProcessor TokenLedger

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Gate Review

| Principle | Status | Evidence |
|---|---|---|
| I. Microservices Continuity | PASS | Changes stay within Basket/API, Ordering/API+Domain+Infra, PaymentProcessor, and WebApp; no new architecture path introduced |
| II. Token Ledger Integrity & Non-Convertibility | PASS | Token checkout uses direct token spend/refund without any dollar-equivalent conversion |
| III. Attribute-Based, Anti-Fraud Valuation | PASS | This feature consumes existing `TokenPrice`; valuation rules remain upstream (spec 004 domain) |
| IV. Trust & Safety in Physical Trades | N/A | No dispute workflow change in this feature |
| V. Risk-Based Testing Discipline | PASS | Token ledger and order state transitions get explicit high-risk tests first |
| VI. Marketplace Scope Boundary | PASS | No shipping/fulfillment ownership changes |
| Technology Constraints | PASS | .NET 10 + Aspire + EventBus patterns remain intact |

**Gate result: PASS.**

### Post-Phase 1 Re-Check

Research, data model, contracts, and quickstart keep token mutations auditable/idempotent and preserve service boundaries. No constitution violations introduced in planning artifacts.

**Gate result: PASS.**

## Project Structure

### Documentation (this feature)

```text
specs/005-token-adjusted-checkout/
├── 005-token-adjusted-checkout.spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── token-checkout-api.md
└── tasks.md                      # created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Basket.API/
│   ├── Model/CustomerBasket.cs
│   ├── Proto/basket.proto
│   └── Grpc/BasketService.cs
├── WebApp/
│   ├── Components/Pages/Checkout/Checkout.razor
│   └── Services/BasketService.cs
├── WebAppComponents/
│   └── Catalog/CatalogItem.cs
├── Ordering.API/
│   ├── Apis/OrdersApi.cs
│   ├── Application/Commands/CreateOrderCommand.cs
│   ├── Application/Commands/CreateOrderCommandHandler.cs
│   ├── Application/Validations/CreateOrderCommandValidator.cs
│   └── Application/Models/
├── Ordering.Domain/
│   └── AggregatesModel/OrderAggregate/
│       ├── Order.cs
│       └── (new) OrderPaymentMethod.cs
├── Ordering.Infrastructure/
│   ├── OrderingContext.cs
│   ├── EntityConfigurations/OrderEntityTypeConfiguration.cs
│   └── Migrations/
└── PaymentProcessor/
    └── TokenLedger/
        ├── Apis/TokensApi.cs
        └── Services/TokenLedgerService.cs

tests/
├── Ordering.UnitTests/
├── Ordering.FunctionalTests/
└── PaymentProcessor.UnitTests/
```

**Structure Decision**: Use the existing service boundaries and ordering state machine. Token checkout introduces an order-level payment choice and synchronous token spend call from Ordering.API to PaymentProcessor, while preserving asynchronous event flow for downstream order/payment lifecycle stages.

## Phase 0: Research Outcome

Phase 0 decisions are captured in [research.md](research.md). All technical unknowns were resolved for payment-method modeling, spend idempotency, refund compensation, and endpoint contract shape.

## Phase 1: Design Output

Phase 1 artifacts produced:

- [data-model.md](data-model.md)
- [contracts/token-checkout-api.md](contracts/token-checkout-api.md)
- [quickstart.md](quickstart.md)

## Complexity Tracking

No constitution exceptions required.

## Implementation Notes For /speckit-tasks

1. Backward compatibility: existing cash checkout payloads must continue to work (default `PaymentMethod = Cash`).
2. Dependency caveat: repo context docs lag implementation; verify `Catalog.API` persistence/serialization of `TokenPrice` is present before coding FR-003/FR-005 paths.
3. Compensation path: ensure token refund is idempotent when `OrderCreationFailedIntegrationEvent` is replayed.