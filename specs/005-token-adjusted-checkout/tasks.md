# Tasks: Token-Based Checkout

**Feature**: 005-token-adjusted-checkout
**Input**: `specs/005-token-adjusted-checkout/` — `005-token-adjusted-checkout.spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/token-checkout-api.md`, `quickstart.md`
**Tests**: Included. The spec defines independent test criteria and this feature touches token-ledger and order-state logic.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label (`US1`, `US2`) for story-phase tasks only
- Every task includes an exact file path

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the feature branch workspace and test harness alignment for checkout/token flow work.

- [X] T001 Create feature-level test checklist updates in `specs/005-token-adjusted-checkout/quickstart.md` reflecting token checkout and rollback assertions
- [X] T002 [P] Add or update token-checkout-focused test fixture data in `tests/Ordering.FunctionalTests/` for users with sufficient and insufficient token balances
- [X] T003 [P] Add shared constants for payment-method literals/enums in `src/Ordering.API/Application/Models/` to avoid string drift between API and command layers

**Checkpoint**: Shared setup is complete and story work can begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement cross-story primitives required by both user stories.

**CRITICAL**: User story implementation starts only after this phase.

- [X] T004 Extend basket contract enum and fields in `src/Basket.API/Proto/basket.proto` (`PaymentMethod`, request/response fields)
- [X] T005 Update basket domain model with payment mode default in `src/Basket.API/Model/CustomerBasket.cs`
- [X] T006 Update basket gRPC mapping for payment mode in `src/Basket.API/Grpc/BasketService.cs`
- [X] T007 [P] Extend checkout request DTO with payment method in `src/Ordering.API/Apis/OrdersApi.cs`
- [X] T008 [P] Extend order command model with payment method in `src/Ordering.API/Application/Commands/CreateOrderCommand.cs`
- [X] T009 Add payment-method validation rules in `src/Ordering.API/Application/Validations/CreateOrderCommandValidator.cs`
- [X] T010 [P] Add order-level payment enum in `src/Ordering.Domain/AggregatesModel/OrderAggregate/OrderPaymentMethod.cs`
- [X] T011 Update order aggregate for `PaymentMethod` and `TokensApplied` in `src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs`
- [X] T012 Update EF mapping for new order columns in `src/Ordering.Infrastructure/EntityConfigurations/OrderEntityTypeConfiguration.cs`
- [X] T013 Create EF migration for `orders.payment_method` and `orders.tokens_applied` in `src/Ordering.Infrastructure/Migrations/`

**Checkpoint**: Basket and Ordering now support payment-method semantics and persistence.

---

## Phase 3: User Story 1 - Purchase a Club with Tokens Instead of Cash (Priority: P1) MVP

**Goal**: Allow full-order token checkout when balance is sufficient, while preserving existing cash checkout behavior.

**Independent Test**: Add a club to basket, choose tokens, place order; token balance decreases by exact `TokenPrice`; no cash payment is initiated.

### Tests for User Story 1

- [X] T014 [P] [US1] Add functional test for successful token checkout in `tests/Ordering.FunctionalTests/` validating order creation and token debit amount
- [X] T015 [P] [US1] Add functional test for insufficient-balance rejection in `tests/Ordering.FunctionalTests/` validating no order persistence
- [X] T016 [P] [US1] Add functional test for cash checkout regression in `tests/Ordering.FunctionalTests/` validating no token debit on cash path
- [X] T017 [P] [US1] Add payment-processor unit test asserting spend idempotency by `orderId` for checkout debit in `tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs`

### Implementation for User Story 1

- [X] T018 [US1] Update checkout UI to select payment method in `src/WebApp/Components/Pages/Checkout/Checkout.razor`
- [X] T019 [US1] Propagate payment method through web basket client in `src/WebApp/Services/BasketService.cs`
- [X] T020 [P] [US1] Update catalog item checkout display for token availability in `src/WebAppComponents/Catalog/CatalogItem.cs`
- [X] T021 [US1] Map API request payment method to create-order command in `src/Ordering.API/Apis/OrdersApi.cs`
- [X] T022 [US1] Implement token spend orchestration in `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs` for `PaymentMethod = Tokens`
- [X] T023 [US1] Implement `TokensApplied` computation and aggregate assignment in `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs`
- [X] T024 [US1] Ensure `PaymentMethod = Cash` path preserves existing card/gateway behavior in `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs`
- [X] T025 [US1] Adjust payment processor stock-confirmed handler to short-circuit gateway for token-paid orders in `src/PaymentProcessor/IntegrationEvents/EventHandling/OrderStatusChangedToStockConfirmedIntegrationEventHandler.cs`
- [X] T026 [US1] Extend token spend endpoint auth policy for internal Ordering flow in `src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs`

**Checkpoint**: US1 delivers token checkout and preserves cash checkout behavior.

---

## Phase 4: User Story 2 - Token Debit Fails, Order Rolls Back (Priority: P2)

**Goal**: Prevent inconsistent states by cancelling order creation when debit fails and compensating refunded tokens if post-spend order persistence fails.

**Independent Test**: Simulate debit failure and post-spend order failure; verify no charge, no persisted inconsistent order, and eventual token refund.

### Tests for User Story 2

- [X] T027 [P] [US2] Add functional test for debit rejection during order submit in `tests/Ordering.FunctionalTests/` validating cancellation and error response
- [X] T028 [P] [US2] Add integration test for successful spend followed by order persistence failure in `tests/Ordering.FunctionalTests/` validating compensation event emission
- [X] T029 [P] [US2] Add payment-processor unit test for idempotent refund handling on duplicate `OrderCreationFailedIntegrationEvent` in `tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs`

### Implementation for User Story 2

- [X] T030 [US2] Add ordering-side compensation event contract in `src/Ordering.API/Application/IntegrationEvents/Events/OrderCreationFailedIntegrationEvent.cs`
- [X] T031 [US2] Publish compensation event on post-spend order-create failure in `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs`
- [X] T032 [US2] Add payment-processor integration event contract in `src/PaymentProcessor/IntegrationEvents/Events/OrderCreationFailedIntegrationEvent.cs`
- [X] T033 [US2] Implement refund event handler in `src/PaymentProcessor/IntegrationEvents/EventHandling/OrderCreationFailedIntegrationEventHandler.cs`
- [X] T034 [US2] Register compensation event subscription in `src/PaymentProcessor/Program.cs`
- [X] T035 [US2] Map token spend failures to explicit ordering API error responses (`insufficient_balance`, `token_service_unavailable`) in `src/Ordering.API/Apis/OrdersApi.cs`

**Checkpoint**: US2 prevents inconsistent debit/order states and provides deterministic rollback behavior.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency, observability, and verification across both stories.

- [X] T036 [P] Add structured logging for token checkout decisions and spend outcomes in `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs`
- [X] T037 [P] Add structured logging for compensation refunds in `src/PaymentProcessor/IntegrationEvents/EventHandling/OrderCreationFailedIntegrationEventHandler.cs`
- [X] T038 Validate end-to-end quickstart scenarios and update notes in `specs/005-token-adjusted-checkout/quickstart.md`
- [X] T039 Run and record test results for `tests/Ordering.UnitTests`, `tests/Ordering.FunctionalTests`, and `tests/PaymentProcessor.UnitTests` in `specs/005-token-adjusted-checkout/tasks.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and can start after US1 token spend contract is in place (`T022`, `T026`).
- **Phase 5 (Polish)**: Depends on completion of selected story phases.

### User Story Dependencies

- **US1 (P1)**: No dependency on US2; delivers MVP.
- **US2 (P2)**: Depends on US1 token-spend path existing and adds rollback/compensation guarantees.

### Within Each User Story

- Tests first, then implementation.
- API/DTO/model updates before command-handler orchestration.
- Event contracts before subscriptions/handlers.

### Parallel Opportunities

- Phase 1: `T002` and `T003` can run in parallel.
- Phase 2: `T007`/`T008` and `T010` can run in parallel while basket tasks complete.
- US1 tests: `T014`-`T017` can run in parallel.
- US2 tests: `T027`-`T029` can run in parallel.
- Polish logging tasks: `T036` and `T037` can run in parallel.

---

## Parallel Example: User Story 1

```text
Task: Add functional test for successful token checkout in tests/Ordering.FunctionalTests/
Task: Add functional test for insufficient-balance rejection in tests/Ordering.FunctionalTests/
Task: Add payment-processor unit test asserting spend idempotency by orderId in tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs
```

## Parallel Example: User Story 2

```text
Task: Add functional test for debit rejection during order submit in tests/Ordering.FunctionalTests/
Task: Add integration test for post-spend order failure compensation in tests/Ordering.FunctionalTests/
Task: Add payment-processor unit test for idempotent refund handling in tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1) only.
3. Validate US1 independent test criteria and cash regression.
4. Demo/deploy MVP if stable.

### Incremental Delivery

1. Deliver US1 for token checkout core value.
2. Deliver US2 for rollback/compensation robustness.
3. Complete polish and full suite verification.

### Format Validation

- All tasks follow `- [ ] T### ...` checklist format.
- Story tasks include `[US1]` or `[US2]` labels.
- Parallelizable tasks are explicitly tagged `[P]`.
- Every task contains a concrete file path.