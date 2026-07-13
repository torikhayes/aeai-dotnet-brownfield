# Feature Specification: Token Wallet Service

**Feature Branch**: `004-token-wallet-service`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Earn Tokens When a Club Sells (Priority: P1)

When a seller's club is purchased, they automatically receive tokens proportional to the club's `TokenPotentialScore`. Tokens appear in their wallet without any manual action.

**Why this priority**: Token earning is the core value proposition for sellers — it must work reliably before any spending feature is built.

**Independent Test**: Complete a club purchase flow, then call `GET /api/tokens/balance` as the seller — token balance has increased by the expected amount.

**Acceptance Scenarios**:

1. **Given** seller A's club has a `TokenPotentialScore` of 50, **When** the club is sold (order paid), **Then** seller A's token balance increases by 50 tokens.
2. **Given** a club was listed by a platform admin (no `SellerId`), **When** the club is sold, **Then** no token award event is generated.
3. **Given** the Token service is temporarily unavailable, **When** a club sells, **Then** the sale still completes and the token award is retried via the event bus (at-least-once delivery).

---

### User Story 2 - Check Token Balance (Priority: P1)

An authenticated user can view their current token balance at any time.

**Why this priority**: Users need to know how many tokens they have before deciding whether to apply them at checkout.

**Independent Test**: Call `GET /api/tokens/balance` as an authenticated user — returns a numeric balance.

**Acceptance Scenarios**:

1. **Given** a user has never sold a club, **When** they check their balance, **Then** the response returns 0 tokens.
2. **Given** a user has earned 150 tokens, **When** they check their balance, **Then** the response returns 150.
3. **Given** an unauthenticated request, **When** the balance endpoint is called, **Then** the API returns HTTP 401.

---

### User Story 3 - View Token Transaction History (Priority: P2)

A user can see a log of all token earn and spend events associated with their account.

**Why this priority**: Transparency and trust — users need to understand where their tokens came from and where they went.

**Independent Test**: After earning and spending tokens, call `GET /api/tokens/transactions` — the log shows both events with correct amounts and timestamps.

**Acceptance Scenarios**:

1. **Given** a user has earned tokens twice and spent once, **When** they view their transaction history, **Then** 3 entries are returned in reverse chronological order.
2. **Given** each transaction, **When** displayed, **Then** it shows: amount (positive for earn, negative for spend), reason/description, and timestamp.

---

### Edge Cases

- What happens if a token award event is delivered twice (duplicate message)?
- Can a token balance go negative?
- What happens if the `TokenPotentialScore` on a club changes between listing and sale?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A new `Token.API` service MUST be added to the solution following the same conventions as `Webhooks.API` (Aspire registration, `AddServiceDefaults()`, EF Core + Postgres).
- **FR-002**: A new `tokendb` Postgres database MUST be declared in `eShop.AppHost/Program.cs`.
- **FR-003**: `Token.API` MUST expose `GET /api/tokens/balance` (authenticated) returning the caller's current token balance as an integer.
- **FR-004**: `Token.API` MUST expose `GET /api/tokens/transactions` (authenticated) returning a paginated list of token transactions for the caller.
- **FR-005**: `Token.API` MUST subscribe to `ClubSoldIntegrationEvent` (published by `Catalog.API` when a seller-owned club's stock reaches 0 after a paid order) and credit tokens equal to the club's `TokenPotentialScore` (rounded to the nearest integer) to the seller's wallet.
- **FR-006**: `Catalog.API` MUST publish `ClubSoldIntegrationEvent` containing `SellerId` and `TokenPotentialScore` when `OrderStatusChangedToPaidIntegrationEventHandler` decrements stock on a seller-owned item to 0.
- **FR-007**: Token awards MUST be idempotent — processing the same `ClubSoldIntegrationEvent` twice MUST NOT result in double-crediting (deduplicate by event ID).
- **FR-008**: A `TokenWallet` entity MUST store `UserId` and `Balance`. A `TokenTransaction` entity MUST store `UserId`, `Amount`, `Reason`, `RelatedEventId`, and `CreatedAt`.
- **FR-009**: Token balances MUST NOT go below zero; spending requests that would result in a negative balance MUST be rejected.

### Key Entities

- **TokenWallet**: `UserId` (string), `Balance` (int). One wallet per user, created on first earn or explicit balance check.
- **TokenTransaction**: `Id`, `UserId`, `Amount` (positive = earn, negative = spend), `Reason` (string), `RelatedEventId` (string, for idempotency), `CreatedAt`.
- **ClubSoldIntegrationEvent**: New integration event carrying `SellerId`, `CatalogItemId`, `TokenPotentialScore`, `EventId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Token balance is credited to the seller within the same event-processing cycle as the order payment confirmation (no manual intervention required).
- **SC-002**: Duplicate event delivery does not result in duplicate token credits — idempotency is verified by processing the same event ID twice and confirming balance changes only once.
- **SC-003**: Balance endpoint responds in under 200ms under normal load.
- **SC-004**: Transaction history returns results in correct reverse-chronological order.
- **SC-005**: A token balance never falls below zero regardless of concurrent spend requests.

## Assumptions

- Token values are integers (no fractional tokens).
- `TokenPotentialScore` is snapshotted at time of sale from the `CatalogItem`; changes to the score after listing but before sale use the value at the moment of the paid event.
- Token.API uses JWT authentication identical to other services — no separate auth mechanism.
- An internal `POST /api/tokens/spend` endpoint is included for use by the checkout flow (Phase 5) but is not exposed publicly.
- The token-to-dollar conversion rate (used in Phase 5) is not defined in this phase; Token.API only tracks token quantities.
- No token expiry is implemented in this phase.
