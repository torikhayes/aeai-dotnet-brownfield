# Research: Token-Based Checkout

**Feature**: 005-token-adjusted-checkout  
**Date**: 2026-07-15

## Decision 1: Payment Method Is Order-Level, Not Item-Level

**Decision**: Represent checkout choice as a single order-level enum (`Cash` or `Tokens`) persisted on both basket and order.

**Rationale**: The spec explicitly disallows blended cash+token payments in this phase. Encoding payment mode at order level keeps validation straightforward and prevents accidental mixed-tender order creation.

**Alternatives considered**:
- Per-line-item payment method: rejected because it directly enables mixed tender and creates complex split-payment reconciliation.
- Implicit method derived from presence of card fields: rejected because it obscures intent and complicates API validation.

## Decision 2: Token Spend Happens During Order Creation, Synchronously

**Decision**: Ordering.API calls `POST /api/tokens/spend` during `CreateOrderCommand` handling when `PaymentMethod = Tokens`, before order commit completes.

**Rationale**: The feature requires that failed token debit prevents order creation. Synchronous spend gives immediate decisioning and avoids asynchronous race windows where order state could advance without confirmed funds.

**Alternatives considered**:
- Event-driven spend after order submission: rejected due to eventual-consistency window and rollback complexity.
- Pre-authorization token reservation: rejected as unnecessary complexity for this phase.

## Decision 3: Refund Uses Compensating Event and Idempotent Ledger Rule

**Decision**: If token spend succeeds but order creation later fails, Ordering emits `OrderCreationFailedIntegrationEvent`; PaymentProcessor processes it as a compensating credit with idempotency keying.

**Rationale**: Keeps ledger mutations in PaymentProcessor while preserving reliable compensation for transactional failure edges.

**Alternatives considered**:
- Ordering directly calls a refund endpoint in catch/finally path: rejected because it tightly couples command failure paths to immediate network retry behavior.
- Database distributed transaction across services: rejected as incompatible with current architecture and operational complexity.

## Decision 4: Cash Flow Remains Existing Baseline

**Decision**: `PaymentMethod = Cash` continues to use existing stock-confirmed -> payment-processor event path and card metadata, with no token interaction.

**Rationale**: This minimizes regression risk and preserves established payment simulation and order lifecycle behavior.

**Alternatives considered**:
- Unify both paths under new direct payment endpoint: rejected because it duplicates proven event flow and increases blast radius.

## Decision 5: Basket Contract Extension Via gRPC Proto + Redis JSON

**Decision**: Extend `basket.proto` and `CustomerBasket` model with `PaymentMethod`, defaulting to `Cash`.

**Rationale**: Checkout UI needs durable, explicit method selection before order creation. Basket already persists as JSON in Redis and is the right pre-order state carrier.

**Alternatives considered**:
- Keep selection only in WebApp session state: rejected due to loss of server-side source of truth and cross-session inconsistencies.

## Decision 6: Internal Spend Endpoint Authorization Hardening

**Decision**: Keep `/api/tokens/spend` behind authorization and enforce service-to-service caller validation (or equivalent policy) for Ordering.API-origin requests.

**Rationale**: Endpoint is intended for internal mesh calls; explicit access restrictions reduce accidental user-token spend abuse.

**Alternatives considered**:
- Public endpoint with user JWT only: rejected because it allows direct client-triggered spending semantics outside Ordering invariants.

## Decision 7: Catalog TokenPrice Dependency Handling

**Decision**: Treat `CatalogItem.TokenPrice` backend persistence as a prerequisite check during implementation kickoff.

**Rationale**: UI model currently includes `TokenPrice`, but context and quick symbol scans do not show backend persistence in all services. The feature depends on this field being available in order creation input.

**Alternatives considered**:
- Add `TokenPrice` persistence in this feature scope unconditionally: rejected pending confirmation because spec states dependency on prior phase completion.
