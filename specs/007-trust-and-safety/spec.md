# Feature Specification: Trust & Safety

**Feature Branch**: `007-trust-and-safety`
**Created**: 2026-07-13
**Status**: Draft

**Note**: This spec implements constitution Principle IV (Trust & Safety in Physical Trades) and
the automated verification step referenced by spec 004 (Token Wallet Service). It has no prior
coverage in specs 001–006.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Photo Evidence Required for Condition Claims (Priority: P1)

A seller listing a club must attach enough photos to back up its stated condition grade before the listing becomes visible to buyers or earns tokens.

**Why this priority**: Without evidence, a condition grade is just an unverified claim — the single biggest source of buyer distrust and dispute volume in a physical-goods marketplace.

**Independent Test**: Submit a listing with zero or too few photos — it is rejected/held; submit one with enough photos — it passes and becomes visible.

**Acceptance Scenarios**:

1. **Given** a seller submits a listing with fewer than the required minimum photos (default: 2), **When** the listing is created, **Then** it is stored in a `PendingVerification` state, is NOT visible in the public catalog, and does NOT trigger a token award.
2. **Given** a seller submits a listing with the required minimum photos, **When** automated verification runs, **Then** the listing transitions to `Verified`, becomes visible in the public catalog, and `Catalog.API` publishes `ClubListingVerifiedIntegrationEvent` (consumed by spec 004).
3. **Given** a listing is `PendingVerification`, **When** the seller adds enough photos and resubmits, **Then** verification re-runs and the listing may transition to `Verified` exactly once (no duplicate token award).

---

### User Story 2 - Open a Dispute on a Completed Trade (Priority: P1)

A buyer who receives a club that doesn't match its stated condition can open a dispute on the completed order.

**Why this priority**: Disputes are the mechanism that makes the condition-evidence requirement meaningful after the fact — buyers need recourse.

**Independent Test**: Complete a purchase, open a dispute referencing that order, and verify the dispute is recorded and a hold is placed.

**Acceptance Scenarios**:

1. **Given** a buyer has a completed order, **When** they open a dispute with a reason, **Then** a `TradeDispute` record is created with status `Open`, linked to the order, the listing, and the seller.
2. **Given** a dispute is opened on an order paid with tokens, **When** the dispute is created, **Then** a `WalletHold` equal to the seller's original listing-reward token amount is placed on the seller's wallet, reducing their spendable balance by that amount until the dispute resolves.
3. **Given** a dispute is opened on an order paid with cash, **When** the dispute is created, **Then** the order is flagged `DisputeOpen` so a refund cannot be processed as a normal return until the dispute resolves.
4. **Given** an order that is already disputed, **When** the same buyer attempts to open a second dispute on it, **Then** the request is rejected — one open dispute per order.

---

### User Story 3 - Resolve a Dispute (Priority: P2)

A moderator (or automated policy, in later phases) resolves a dispute by upholding or rejecting the buyer's claim.

**Why this priority**: Disputes must reach a conclusion — an indefinitely open dispute permanently locks seller tokens and buyer trust.

**Independent Test**: Resolve a dispute as "upheld" — verify clawback and refund occur; resolve as "rejected" — verify the hold is released and no funds move.

**Acceptance Scenarios**:

1. **Given** an open dispute is resolved as `Upheld` (misrepresentation confirmed), **When** resolution is recorded, **Then** the seller's `WalletHold` is converted into a permanent debit (clawback) via a new `TokenTransaction` with reason `misrepresentation_clawback`, and the buyer is refunded (tokens returned to wallet, or cash refund initiated via `PaymentProcessor`, depending on the order's `PaymentMethod`).
2. **Given** an open dispute is resolved as `Rejected` (claim not substantiated), **When** resolution is recorded, **Then** the seller's `WalletHold` is released (no debit), and no refund is issued.
3. **Given** a dispute is resolved (either outcome), **When** resolution completes, **Then** its status can no longer be changed (immutable once resolved).

---

### User Story 4 - Seller Accountability for Repeated Misrepresentation (Priority: P3)

A seller with multiple upheld misrepresentation disputes is flagged for account-level review.

**Why this priority**: Individual clawbacks address single incidents; repeated offenses need a stronger signal to protect the marketplace.

**Independent Test**: Simulate 3 upheld disputes against the same seller — verify an account flag is set.

**Acceptance Scenarios**:

1. **Given** a seller has 3 upheld misrepresentation disputes within a rolling 90-day window, **When** the 3rd is resolved, **Then** the seller's account is flagged `UnderReview` for manual moderation action (e.g., listing suspension) — the specific moderation action itself is out of scope for this phase.

---

### Edge Cases

- What is the minimum photo count, and can it vary by club category? (Default: 2 photos, uniform across categories in this phase.)
- What happens if a seller's wallet balance is already below the hold amount when a dispute opens (they already spent the tokens elsewhere)? → the hold MUST be allowed to make the balance go negative for hold-accounting purposes only; actual spend-blocking still enforces `Balance - ActiveHolds >= 0` for new spends (see FR-006).
- Can a buyer dispute an order after some arbitrary time window, or is there a cutoff? (Default: 30 days from order completion.)
- What happens to a `PendingVerification` listing that is never resubmitted? (Default: auto-expires and is removed after 14 days — no token award, no clawback since none was ever issued.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CatalogItem` MUST be extended with a `VerificationStatus` (enum: `PendingVerification` | `Verified`) field. Listings MUST default to `PendingVerification` on creation.
- **FR-002**: `Catalog.API` MUST require at least 2 photos attached to a listing before it can transition to `Verified`; listings below this threshold remain `PendingVerification` and are excluded from public catalog queries.
- **FR-003**: Only `Verified` listings MUST trigger `ClubListingVerifiedIntegrationEvent` (spec 004) and appear in public catalog/search results.
- **FR-004**: A new `TradeDispute` entity MUST record `Id`, `OrderId`, `CatalogItemId`, `SellerId`, `RaisedByUserId`, `Reason` (text), `Status` (enum: `Open` | `Upheld` | `Rejected`), `CreatedAt`, `ResolvedAt`.
- **FR-005**: A new authenticated endpoint `POST /api/disputes` MUST allow the buyer on a completed order to open exactly one dispute per order, within 30 days of order completion.
- **FR-006**: A new `WalletHold` entity MUST record `Id`, `UserId`, `Amount`, `DisputeId`, `Status` (enum: `Active` | `Released` | `Converted`). Active holds MUST reduce a user's spendable balance (`Balance - SUM(active hold amounts)`) without altering `Balance` itself.
- **FR-007**: Opening a dispute on a token-paid order MUST create an `Active` `WalletHold` on the seller's wallet equal to the token amount originally awarded for that listing.
- **FR-008**: Opening a dispute on a cash-paid order MUST set the order's `DisputeOpen` flag, blocking any standard refund/return flow until resolved.
- **FR-009**: A new authenticated (moderator-role) endpoint `POST /api/disputes/{id}/resolve` MUST accept an outcome (`Upheld` | `Rejected`) and:
  - On `Upheld`: convert the `WalletHold` to a `Converted` status, create a `TokenTransaction` debiting the seller for the held amount (reason `misrepresentation_clawback`), and refund the buyer (token credit if token-paid, or emit a cash-refund request to `PaymentProcessor` if cash-paid).
  - On `Rejected`: set the `WalletHold` to `Released`, with no balance change.
- **FR-010**: A seller with 3 or more `Upheld` disputes within a rolling 90-day window MUST be flagged `UnderReview` on their account record.
- **FR-011**: A `PendingVerification` listing with no photo updates for 14 days MUST be automatically expired (removed from active listings, no token award).

### Key Entities

- **CatalogItem** (extended): Adds `VerificationStatus` (enum). Existing fields unchanged.
- **TradeDispute**: `Id`, `OrderId`, `CatalogItemId`, `SellerId`, `RaisedByUserId`, `Reason`, `Status`, `CreatedAt`, `ResolvedAt`.
- **WalletHold**: `Id`, `UserId`, `Amount`, `DisputeId`, `Status`.
- **SellerAccountFlag**: `SellerId`, `Flag` (enum, e.g., `UnderReview`), `SetAt`, `Reason`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of listings that reach `Verified` status have at least 2 attached photos at the time of transition.
- **SC-002**: No token award (`ClubListingVerifiedIntegrationEvent`) is ever published for a `PendingVerification` listing.
- **SC-003**: Opening a dispute on a token-paid order places a hold within the same request cycle — no window where the seller could spend the held tokens.
- **SC-004**: Every `Upheld` dispute results in exactly one clawback `TokenTransaction` and exactly one buyer refund action — verified by integration test for both `Tokens` and `Cash` payment methods.
- **SC-005**: A seller's spendable balance (`Balance - active holds`) never goes negative as a result of a new spend request, even while holds are active.

## Assumptions

- Moderator-role resolution (`POST /api/disputes/{id}/resolve`) is manual in this phase; automated dispute resolution or ML-based evidence review is out of scope.
- Photo storage/CDN integration is a placeholder per spec 006's assumptions — this spec only requires a photo *count* check, not image quality/content analysis.
- The minimum photo count (2) and dispute window (30 days) are configurable defaults, not hardcoded, but ship with these values in Phase 1.
- This spec depends on spec 002 (listing model) and spec 004 (token wallet, for the `WalletHold`/clawback mechanics); spec 004 depends back on this spec's `ClubListingVerifiedIntegrationEvent` trigger definition — these two specs MUST ship together or with 007 first.
