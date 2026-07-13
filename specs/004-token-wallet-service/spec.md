# Feature Specification: PaymentProcessor Token Ledger Extension

**Feature Branch**: `004-paymentprocessor-token-ledger`
**Created**: 2026-07-13
**Status**: Draft

> **Constitutional alignment**:
> - **Principle I**: Extends the existing `PaymentProcessor` service as the token wallet and ledger — no new service created.
> - **Principle II**: All token operations are idempotent and fully auditable; tokens are non-convertible (platform-only).
> - **Principle III**: Token award amounts derive exclusively from a category × condition lookup table; engagement metrics play no role. Tokens are credited only after automated listing verification passes, not at instant submission.
> - **Principle V**: All code touching the token ledger, award calculation, and balance mutation MUST follow TDD (failing test written and reviewed before implementation).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Earn Tokens When a Listing is Verified (Priority: P1)

After a seller submits a club listing and it passes automated verification (required photo count present, condition grade plausible), tokens are automatically credited to the seller's wallet. The token amount is determined by the category × condition lookup table, not by any engagement metric.

**Why this priority**: Token earning is the core value proposition for sellers and must be correct before the spending flow is built.

**Independent Test**: Submit a Driver in Excellent condition with the required photos. After the `ClubListingVerifiedIntegrationEvent` is processed, call `GET /api/tokens/balance` as the seller — balance has increased by the lookup table value for (Driver, Excellent).

**Acceptance Scenarios**:

1. **Given** seller A lists a Driver in Excellent condition with sufficient photos, **When** automated verification passes and `ClubListingVerifiedIntegrationEvent` is received, **Then** seller A's balance increases by the lookup table value for (Driver, Excellent).
2. **Given** a listing is submitted but fails verification (insufficient photos), **When** the verification event is not published, **Then** no tokens are credited.
3. **Given** a club listed by a platform admin (no `SellerId`), **When** a listing verified event is received with no `SellerId`, **Then** no tokens are credited and no error is raised.
4. **Given** the PaymentProcessor is temporarily unavailable, **When** a `ClubListingVerifiedIntegrationEvent` is published, **Then** the event is retried via the RabbitMQ event bus (at-least-once delivery) and tokens are credited once processing resumes.

---

### User Story 2 - Check Token Balance (Priority: P1)

An authenticated user can view their current token balance at any time.

**Why this priority**: Users need to know how many tokens they have before deciding whether to apply them at checkout.

**Independent Test**: Call `GET /api/tokens/balance` as an authenticated user — returns a numeric balance.

**Acceptance Scenarios**:

1. **Given** a user has never had a listing verified, **When** they check their balance, **Then** the response returns 0.
2. **Given** a user has earned 150 tokens, **When** they check their balance, **Then** the response returns 150.
3. **Given** an unauthenticated request, **When** the balance endpoint is called, **Then** the API returns HTTP 401.

---

### User Story 3 - View Token Transaction History (Priority: P2)

A user can see a log of all token earn and spend events associated with their account, including the listing and lookup table version that generated each earn entry.

**Why this priority**: Transparency and auditability — users and platform operators must be able to trace every token credit back to the listing attributes and lookup table version that produced it (Principle II).

**Independent Test**: After earning and spending tokens, call `GET /api/tokens/transactions` — the log shows both events with amounts, timestamps, and lookup table version on earn entries.

**Acceptance Scenarios**:

1. **Given** a user has earned tokens twice and spent once, **When** they view their transaction history, **Then** 3 entries are returned in reverse chronological order.
2. **Given** an earn transaction, **When** displayed, **Then** it shows: amount, reason (club category + condition), listing ID, and the `LookupTableVersion` that was in effect at award time.
3. **Given** a spend transaction, **When** displayed, **Then** it shows: amount, reason ("purchase debit"), and associated order ID.

---

### User Story 4 - Preview Token Reward Before Listing (Priority: P2)

A seller filling in a club listing can see the exact token reward they will earn for the selected category and condition grade, before submitting.

**Why this priority**: Sellers need to understand the reward structure upfront. Showing the lookup-table value at form time — rather than a speculative engagement-based score — is tamper-resistant and honest.

**Independent Test**: Select "Iron Set" + "Good" in the listing form — the preview shows the current lookup table value for (Iron Set, Good).

**Acceptance Scenarios**:

1. **Given** the current lookup table values, **When** a seller selects category = "Wedge" and condition = "Fair", **Then** the listing form displays the corresponding token reward amount.
2. **Given** the lookup table is updated, **When** a seller opens the listing form, **Then** the preview reflects the updated values.

---

### Edge Cases

- What happens if a token award event is delivered twice (duplicate event ID)? → Idempotency via `RelatedEventId` deduplication (FR-008).
- Can a token balance go negative? → No; rejected with explicit error (FR-010).
- What happens if the lookup table is updated after a listing is submitted but before the verified event is processed? → The active version at event processing time is used (FR-006); the version is stored in `TokenTransaction` for auditability.
- Can a seller earn tokens by resubmitting a failed listing after adding the required photos? → No — tokens are awarded at most once per `CatalogItemId`; subsequent verified events for the same listing are rejected (FR-008).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing `PaymentProcessor` service MUST be extended to act as the token wallet and ledger. No new top-level service is created. The extension follows the same Aspire registration and `AddServiceDefaults()` conventions already used by PaymentProcessor.
- **FR-002**: A new `tokendb` Postgres database MUST be declared in `eShop.AppHost/Program.cs` and referenced by PaymentProcessor.
- **FR-003**: PaymentProcessor MUST expose `GET /api/tokens/balance` (authenticated) returning the caller's current integer token balance. This requires adding an HTTP host to PaymentProcessor alongside its existing background worker.
- **FR-004**: PaymentProcessor MUST expose `GET /api/tokens/transactions` (authenticated) returning a paginated list of token transactions for the caller in reverse chronological order.
- **FR-005**: `Catalog.API` MUST publish `ClubListingVerifiedIntegrationEvent` when an authenticated listing passes automated verification (photo count ≥ required minimum for the given condition grade). The event MUST carry: `SellerId`, `CatalogItemId`, `Category` (club type), `Condition` (New/Excellent/Good/Fair), `EventId`.
- **FR-006**: PaymentProcessor MUST subscribe to `ClubListingVerifiedIntegrationEvent` and credit tokens to the seller's wallet using the **active lookup table version at the time the event is processed** (not at submission time). The `TokenTransaction` record MUST store the `LookupTableVersion` that was active at processing time.
- **FR-007**: The token award lookup table MUST be a server-side data structure (e.g., configuration file or DB table) keyed on (`ClubCategory` × `ConditionGrade`). The table MAY be retuned; each version increment MUST be recorded so past awards remain re-derivable.
- **FR-008**: All token award operations MUST be idempotent at two levels: (1) processing the same `EventId` twice MUST NOT double-credit the balance; (2) a `CatalogItemId` MUST NOT be credited more than once across all `ClubListingVerifiedIntegrationEvent` instances — if a listing is resubmitted after a failed verification, the second verified event MUST be rejected with no token credit. PaymentProcessor MUST track awarded `CatalogItemId` values in a deduplicated set in `tokendb`.
- **FR-009**: A `TokenWallet` entity MUST store `UserId` and `Balance` (int) and `RowVersion`. A wallet row MUST be created only on first token award, not on first balance read. `GET /api/tokens/balance` MUST return `0` for users with no wallet row without writing to the database. A `TokenTransaction` entity MUST store `Id`, `UserId`, `Amount`, `Reason`, `RelatedEventId`, `LookupTableVersion` (nullable, populated on earn transactions), and `CreatedAt`.
- **FR-010**: Token balances MUST NOT go below zero; any debit that would result in a negative balance MUST be rejected with an explicit error.
- **FR-010a**: `TokenWallet` MUST use EF Core optimistic concurrency via a `RowVersion` (byte[]) concurrency token. Any balance mutation that encounters a concurrency conflict MUST retry the operation (re-read, re-validate, re-save) up to a configurable maximum number of retries before returning an error.
- **FR-011**: An internal `POST /api/tokens/spend` endpoint MUST be added to PaymentProcessor for use by the checkout flow (spec 005). It MUST be bound to the internal Aspire service-mesh network only (not registered on the public-facing port), making it accessible exclusively via Aspire service-discovery URIs from Ordering.API and unreachable from outside the Aspire host.
- **FR-012**: PaymentProcessor MUST expose `GET /api/tokens/reward-preview?category={cat}&condition={cond}` (unauthenticated) returning the current lookup table value for the given category and condition, for use by the listing form UI.
- **FR-013**: Code paths touching the token ledger, award calculation, and balance mutation MUST be developed test-first (Principle V): failing tests written and reviewed before implementation, covering idempotent-retry and concurrent-balance-mutation scenarios.

### Key Entities

- **TokenWallet**: `UserId` (string), `Balance` (int), `RowVersion` (byte[], concurrency token). Created on first token award only — `GET /balance` for a user with no awards returns `0` without creating a row.
- **TokenTransaction**: `Id`, `UserId`, `Amount` (positive = earn, negative = spend), `Reason` (string), `RelatedEventId` (string, for idempotency), `LookupTableVersion` (string, nullable), `CreatedAt`.
- **TokenAwardLookupEntry**: `ClubCategory` (string), `ConditionGrade` (string), `TokenAmount` (int), `TableVersion` (string), `EffectiveFrom` (datetime). Immutable once published; new tunings insert new rows rather than updating existing ones.
- **TokenAwardedListing**: `CatalogItemId` (string, unique index). Records which listings have already been awarded tokens, used to enforce the one-award-per-listing rule (FR-008).
- **ClubListingVerifiedIntegrationEvent**: `SellerId`, `CatalogItemId`, `Category`, `Condition`, `EventId` (maps to `Id` on the `IntegrationEvent` base class; stored as `TokenTransaction.RelatedEventId` for idempotency), `OccurredOn` (maps to `CreationDate` on the base class).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Token balance is credited to the seller within the same event-processing cycle as the listing verification event — no manual intervention required.
- **SC-002**: Duplicate event delivery does not result in duplicate token credits — verified by processing the same `EventId` twice and confirming balance changes only once.
- **SC-003**: Every earn transaction record includes a `LookupTableVersion` value from which the award amount can be independently re-derived.
- **SC-004** *(post-launch SLO — not a build-time gate)*: Balance endpoint responds in under 200ms under normal load.
- **SC-005**: A token balance never falls below zero regardless of concurrent spend requests — verified by a concurrent-mutation integration test that triggers simultaneous debits exceeding the balance; the RowVersion conflict causes retries, and the final balance must be ≥ 0 with exactly the correct net debit applied.
- **SC-006**: Token ledger tests achieve 100% path coverage of idempotency, concurrent debit, and award calculation logic (Principle V TDD requirement).

## Assumptions

- Token values are integers (no fractional tokens).
- Award timing is at listing verification, not at club sale time; listing verification is automated (photo count check), not human-gated.
- The token award lookup table is seeded in `tokendb` at startup; the initial table values are defined as part of the data model work in the planning phase for this spec.
- PaymentProcessor adding HTTP endpoints alongside its background worker is an architectural extension consistent with Principle I's mandate that it become the token wallet; this is not a rewrite.
- Token.API as a separate service is explicitly NOT created; any references to "Token.API" in other specs are updated to reference PaymentProcessor token ledger endpoints.
- Tokens are platform-only credits (Principle II): they cannot be purchased, cashed out, or exchanged for real currency or gift cards.
- No token expiry is implemented in this phase.

## Clarifications

### Session 2026-07-13

- Q: How should concurrent balance mutations be handled? → A: Optimistic concurrency — `RowVersion` (byte[]) concurrency token on `TokenWallet` via EF Core; retry on conflict.
- Q: How is the internal spend endpoint isolated from external access? → A: Aspire service-mesh network isolation — endpoint bound to internal network only, reachable via service discovery from Ordering.API, not exposed on the public port.
- Q: When the lookup table updates between listing submission and verified event processing, which version is used for the award? → A: The active version at event processing time; stored in `TokenTransaction.LookupTableVersion` for audit trail.
- Q: Should `GET /balance` create a wallet row for users with no awards? → A: No — return `0` without writing; wallet row created only on first token award.
- Q: Can a seller earn tokens by resubmitting a failed listing after adding required photos? → A: No — tokens awarded at most once per `CatalogItemId`; subsequent verified events for the same listing are rejected.
