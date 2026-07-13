# Data Model: Catalog Domain Rebrand

## Scope
This feature changes seed values and static assets only. Existing schema and entity relationships remain unchanged.

## Entity: CatalogBrand (existing)
- Purpose: Manufacturer taxonomy used by catalog items and filters.
- Fields used:
  - `Id` (int, PK)
  - `Brand` (string, required)
- Domain set required by this feature:
  - `Callaway`
  - `TaylorMade`
  - `Ping`
  - `Titleist`
  - `Cobra`
- Validation rules:
  - Must be non-empty.
  - Seed source must not include AdventureWorks-era brand names.

## Entity: CatalogType (existing)
- Purpose: Club category taxonomy used by catalog items and filters.
- Fields used:
  - `Id` (int, PK)
  - `Type` (string, required)
- Domain set required by this feature:
  - `Driver`
  - `Iron Set`
  - `Wedge`
  - `Putter`
  - `Hybrid`
  - `Fairway Wood`
- Validation rules:
  - Must be non-empty.
  - Seed source must not include AdventureWorks-era type names.

## Entity: CatalogItem (existing)
- Purpose: Public listing document served by catalog endpoints.
- Fields in scope:
  - `Id` (int)
  - `Name` (string)
  - `Description` (string)
  - `Price` (decimal)
  - `CatalogBrandId` (FK -> CatalogBrand)
  - `CatalogTypeId` (FK -> CatalogType)
  - `PictureFileName` (string, `{Id}.webp`)
  - Existing stock fields remain unchanged by this feature.
- Validation rules:
  - Every seeded item maps to one valid golf brand and one valid golf type.
  - Item names/descriptions must use golf terminology.

## Relationships
- `CatalogBrand (1) -> (many) CatalogItem`
- `CatalogType (1) -> (many) CatalogItem`

## State/Transition Notes
- Startup seed transition:
  - If `CatalogItems` is empty, seed inserts the golf taxonomy and items from `Setup/catalog.json`.
  - If `CatalogItems` is not empty, seed does not apply; mixed old/new state is out of scope and treated as invalid rollout state for this feature.
