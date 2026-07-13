# Feature Specification: Seller Club Listings

**Feature Branch**: `002-seller-club-listings`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - List a Club for Sale (Priority: P1)

An authenticated user navigates to "Sell My Club", fills in details (club type, brand, condition, year, price, description), and submits a listing. The club appears in the public catalog immediately.

**Why this priority**: This is the core supply-side flow of the marketplace — without it, no clubs exist to buy.

**Independent Test**: Log in, submit a club listing via the API, then fetch the catalog — the new item appears with the correct `SellerId` and details.

**Acceptance Scenarios**:

1. **Given** a user is authenticated, **When** they POST a valid club listing to `POST /api/catalog/items`, **Then** the item is created in the catalog with `SellerId` set to their user ID and `AvailableStock` of 1.
2. **Given** a user is not authenticated, **When** they attempt to POST a club listing, **Then** the API returns HTTP 401.
3. **Given** a user submits a listing with missing required fields (name, price, type), **When** the request is processed, **Then** the API returns HTTP 400 with validation errors.

---

### User Story 2 - Browse Clubs by Seller (Priority: P2)

A user views the profile/listings page of a specific seller and sees all clubs that seller has listed.

**Why this priority**: Enables trust and repeat purchases from the same seller.

**Independent Test**: Call `GET /api/catalog/items/by-seller/{sellerId}` — returns only that seller's active listings.

**Acceptance Scenarios**:

1. **Given** seller A has listed 3 clubs, **When** a user requests seller A's listings, **Then** exactly 3 items are returned.
2. **Given** a seller has no listings, **When** their listings are requested, **Then** an empty paginated result is returned (not a 404).

---

### User Story 3 - View My Own Listings (Priority: P2)

An authenticated user can view and manage their own active listings.

**Why this priority**: Sellers need to see what they have listed to manage their inventory.

**Independent Test**: Authenticated call to `GET /api/catalog/items/my-listings` returns only items where `SellerId` matches the caller's identity.

**Acceptance Scenarios**:

1. **Given** a seller has 2 active listings, **When** they call the my-listings endpoint, **Then** only their 2 items are returned.
2. **Given** another seller's items exist, **When** user A calls my-listings, **Then** user B's items do not appear.

---

### User Story 4 - Remove a Listing (Priority: P3)

A seller can delist a club (mark it unavailable) before it sells.

**Why this priority**: Sellers need the ability to withdraw a listing if circumstances change.

**Independent Test**: Authenticated DELETE or PATCH to mark an item as unavailable — item no longer appears in public catalog searches.

**Acceptance Scenarios**:

1. **Given** a seller owns listing X, **When** they delete or deactivate it, **Then** `AvailableStock` is set to 0 and the item is excluded from default catalog queries.
2. **Given** seller A owns listing X, **When** seller B attempts to delete listing X, **Then** the API returns HTTP 403.

---

### Edge Cases

- What happens if a user lists a club and then their account is deleted?
- Can a seller re-list a club that was previously sold (i.e., same club, new listing)?
- What is the maximum number of active listings per seller?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CatalogItem` MUST be extended with `SellerId` (string, nullable for legacy/admin-created items), `Condition` (enum: New, Excellent, Good, Fair — the canonical condition grades defined in the project constitution, Principle III), and `ManufactureYear` (int, nullable).
- **FR-002**: An EF Core migration MUST be created for the new `CatalogItem` columns.
- **FR-003**: A new authenticated endpoint `POST /api/catalog/items` MUST allow any logged-in user to create a listing; `SellerId` is set server-side from the authenticated user's identity claim.
- **FR-004**: A new endpoint `GET /api/catalog/items/by-seller/{sellerId}` MUST return paginated listings for a given seller (public, no auth required).
- **FR-005**: A new authenticated endpoint `GET /api/catalog/items/my-listings` MUST return only the caller's own listings.
- **FR-006**: `AvailableStock` for seller-created listings MUST be set to `1` automatically on creation.
- **FR-007**: Only the owning seller (or an admin) MUST be permitted to delete or deactivate their own listing.
- **FR-008**: The existing admin item-creation endpoint MUST remain functional for seeded/admin catalog entries.

### Key Entities

- **CatalogItem** (extended): Adds `SellerId` (string), `Condition` (string/enum: New/Excellent/Good/Fair), `ManufactureYear` (int?). All existing fields unchanged.
- **Seller identity**: Represented by the user's subject claim from Identity.API JWT — no new entity required.

**Note**: `Condition` grades captured here are used downstream by the token valuation lookup table
(spec 004) and are subject to the photo-evidence and automated-verification requirements defined
in spec 007 (Trust & Safety). This spec covers listing CRUD only — it does not itself gate listing
creation on photo evidence or trigger token awards.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A seller can list a club in under 60 seconds via the UI.
- **SC-002**: Listed clubs appear in the public catalog within one page refresh (no async delay).
- **SC-003**: Seller-filtered queries return correct results with 100% accuracy.
- **SC-004**: Unauthorized listing attempts are rejected 100% of the time.
- **SC-005**: Existing catalog functional tests continue to pass after the migration.

## Assumptions

- Seller identity is the `sub` claim from the JWT issued by Identity.API; no new user profile storage is needed.
- `AvailableStock` of 1 means exactly one unit (the club) is for sale; the stock decrement logic already built into Catalog.API handles the sold state.
- Listing images are uploaded via the existing image handling mechanism; new image upload infrastructure is out of scope for this phase.
- Admin-seeded catalog items have `SellerId = null`, indicating platform inventory (not individual seller items).
- A seller can have multiple active listings simultaneously; no per-seller listing cap is enforced in this phase.
