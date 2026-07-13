# Feature Specification: Club Scoring & Ratings

**Feature Branch**: `003-club-scoring-ratings`
**Created**: 2026-07-13
**Status**: Draft

**Note**: This spec was revised to remove the `TokenPotentialScore` engagement-based formula that
previously appeared here. Token rewards are governed exclusively by the category × condition
lookup table defined in the constitution (Principle III) and implemented in spec 004 (Token
Wallet Service) — they are NOT derived from views, favorites, or ratings. Ratings, views, and
favorites remain in this spec purely as trust/discovery signals for buyers.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rate a Club (Priority: P1)

A buyer who has viewed a club listing submits a star rating (1–5). The rating is recorded and the club's average rating updates immediately.

**Why this priority**: Ratings are a core trust signal that helps buyers evaluate listings before purchasing.

**Independent Test**: Submit a rating via `POST /api/catalog/items/{id}/rate`, then GET the item — `AverageRating` and `RatingCount` are updated.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they submit a 5-star rating for a club, **Then** the club's `AverageRating` increases and `RatingCount` increments by 1.
2. **Given** a user has already rated a club, **When** they submit a new rating for the same club, **Then** their previous rating is updated (not duplicated).
3. **Given** an unauthenticated user, **When** they attempt to submit a rating, **Then** the API returns HTTP 401.
4. **Given** an invalid rating value (e.g., 0 or 6), **When** submitted, **Then** the API returns HTTP 400.

---

### User Story 2 - View Demand Signals on a Club (Priority: P2)

A seller views their listing and sees how many times it has been viewed and favorited, giving them a sense of buyer interest.

**Why this priority**: Demand signals (views, favorites) help sellers understand buyer interest and improve their listings, but — per the constitution — they have no bearing on token rewards.

**Independent Test**: View a club detail page (triggering a view increment), then GET the item — `ViewCount` has increased.

**Acceptance Scenarios**:

1. **Given** a club listing is fetched via `GET /api/catalog/items/{id}`, **When** the request completes, **Then** `ViewCount` increments by 1.
2. **Given** an authenticated user, **When** they toggle a favorite on a club, **Then** `FavoriteCount` increases or decreases accordingly.
3. **Given** a club has been favorited by 10 users, **When** the item is fetched, **Then** `FavoriteCount` is 10.

---

### User Story 3 - Tag a Club (Priority: P3)

A seller can add descriptive tags to their listing (e.g., "left-handed", "graphite-shaft", "tour-issue") to improve discoverability.

**Why this priority**: Tags improve search and filtering.

**Independent Test**: Create a listing with tags, then search by tag — only tagged items appear.

**Acceptance Scenarios**:

1. **Given** a listing has tags ["graphite-shaft", "left-handed"], **When** a user searches by tag "left-handed", **Then** the listing is returned.
2. **Given** a seller updates tags on their listing, **When** the listing is fetched, **Then** the new tags are reflected.

---

### Edge Cases

- Is view counting de-duplicated (e.g., per-session, per-user, or raw)?
- What is the maximum number of tags per listing?
- What happens to `AverageRating` when the last rating on an item is effectively superseded by a user changing their score?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CatalogItem` MUST be extended with `ViewCount` (int), `FavoriteCount` (int), `AverageRating` (float), `RatingCount` (int), and `Tags` (string, comma-separated). No token-related field is added by this spec.
- **FR-002**: A new `CatalogItemRating` entity MUST be created with fields: `Id`, `CatalogItemId`, `UserId`, `Stars` (1–5), `CreatedAt`. One rating per user per item (upsert semantics).
- **FR-003**: An EF Core migration MUST be created for the schema changes.
- **FR-004**: `POST /api/catalog/items/{id}/rate` MUST accept a star rating from an authenticated user and update `AverageRating` and `RatingCount` on the parent item.
- **FR-005**: `GET /api/catalog/items/{id}` MUST increment `ViewCount` on each call.
- **FR-006**: `POST /api/catalog/items/{id}/favorite` MUST toggle the authenticated user's favorite on a club and update `FavoriteCount`.
- **FR-007**: `PATCH /api/catalog/items/{id}/tags` MUST allow the owning seller to update tags on their listing.

### Key Entities

- **CatalogItem** (extended): Adds `ViewCount`, `FavoriteCount`, `AverageRating`, `RatingCount`, `Tags`. All existing fields unchanged. Does not carry any token-value field — see spec 004 for `TokenPrice`.
- **CatalogItemRating**: New entity in `catalogdb`. Tracks one rating per user per item. Used to compute and update `AverageRating`.
- **CatalogItemFavorite**: New entity in `catalogdb`. Tracks which users have favorited which items. Used to maintain `FavoriteCount`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `AverageRating` is accurate to 1 decimal place after any number of rating submissions.
- **SC-002**: View count increments are recorded for 100% of catalog item GET requests.
- **SC-003**: Tag-based search returns correct results with no false positives.
- **SC-004**: All existing catalog API tests continue to pass after the migration.

## Assumptions

- View counting is raw (not de-duplicated per user/session) in Phase 1; de-duplication is a future enhancement.
- Ratings are only submitted by authenticated users; anonymous ratings are not supported.
- Tags are free-text strings; a controlled vocabulary or tag taxonomy is out of scope for this phase.
- `CatalogItemFavorite` tracks favorites per user for correct `FavoriteCount`; a separate "my favorites" browsing feature is out of scope for this phase.
- Ratings/views/favorites are informational trust and discovery signals only. Per the project constitution (Principle III), they MUST NOT feed into token valuation — token rewards are computed solely from club category and condition grade (spec 004).
