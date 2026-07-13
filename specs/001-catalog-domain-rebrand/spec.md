# Feature Specification: Catalog Domain Rebrand

**Feature Branch**: `001-catalog-domain-rebrand`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse Golf Club Catalog (Priority: P1)

A visitor to the marketplace opens the storefront and sees golf clubs instead of generic AdventureWorks products. Club types (Driver, Iron Set, Wedge, Putter, Hybrid, Fairway Wood) and brands (Callaway, TaylorMade, Ping, Titleist, Cobra) are correctly displayed throughout the UI.

**Why this priority**: The entire marketplace experience depends on correct domain terminology. Without this, every other feature ships with wrong labels.

**Independent Test**: Open the storefront, navigate the catalog — all visible product categories, brand filters, and item names refer to golf clubs and manufacturers.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** a user opens the catalog page, **Then** all visible product type labels are golf club types (e.g., "Driver", "Putter") and no AdventureWorks product names appear.
2. **Given** the catalog filter panel is open, **When** a user browses by brand, **Then** only golf club manufacturers are listed.
3. **Given** the catalog seed data has been applied, **When** the Catalog API returns items, **Then** each item has a valid `CatalogType` and `CatalogBrand` corresponding to golf club domain values.

---

### User Story 2 - Search for Clubs by Type (Priority: P2)

A user filters the catalog by club type (e.g., "Wedge") and sees only wedge listings.

**Why this priority**: Type-based filtering is the primary navigation pattern and must work with the new taxonomy.

**Independent Test**: Use the type filter in the storefront — only clubs of the selected type appear.

**Acceptance Scenarios**:

1. **Given** catalog is seeded with golf data, **When** a user selects the "Putter" type filter, **Then** only putters are shown in results.
2. **Given** multiple brands exist, **When** a user combines brand and type filters, **Then** results are narrowed correctly.

---

### Edge Cases

- What happens if the old seed data is partially present alongside new golf data?
- How does the system handle missing product images during the transition?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The catalog seed data (`Setup/catalog.json`) MUST be replaced with golf club items, types, and brands.
- **FR-002**: `CatalogType` values MUST include: Driver, Iron Set, Wedge, Putter, Hybrid, Fairway Wood.
- **FR-003**: `CatalogBrand` values MUST include: Callaway, TaylorMade, Ping, Titleist, Cobra.
- **FR-004**: Product images in `Pics/` MUST be replaced with golf club images (or placeholder images clearly labeled as golf clubs).
- **FR-005**: All existing catalog API endpoints MUST continue to function without schema changes.
- **FR-006**: No EF Core migrations are required — this is a data-only change applied via the existing seed mechanism.

### Key Entities

- **CatalogItem**: Existing entity — name, description, price, brand, type. Seed data replaced; schema unchanged.
- **CatalogBrand**: Existing entity — golf manufacturer names replace AdventureWorks brands.
- **CatalogType**: Existing entity — golf club categories replace AdventureWorks product types.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero AdventureWorks product names or brand names appear in the storefront after seed data is applied.
- **SC-002**: All 6 club types and all 5 brands are present in the catalog database after a clean startup.
- **SC-003**: All existing catalog API endpoint tests pass without modification.
- **SC-004**: Catalog page loads successfully with golf club data on first run (migrations + seed applied automatically by Aspire).

## Assumptions

- Product images are placeholders for Phase 1; real photography is out of scope for this phase.
- Pricing values in seed data are representative but not final business prices.
- No changes to `AvailableStock`, `RestockThreshold`, or `MaxStockThreshold` logic — stock management for individually-owned clubs is addressed in Phase 2 (seller listings).
- Existing functional tests that assert on AdventureWorks product names will need seed-data-aware updates but no structural test changes.
