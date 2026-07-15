# Feature Specification: Token-Based Checkout

**Feature Branch**: `005-token-adjusted-checkout`
**Created**: 2026-07-13
**Status**: Draft

**Note**: This spec was revised to remove the token-to-dollar conversion rate and partial
cash+token blending from the original design. Per constitution Principle II (NON-NEGOTIABLE),
tokens MUST NOT be cashed out or redeemed for real currency or any other real-world value.
Establishing a `$ per token` rate to discount a cash payment is economically equivalent to cashing
tokens out, so it is disallowed. Instead, a buyer chooses one of two mutually exclusive payment
methods for an order: pay the full cash price, or pay the full token price (`CatalogItem.TokenPrice`,
set in spec 004) — there is no partial/blended payment.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Purchase a Club with Tokens Instead of Cash (Priority: P1)

A buyer with enough tokens can choose to pay for a club entirely with tokens instead of cash. No payment card interaction is required, and no cash amount is charged.

**Why this priority**: This is the primary token redemption mechanic — the reason sellers want to earn tokens and buyers want to accumulate them.

**Independent Test**: Add a club to the basket, select "Pay with Tokens", place the order — the buyer's token balance decreases by exactly `CatalogItem.TokenPrice`, and no cash payment is initiated.

**Acceptance Scenarios**:

1. **Given** a buyer has 120 tokens and a club's `TokenPrice` is 90, **When** they select "Pay with Tokens" and place the order, **Then** 90 tokens are debited from their wallet, `PaymentMethod` is `Tokens`, and the payment gateway is never called.
2. **Given** a buyer has 40 tokens and a club's `TokenPrice` is 90, **When** they attempt to select "Pay with Tokens", **Then** the option is disabled/rejected with a clear insufficient-balance error, and they must pay cash instead.
3. **Given** a buyer selects "Pay with Cash", **When** the order is placed, **Then** the full cash `Price` is charged via the payment gateway and no tokens are debited, regardless of the buyer's token balance.

---

### User Story 2 - Token Debit Fails — Order Rolls Back (Priority: P2)

If the token debit fails (e.g., balance changed between basket and order submission), the order is cancelled and the user is informed.

**Why this priority**: Data consistency — tokens must not be debited without a corresponding confirmed order, and an order must not proceed if token debit fails.

**Independent Test**: Simulate an insufficient balance at order submission time — the order is cancelled and no payment is charged.

**Acceptance Scenarios**:

1. **Given** a buyer's token balance drops below the club's `TokenPrice` between basket creation and order submission, **When** the order is placed with `PaymentMethod = Tokens`, **Then** Token.API rejects the debit, Ordering.API cancels the order, and the buyer receives an error.
2. **Given** a token debit failure, **When** the order is rolled back, **Then** no cash payment is initiated.

---

### Edge Cases

- What happens if a `TokenPrice` is not yet set on a listing (e.g., verification/token award hasn't completed) when a buyer attempts to select "Pay with Tokens"? → the token payment option MUST be unavailable until `TokenPrice` is set.
- Can a buyer mix tokens and cash on a multi-item basket (e.g., pay for item A with tokens and item B with cash in the same order)? → out of scope for this phase; each order uses a single `PaymentMethod` for its entire total (see Assumptions).
- What happens if two buyers simultaneously attempt to purchase the same single-stock listing with tokens?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CustomerBasket` MUST be extended with a `PaymentMethod` (enum: `Cash` | `Tokens`, default `Cash`) field stored in Redis.
- **FR-002**: `CreateOrderCommand` MUST be extended to include `PaymentMethod` (enum).
- **FR-003**: The `Order` aggregate MUST be extended with `PaymentMethod` (enum) and `TokensApplied` (int, equal to the item's `TokenPrice` when `PaymentMethod = Tokens`, otherwise 0).
- **FR-004**: `Token.API` MUST expose an internal `POST /api/tokens/spend` endpoint that debits the exact `TokenPrice` amount from a user's wallet atomically; it MUST return an error if the balance is insufficient.
- **FR-005**: Ordering.API MUST call Token.API to debit tokens as part of order creation when `PaymentMethod = Tokens`; if the debit fails, the order MUST NOT be created.
- **FR-006**: `PaymentProcessor` MUST skip the external payment gateway entirely when `PaymentMethod = Tokens`, and MUST publish `OrderPaymentSucceededIntegrationEvent` immediately once the token debit is confirmed.
- **FR-007**: `PaymentProcessor` MUST use the full cash `Price` (unchanged) when `PaymentMethod = Cash`; no token interaction occurs for cash orders.
- **FR-008**: If the token debit succeeds but order creation subsequently fails for any reason, Token.API MUST receive an `OrderCreationFailedIntegrationEvent` and refund the debited tokens.
- **FR-009**: A basket or order MUST NOT contain a mix of cash-paid and token-paid line items in this phase — `PaymentMethod` applies to the whole order.

### Key Entities

- **CustomerBasket** (extended): Adds `PaymentMethod` (enum). Schema is JSON in Redis — no migration needed.
- **Order** (extended): Adds `PaymentMethod` (enum) and `TokensApplied` (int). EF Core migration required.
- **TokenSpendRequest**: Internal request DTO to Token.API carrying `UserId`, `Amount`, `OrderId` (for idempotency and potential refund).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A buyer's token balance is correctly decremented by exactly the item's `TokenPrice` on every successful token-paid order.
- **SC-002**: No cash payment is initiated on any order where `PaymentMethod = Tokens`.
- **SC-003**: Token debit and order creation are consistent — no scenario results in debited tokens without a confirmed order (verified by integration test simulating order-creation failure).
- **SC-004**: Checkout with tokens completes in the same time window as checkout with cash (no perceivable latency increase).
- **SC-005**: Token refund is issued within one event-processing cycle of an order-creation failure.
- **SC-006**: No code path anywhere in checkout computes or stores a dollar-equivalent value for tokens (verified by code review against constitution Principle II).

## Assumptions

- Tokens are never assigned a dollar exchange rate; `TokenPrice` is a token-denominated figure set by spec 004, unrelated to the item's cash `Price`.
- Partial/blended payment (some cash + some tokens on one order) is explicitly out of scope for this phase — an order is paid entirely in one currency.
- Token debit happens synchronously during order creation (not via event bus) to ensure consistency before the order is persisted.
- Token.API's `spend` endpoint is not publicly accessible — it is only called service-to-service from Ordering.API.
- This phase depends on Phase 4 (Token Wallet Service) being complete and deployed, specifically the `CatalogItem.TokenPrice` field it populates.
