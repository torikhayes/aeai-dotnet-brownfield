# Feature Specification: Token Wallet Service

**Feature Branch**: `004-token-wallet-service`
**Created**: 2026-07-13
**Status**: Draft

**Note**: This spec was revised to align with the project constitution (Principle III). Tokens are
now earned at **listing time**, once a listing passes automated verification (defined in spec
007), and the reward amount comes from a **category × condition lookup table** — not from an
engagement-based score computed at time of sale. This also removes the prior dependency on a
`TokenPotentialScore` field (spec 003 no longer defines one).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Earn Tokens When a Listing Passes Verification (Priority: P1)

When a seller submits a club listing and it passes the automated verification step (photo
presence/plausibility check, defined in spec 007), they automatically receive tokens looked up
from the club's category and condition grade. Tokens appear in their wallet without any manual
action, and the same amount is recorded as the club's `TokenPrice` for buyers.

**Why this priority**: Token earning is the core value proposition for sellers — it must work reliably before any spending feature is built.

**Independent Test**: Create and verify a listing, then call `GET /api/tokens/balance` as the seller — token balance has increased by the lookup-table amount for that listing's category and condition.

**Acceptance Scenarios**:

1. **Given** a seller lists a "Driver" in "Excellent" condition, and the lookup table maps (Driver, Excellent) → 40 tokens, **When** the listing passes automated verification, **Then** seller's token balance increases by 40 tokens.
2. **Given** a club was listed by a platform admin (no `SellerId`), **When** the listing is created, **Then** no token award event is generated.
3. **Given** the Token service is temporarily unavailable, **When** a listing passes verification, **Then** the listing still becomes visible and the token award is retried via the event bus (at-least-once delivery).
4. **Given** a listing fails automated verification (e.g., insufficient photos), **When** the seller resubmits with corrected evidence and it passes, **Then** tokens are awarded exactly once for that listing.

---

### User Story 2 - Check Token Balance (Priority: P1)

An authenticated user can view their current token balance at any time.

**Why this priority**: Users need to know how many tokens they have before deciding whether to spend them on another listing.

**Independent Test**: Call `GET /api/tokens/balance` as an authenticated user — returns a numeric balance.

**Acceptance Scenarios**:

1. **Given** a user has never listed a club, **When** they check their balance, **Then** the response returns 0 tokens.
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

- What happens if a listing-verified event is delivered twice (duplicate message)?
- Can a token balance go negative?
- What happens if the lookup table is retuned after a listing was verified but before the award event is processed (use the table version in effect at verification time, per constitution Principle III)?
- What happens to a seller's already-earned tokens if the listing is later found to misrepresent condition? (See spec 007 — clawback.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A new `Token.API` service MUST be added to the solution following the same conventions as `Webhooks.API` (Aspire registration, `AddServiceDefaults()`, EF Core + Postgres).
- **FR-002**: A new `tokendb` Postgres database MUST be declared in `eShop.AppHost/Program.cs`.
- **FR-003**: `Token.API` MUST expose `GET /api/tokens/balance` (authenticated) returning the caller's current token balance as an integer.
- **FR-004**: `Token.API` MUST expose `GET /api/tokens/transactions` (authenticated) returning a paginated list of token transactions for the caller.
- **FR-005**: `Token.API` MUST own a maintained lookup table keyed on (`ClubCategory` × `ConditionGrade`) → token amount, per constitution Principle III. The table is configuration-driven (e.g., `appsettings.json`, hot-reloadable) and versioned so historical awards remain re-derivable.
- **FR-006**: `Catalog.API` MUST run an automated verification step (defined in spec 007) when a seller-owned listing is created or edited, and — upon passing — MUST publish `ClubListingVerifiedIntegrationEvent` containing `CatalogItemId`, `SellerId`, `ClubCategory`, `ConditionGrade`, `EventId`.
- **FR-007**: `Token.API` MUST subscribe to `ClubListingVerifiedIntegrationEvent`, resolve the token amount from its lookup table using the event's `ClubCategory` and `ConditionGrade`, and credit that amount to the seller's wallet.
- **FR-008**: Token awards MUST be idempotent — processing the same `ClubListingVerifiedIntegrationEvent` twice MUST NOT result in double-crediting (deduplicate by `EventId`).
- **FR-009**: After crediting an award, `Token.API` MUST publish `TokenValueAssignedIntegrationEvent` containing `CatalogItemId` and the awarded `TokenAmount`.
- **FR-010**: `Catalog.API` MUST subscribe to `TokenValueAssignedIntegrationEvent` and set `CatalogItem.TokenPrice` to the awarded amount, making it visible to buyers as the token cost to acquire that listing (see spec 005).
- **FR-011**: A `TokenWallet` entity MUST store `UserId` and `Balance`. A `TokenTransaction` entity MUST store `UserId`, `Amount`, `Reason`, `RelatedEventId`, and `CreatedAt`.
- **FR-012**: Token balances MUST NOT go below zero; spending requests that would result in a negative balance MUST be rejected.

### Key Entities

- **TokenWallet**: `UserId` (string), `Balance` (int). One wallet per user, created on first earn or explicit balance check.
- **TokenTransaction**: `Id`, `UserId`, `Amount` (positive = earn, negative = spend), `Reason` (string), `RelatedEventId` (string, for idempotency), `CreatedAt`.
- **TokenValuationRule** (lookup table row): `ClubCategory` (string), `ConditionGrade` (string), `TokenAmount` (int), `EffectiveFrom` (timestamp) — versioned so past awards remain auditable per Principle II.
- **ClubListingVerifiedIntegrationEvent**: New integration event carrying `CatalogItemId`, `SellerId`, `ClubCategory`, `ConditionGrade`, `EventId`.
- **TokenValueAssignedIntegrationEvent**: New integration event carrying `CatalogItemId`, `TokenAmount`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Token balance is credited to the seller within the same event-processing cycle as listing verification (no manual intervention required).
- **SC-002**: Duplicate event delivery does not result in duplicate token credits — idempotency is verified by processing the same event ID twice and confirming balance changes only once.
- **SC-003**: Balance endpoint responds in under 200ms under normal load.
- **SC-004**: Transaction history returns results in correct reverse-chronological order.
- **SC-005**: A token balance never falls below zero regardless of concurrent spend requests.
- **SC-006**: The token amount awarded for any given listing is always re-derivable from the lookup table version in effect at verification time.

## Assumptions

- Token values are integers (no fractional tokens).
- The category × condition lookup table starts with a small fixed set of values (owned by product/business, not derived from market pricing) and is expected to be retuned over time by configuration change, not code change.
- Token.API uses JWT authentication identical to other services — no separate auth mechanism.
- An internal `POST /api/tokens/spend` endpoint is included for use by the checkout flow (spec 005) but is not exposed publicly.
- Tokens are never assigned a real-currency exchange rate anywhere in the system (constitution Principle II) — `TokenPrice` on a listing is a token-denominated cost, not a dollar-equivalent.
- No token expiry is implemented in this phase.
- This spec depends on spec 007 (Trust & Safety) for the definition of the automated verification step that gates `ClubListingVerifiedIntegrationEvent`.
