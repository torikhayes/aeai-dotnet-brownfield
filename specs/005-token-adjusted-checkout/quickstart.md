# Quickstart: Token-Based Checkout

**Feature**: 005-token-adjusted-checkout

## Prerequisites

- Local environment configured (containers + .NET 10 SDK)
- AppHost boots successfully
- Token ledger migrations and seed data are applied (`tokendb`)
- A test user with known token balance exists

## Start the System

```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

## Validate Baseline Cash Checkout (Regression)

1. Add a catalog item to basket.
2. Select `Cash` checkout in UI.
3. Place order.
4. Confirm behavior is unchanged:
   - order created successfully
   - existing payment simulation/event path still executes
   - token balance unchanged

## Validate Token Checkout

1. Ensure basket item has non-null token price and user balance is sufficient.
2. In checkout, select `Tokens`.
3. Place order.
4. Verify:
   - no card gateway call is initiated
   - `PaymentProcessor /api/tokens/spend` is called once
   - order persists with `PaymentMethod = Tokens`
   - `TokensApplied` equals summed token amount
   - user token balance decreases by exact amount

## Validate Insufficient Balance Guard

1. Use user balance lower than required token amount.
2. Attempt token checkout.
3. Verify:
   - clear insufficient-balance response shown
   - no order persisted
   - no cash charge initiated

## Validate Compensation Flow

1. Simulate failure after successful spend (e.g., force order persistence failure in integration test).
2. Verify:
   - `OrderCreationFailedIntegrationEvent` is emitted
   - PaymentProcessor applies one refund transaction
   - final balance matches pre-attempt balance

## Suggested Test Commands

```bash
# ordering domain/app behavior
dotnet test tests/Ordering.UnitTests

# ordering API integration behavior
dotnet test tests/Ordering.FunctionalTests

# token ledger spend/refund behaviors
dotnet test tests/PaymentProcessor.UnitTests
```

## Validation Checklist

- [x] Cash checkout still succeeds and does not debit tokens.
- [x] Token checkout succeeds when spend call succeeds.
- [x] Token checkout returns `insufficient_balance` when spend is rejected.
- [x] Post-spend order persistence failure emits `OrderCreationFailedIntegrationEvent`.
- [x] Compensation refund is idempotent for duplicate rollback events.
- [x] Internal spend endpoint accepts only authenticated callers or trusted `X-Internal-Client: ordering-api` traffic.

## Latest Test Evidence

- `dotnet test tests/Ordering.UnitTests/Ordering.UnitTests.csproj`: Passed (41)
- `dotnet test tests/PaymentProcessor.UnitTests/PaymentProcessor.UnitTests.csproj`: Passed (29)
- `dotnet test tests/Ordering.FunctionalTests/Ordering.FunctionalTests.csproj`: Passed (16)

## Implementation Checklist

- Extend basket proto + model with payment method
- Extend ordering API request/command/validator with payment method
- Add order aggregate + EF migration fields (`PaymentMethod`, `TokensApplied`)
- Add ordering-to-paymentprocessor spend call for token path
- Skip card gateway path when order payment method is tokens
- Emit and consume compensation event for post-spend order failure
- Add/update tests for idempotency, insufficient balance, and rollback consistency