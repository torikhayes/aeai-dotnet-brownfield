# Contract: Catalog API Domain Rebrand (Data-Only)

## Contract type
Behavioral contract for existing REST endpoints in `Catalog.API`.

## Compatibility statement
- No endpoint additions/removals.
- No schema changes.
- No request/response shape changes.
- No versioning changes.

## Endpoints in scope
- `GET /api/catalog/items`
- `GET /api/catalog/items/{id}`
- `GET /api/catalog/catalogtypes`
- `GET /api/catalog/catalogbrands`
- Existing image endpoint contract remains unchanged (`/api/catalog/items/{id}/pic`).

## Required behavioral assertions

### 1. Catalog type vocabulary
`GET /api/catalog/catalogtypes` must return only golf club categories:
- Driver
- Iron Set
- Wedge
- Putter
- Hybrid
- Fairway Wood

### 2. Catalog brand vocabulary
`GET /api/catalog/catalogbrands` must return only golf manufacturers:
- Callaway
- TaylorMade
- Ping
- Titleist
- Cobra

### 3. Item domain integrity
Responses from `GET /api/catalog/items` and `GET /api/catalog/items/{id}` must satisfy:
- Every item references a valid golf `CatalogType` and `CatalogBrand`.
- Item names/descriptions use golf terminology.
- No AdventureWorks product names/brands are present.

### 4. Filtering compatibility
Existing type/brand filtering behavior must remain unchanged in semantics:
- Type-only filters return only matching club type results.
- Combined type + brand filters return correctly narrowed results.

### 5. Image availability contract
For seeded golf items:
- `GET /api/catalog/items/{id}/pic` resolves to an image for each listed item.
- Placeholder images are valid as long as they are golf-labeled and mapped by existing filename conventions.
