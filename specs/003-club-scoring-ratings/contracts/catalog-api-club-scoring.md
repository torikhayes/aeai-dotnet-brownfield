# Contract: Catalog API Club Scoring

## Contract type
Behavioral contract for catalog scoring, favorites, tags, and item-read counters.

## Compatibility statement
- Existing catalog endpoints remain in place.
- `GET /api/catalog/items/{id}` gains a side effect: it increments `ViewCount` on each successful call.
- Item payloads gain `ViewCount`, `FavoriteCount`, `AverageRating`, `RatingCount`, and `Tags`.
- No token-related fields or token-calculation behavior are introduced.

## Endpoints in scope
- `GET /api/catalog/items/{id}`
- `GET /api/catalog/items?tag={tag}` or equivalent tag-aware filter on the existing list surface
- `POST /api/catalog/items/{id}/rate`
- `POST /api/catalog/items/{id}/favorite`
- `PATCH /api/catalog/items/{id}/tags`

## Required behavioral assertions

### 1. Item read counter
`GET /api/catalog/items/{id}` must:
- return `404` when the item does not exist
- increment `ViewCount` exactly once for each successful request
- include the updated counter fields in the response payload

### 2. Rating submission
`POST /api/catalog/items/{id}/rate` must:
- require authentication
- reject invalid star values outside `1..5` with `400`
- upsert the caller's rating for the item
- update `AverageRating` and `RatingCount` on the parent item immediately

### 3. Favorite toggle
`POST /api/catalog/items/{id}/favorite` must:
- require authentication
- toggle the caller's favorite state for the item
- update `FavoriteCount` on the parent item immediately

### 4. Tag update
`PATCH /api/catalog/items/{id}/tags` must:
- require the authenticated seller who owns the listing
- accept a tag list payload and persist it as the item's normalized comma-separated `Tags` value
- expose the updated tags on subsequent GET responses

### 5. Tag discovery
Tag-aware list/search requests must:
- match the persisted tag tokens, not free-text substrings inside arbitrary item text
- return only items whose stored tag list contains the requested token

## Response expectations
- Rating, favorite, and tag update endpoints may return `204 No Content` after a successful mutation.
- `GET` responses continue to use the existing catalog item shape, with the new scoring fields added.