# Feature Specification: Token-Adjusted Checkout

**Feature Branch**: `005-token-adjusted-checkout`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Apply Tokens at Checkout to Reduce Price (Priority: P1)

A buyer with tokens in their wallet can choose how many tokens to apply at checkout. The displayed total reduces accordingly, and the cash payment charged is for the net amount only.

**Why this priority**: This is the primary token redemption mechanic — the reason sellers want to earn tokens and buyers want to accumulate them.

**Independent Test**: Add a club to basket, set `TokensToRedeem` = 50, place the order — the resulting order's `NetPaymentAmount` equals `Price - 50` and the buyer's token balance decreases by 50.

**Acceptance Scenarios**:

1. **Given** a buyer has 100 tokens and a club costs $80, **When** they apply 80 tokens at checkout, **Then** the order's `NetPaymentAmount` is $0 and 80 tokens are debited from their wallet.
2. **Given** a buyer applies 30 tokens to a $80 club, **When** the order is placed, **Then** `NetPaymentAmount` is $50 and 30 tokens are debited.
3. **Given** a buyer tries to apply 150 tokens but only has 100, **When** the order is submitted, **Then** the request is rejected with a clear insufficient-balance error.
4. **Given** a buyer applies 0 tokens, **When** the order is placed, **Then** the full price is charged and no tokens are debited.

---

### User Story 2 - Free Purchase with Sufficient Tokens (Priority: P1)

A buyer with enough tokens can purchase a club at zero cash cost. No payment card interaction is required.

**Why this priority**: This is a core marketplace differentiator — the "free club" scenario motivates both earning and spending tokens.

**Independent Test**: Apply tokens equal to or greater than the club's price — `NetPaymentAmount` is 0, payment gateway is not called, order completes successfully.

**Acceptance Scenarios**:

1. **Given** a buyer has 200 tokens and a club costs $120, **When** they apply 120 tokens, **Then** `NetPaymentAmount` is $0, the payment processor skips the gateway call, and the order moves directly to confirmed.
2. **Given** a zero-cost order, **When** the order status flow runs, **Then** `PaymentSucceeded` is published immediately by the payment processor without an external gateway request.

---

### User Story 3 - Token Debit Fails — Order Rolls Back (Priority: P2)

If the token debit fails (e.g., balance changed between basket and order submission), the order is cancelled and the user is informed.

**Why this priority**: Data consistency — tokens must not be debited without a corresponding confirmed order, and an order must not proceed if token debit fails.

**Independent Test**: Simulate an insufficient balance at order submission time — the order is cancelled and no payment is charged.

**Acceptance Scenarios**:

1. **Given** a buyer's token balance drops to 0 between basket creation and order submission, **When** the order is placed with tokens, **Then** Token.API rejects the debit, Ordering.API cancels the order, and the buyer receives an error.
2. **Given** a token debit failure, **When** the order is rolled back, **Then** no cash payment is initiated.

---

### Edge Cases

- What happens if the payment gateway fails after tokens have already been debited?
- Can a buyer apply more tokens than the price of the club (overpay with tokens)?
- What token-to-dollar exchange rate is used?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CustomerBasket` MUST be extended with a `TokensToRedeem` (int, default 0) field stored in Redis.
- **FR-002**: `CreateOrderCommand` MUST be extended to include `TokensApplied` (int).
- **FR-003**: The `Order` aggregate MUST be extended with `TokensApplied` (int) and `NetPaymentAmount` (decimal), where `NetPaymentAmount = max(0, TotalPrice - TokensApplied × TokenToDollarRate)`.
- **FR-004**: The token-to-dollar conversion rate MUST be configurable via app settings (e.g., 1 token = $1.00 by default).
- **FR-005**: `Token.API` MUST expose an internal `POST /api/tokens/spend` endpoint that debits tokens from a user's wallet atomically; it MUST return an error if the balance is insufficient.
- **FR-006**: Ordering.API MUST call Token.API to debit tokens as part of order creation when `TokensApplied > 0`; if the debit fails, the order MUST NOT be created.
- **FR-007**: `PaymentProcessor` MUST use `NetPaymentAmount` instead of the gross order total when calling the payment gateway.
- **FR-008**: If `NetPaymentAmount` is 0, `PaymentProcessor` MUST publish `OrderPaymentSucceededIntegrationEvent` immediately without calling any external payment gateway.
- **FR-009**: If the payment gateway fails after a successful token debit, Token.API MUST receive a `OrderPaymentFailedIntegrationEvent` and refund the debited tokens.

### Key Entities

- **CustomerBasket** (extended): Adds `TokensToRedeem` (int). Schema is JSON in Redis — no migration needed.
- **Order** (extended): Adds `TokensApplied` (int) and `NetPaymentAmount` (decimal). EF Core migration required.
- **TokenSpendRequest**: Internal request DTO to Token.API carrying `UserId`, `Amount`, `OrderId` (for idempotency and potential refund).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A buyer's token balance is correctly decremented by the applied amount on every successful order.
- **SC-002**: No cash payment is initiated when `NetPaymentAmount` is $0.00.
- **SC-003**: Token debit and order creation are consistent — no scenario results in debited tokens without a confirmed order (verified by integration test simulating payment failure).
- **SC-004**: Checkout with tokens completes in the same time window as checkout without tokens (no perceivable latency increase).
- **SC-005**: Token refund is issued within one event-processing cycle of a payment failure.

## Assumptions

- 1 token = $1.00 USD by default; this rate is configurable but not user-adjustable.
- Buyers cannot apply more tokens than the total price of the order (capped at 100% discount).
- Token debit happens synchronously during order creation (not via event bus) to ensure consistency before the order is persisted; this is an acceptable trade-off for Phase 1.
- Partial token application (e.g., apply 50 tokens to a $120 club, pay $70 cash) is fully supported.
- Token.API's `spend` endpoint is not publicly accessible — it is only called service-to-service from Ordering.API.
- This phase depends on Phase 4 (Token Wallet Service) being complete and deployed.
