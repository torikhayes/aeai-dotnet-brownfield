# Feature Specification: Club Scoring & Ratings

**Feature Branch**: `003-club-scoring-ratings`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rate a Club (Priority: P1)

A buyer who has viewed a club listing submits a star rating (1–5). The rating is recorded and the club's average rating updates immediately.

**Why this priority**: Ratings are the foundation of the scoring system — without them, the token potential score cannot be computed.

**Independent Test**: Submit a rating via `POST /api/catalog/items/{id}/rate`, then GET the item — `AverageRating` and `RatingCount` are updated.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they submit a 5-star rating for a club, **Then** the club's `AverageRating` increases and `RatingCount` increments by 1.
2. **Given** a user has already rated a club, **When** they submit a new rating for the same club, **Then** their previous rating is updated (not duplicated).
3. **Given** an unauthenticated user, **When** they attempt to submit a rating, **Then** the API returns HTTP 401.
4. **Given** an invalid rating value (e.g., 0 or 6), **When** submitted, **Then** the API returns HTTP 400.

---

### User Story 2 - View Demand Signals on a Club (Priority: P1)

A seller views their listing and sees how many times it has been viewed and favorited, giving them a sense of demand.

**Why this priority**: Demand signals (views, favorites) are direct inputs to the token potential score and motivate sellers.

**Independent Test**: View a club detail page (triggering a view increment), then GET the item — `ViewCount` has increased.

**Acceptance Scenarios**:

1. **Given** a club listing is fetched via `GET /api/catalog/items/{id}`, **When** the request completes, **Then** `ViewCount` increments by 1.
2. **Given** an authenticated user, **When** they toggle a favorite on a club, **Then** `FavoriteCount` increases or decreases accordingly.
3. **Given** a club has been favorited by 10 users, **When** the item is fetched, **Then** `FavoriteCount` is 10.

---

### User Story 3 - View Token Potential Score on a Listing (Priority: P2)

A seller can see the computed `TokenPotentialScore` on their listing, which indicates how many tokens they stand to earn when the club sells.

**Why this priority**: Sellers need visibility into the scoring formula to understand how to maximize their token earnings.

**Independent Test**: GET a club item — the `TokenPotentialScore` field is present and is a positive number that changes as ratings/demand update.

**Acceptance Scenarios**:

1. **Given** a club with 100 views, 20 favorites, and a 4.5 average rating, **When** the item is fetched, **Then** `TokenPotentialScore` reflects a weighted combination of those signals.
2. **Given** a new listing with no ratings or views, **When** fetched, **Then** `TokenPotentialScore` is 0 or a defined minimum.
3. **Given** a club's view count increases, **When** the score is recalculated, **Then** `TokenPotentialScore` increases.

---

### User Story 4 - Tag a Club (Priority: P3)

A seller can add descriptive tags to their listing (e.g., "left-handed", "graphite-shaft", "tour-issue") to improve discoverability.

**Why this priority**: Tags improve search and filtering; they also contribute to the scoring formula as a richness signal.

**Independent Test**: Create a listing with tags, then search by tag — only tagged items appear.

**Acceptance Scenarios**:

1. **Given** a listing has tags ["graphite-shaft", "left-handed"], **When** a user searches by tag "left-handed", **Then** the listing is returned.
2. **Given** a seller updates tags on their listing, **When** the listing is fetched, **Then** the new tags are reflected.

---

### Edge Cases

- What happens to `TokenPotentialScore` when a rating is deleted or updated?
- Is view counting de-duplicated (e.g., per-session, per-user, or raw)?
- What is the maximum number of tags per listing?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CatalogItem` MUST be extended with `ViewCount` (int), `FavoriteCount` (int), `AverageRating` (float), `RatingCount` (int), `Tags` (string, comma-separated), and `TokenPotentialScore` (float).
- **FR-002**: A new `CatalogItemRating` entity MUST be created with fields: `Id`, `CatalogItemId`, `UserId`, `Stars` (1–5), `CreatedAt`. One rating per user per item (upsert semantics).
- **FR-003**: An EF Core migration MUST be created for both schema changes.
- **FR-004**: `POST /api/catalog/items/{id}/rate` MUST accept a star rating from an authenticated user and update `AverageRating` and `RatingCount` on the parent item.
- **FR-005**: `GET /api/catalog/items/{id}` MUST increment `ViewCount` on each call.
- **FR-006**: `POST /api/catalog/items/{id}/favorite` MUST toggle the authenticated user's favorite on a club and update `FavoriteCount`.
- **FR-007**: `TokenPotentialScore` MUST be recomputed and persisted whenever `ViewCount`, `FavoriteCount`, `AverageRating`, or `RatingCount` changes.
- **FR-008**: The scoring formula is: `score = (views × 0.1) + (favorites × 0.5) + (averageRating × ratingCount × 2.0)`. Weights are configurable via app settings.
- **FR-009**: `PATCH /api/catalog/items/{id}/tags` MUST allow the owning seller to update tags on their listing.

### Key Entities

- **CatalogItem** (extended): Adds demand/scoring fields. All existing fields unchanged.
- **CatalogItemRating**: New entity in `catalogdb`. Tracks one rating per user per item. Used to compute and update `AverageRating`.
- **CatalogItemFavorite**: New entity in `catalogdb`. Tracks which users have favorited which items. Used to maintain `FavoriteCount`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `AverageRating` is accurate to 1 decimal place after any number of rating submissions.
- **SC-002**: `TokenPotentialScore` updates within the same API response cycle that triggers the change (no eventual consistency lag in Phase 1).
- **SC-003**: View count increments are recorded for 100% of catalog item GET requests.
- **SC-004**: Tag-based search returns correct results with no false positives.
- **SC-005**: All existing catalog API tests continue to pass after the migration.

## Assumptions

- View counting is raw (not de-duplicated per user/session) in Phase 1; de-duplication is a future enhancement.
- Scoring formula weights are fixed defaults configurable via `appsettings.json`; a UI to adjust weights is out of scope.
- Ratings are only submitted by authenticated users; anonymous ratings are not supported.
- Tags are free-text strings; a controlled vocabulary or tag taxonomy is out of scope for this phase.
- `CatalogItemFavorite` tracks favorites per user for correct `FavoriteCount`; a separate "my favorites" browsing feature is out of scope for this phase.
