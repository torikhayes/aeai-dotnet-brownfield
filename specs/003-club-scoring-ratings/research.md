# Research: Club Scoring & Ratings

## Decisions

### 1. Keep scoring data inside `Catalog.API`
- Decision: Store ratings, favorites, and item-level aggregates in the existing catalog database and service boundary.
- Rationale: The repo already uses EF Core and `CatalogContext` for catalog persistence, so this keeps the implementation local and transactional.
- Alternatives considered: A separate scoring service, event-sourced read model, or storing aggregates in Redis. Those options add coupling or make consistency harder without solving a present need.

### 2. Model ratings as an upsert per user per item
- Decision: Add a `CatalogItemRating` table keyed logically by `(CatalogItemId, UserId)` and update the existing rating row when the same user submits a new score.
- Rationale: This matches the spec's one-rating-per-user semantics and keeps `AverageRating` and `RatingCount` correct when a user changes their mind.
- Alternatives considered: Append-only ratings or client-computed averages. Append-only records complicate updates; client-side aggregation is not trustworthy.

### 3. Maintain aggregates server-side after each mutation
- Decision: Recompute or update `AverageRating`, `RatingCount`, and `FavoriteCount` in the write path after the underlying row changes.
- Rationale: The contract requires the parent item to reflect the latest counts immediately and not depend on eventual background jobs.
- Alternatives considered: Derived views, periodic batch jobs, or on-read aggregation. Those are weaker for immediate consistency and functional testing.

### 4. Count views as raw reads
- Decision: Increment `ViewCount` on every successful `GET /api/catalog/items/{id}` call without de-duplication.
- Rationale: The spec explicitly states raw view counting for Phase 1, which keeps the implementation simple and deterministic.
- Alternatives considered: Per-user or per-session deduplication. Those require extra identity/session tracking and were intentionally deferred.

### 5. Store tags as a normalized comma-separated field
- Decision: Add `Tags` to `CatalogItem` as a comma-separated string and normalize seller input before saving.
- Rationale: This matches the feature requirement and avoids introducing a new join table or taxonomy service for a small metadata feature.
- Alternatives considered: A normalized tag table, JSON arrays, or a controlled vocabulary. Those are more flexible, but they are unnecessary for the current scope.

### 6. Reuse the existing catalog listing surface for tag discovery
- Decision: Add tag filtering to the current catalog item query surface rather than introducing a new search endpoint.
- Rationale: The repository already exposes list/filter endpoints in `CatalogApi`, so extending that surface is the smallest way to make tags discoverable.
- Alternatives considered: A brand-new search endpoint or UI-specific search service. Those would duplicate existing query behavior and expand the feature surface unnecessarily.

### 7. Use the authenticated user identity from claims
- Decision: Resolve `UserId` from the authenticated principal when recording ratings and favorites, and reject anonymous requests.
- Rationale: The feature needs stable per-user uniqueness and the spec requires HTTP 401 for unauthenticated rating attempts.
- Alternatives considered: Email address, username, or client-provided user IDs. Those are less stable and easier to spoof.