# Data Model: Club Scoring & Ratings

## CatalogItem

Existing entity extended with:
- `ViewCount` (`int`): raw read counter, increments on every successful item GET.
- `FavoriteCount` (`int`): number of active favorites across users.
- `AverageRating` (`float`): current average of all stored ratings, rounded for presentation to one decimal place.
- `RatingCount` (`int`): total number of active rating rows for the item.
- `Tags` (`string`): comma-separated list of normalized tags.

Validation and rules:
- Existing item fields remain unchanged.
- `ViewCount`, `FavoriteCount`, and `RatingCount` must never be negative.
- `Tags` is optional; when present it is stored as trimmed, normalized tokens joined by commas.

Relationships:
- One `CatalogItem` has many `CatalogItemRating` rows.
- One `CatalogItem` has many `CatalogItemFavorite` rows.

## CatalogItemRating

New entity used to track per-user ratings.

Fields:
- `Id` (`int` or service-standard surrogate key)
- `CatalogItemId` (`int`)
- `UserId` (`string`)
- `Stars` (`int`)
- `CreatedAt` (`DateTimeOffset`)

Validation and rules:
- `Stars` must be between 1 and 5 inclusive.
- Only one active rating exists per `(CatalogItemId, UserId)` pair.
- If the same user rates the same item again, the existing row is updated instead of duplicated.

Relationships:
- Many-to-one to `CatalogItem`.

State transitions:
- `Create`: insert a new rating row, increment `RatingCount`, recompute `AverageRating`.
- `Update`: replace `Stars` on the existing row, keep `RatingCount` unchanged, recompute `AverageRating`.

## CatalogItemFavorite

New entity used to track per-user favorites.

Fields:
- `Id` (`int` or service-standard surrogate key)
- `CatalogItemId` (`int`)
- `UserId` (`string`)
- `CreatedAt` (`DateTimeOffset`)

Validation and rules:
- Only one active favorite exists per `(CatalogItemId, UserId)` pair.
- Favorite toggle semantics add a row when absent and remove the row when present.

Relationships:
- Many-to-one to `CatalogItem`.

State transitions:
- `Toggle on`: insert a favorite row, increment `FavoriteCount`.
- `Toggle off`: delete the favorite row, decrement `FavoriteCount`.

## Query / Read Model Behavior

- `GET /api/catalog/items/{id}` increments `ViewCount` before returning the item.
- Existing list/search endpoints should surface the new item fields without changing their existing pagination semantics.
- Tag discovery uses the persisted `Tags` field through the existing catalog query surface.