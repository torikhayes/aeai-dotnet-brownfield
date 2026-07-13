# Tasks: Seller Club Listings

**Input**: Design documents from `/specs/002-seller-club-listings/`
**Prerequisites**: plan.md ✅, spec.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`–`[US4]`)
- Include exact file paths in each task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm auth patterns and migration workflow before any implementation.

- [X] T001 Audit JWT `sub` claim extraction pattern in `src/Ordering.API/` or `src/Basket.API/` endpoints — document the pattern to reuse in `src/Catalog.API/Apis/CatalogApi.cs`.
- [X] T002 Confirm EF Core migration generation command for `src/Catalog.API/` and verify migrations apply correctly via Aspire on startup (`src/Catalog.API/Infrastructure/CatalogContext.cs`).
- [X] T003 [P] Confirm existing photo-serving mechanism in `src/Catalog.API/` (how `Pics/` images are served) to inform `PhotoUrls` field design.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema migration and model extension required before any user story endpoint can be built.

⚠️ **CRITICAL**: No endpoint work can begin until T004–T007 are complete.

- [X] T004 Extend `src/Catalog.API/Model/CatalogItem.cs` — add `SellerId` (string, nullable), `Condition` (string, values: New/Excellent/Good/Fair — **not** LikeNew), `ManufactureYear` (int, nullable), `PhotoUrls` (string, comma-separated or JSON array).
- [X] T005 Create EF Core migration in `src/Catalog.API/Infrastructure/Migrations/` for the new `CatalogItem` columns (`SellerId`, `Condition`, `ManufactureYear`, `PhotoUrls`).
- [X] T006 Update `src/Catalog.API/Infrastructure/CatalogContext.cs` if any fluent config is needed for new columns (nullable, max-length, etc.).
- [X] T007 Verify Aspire startup applies migration cleanly and existing seed data (`src/Catalog.API/Setup/catalog.json`) still loads with `SellerId = null` for admin-seeded items.

**Checkpoint**: Schema is live — all user story endpoint work can proceed.

---

## Phase 3: User Story 1 — List a Club for Sale (Priority: P1) 🎯 MVP

**Goal**: Authenticated user can POST a new club listing; it appears in the public catalog with their `SellerId`.

**Independent Test**: Authenticate as a test user, POST a valid club listing to `POST /api/catalog/items`, then `GET /api/catalog/items` — the new item appears with correct `SellerId` and `AvailableStock = 1`.

### Tests for User Story 1

- [ ] T008 [P] [US1] Write functional test in `tests/Catalog.FunctionalTests/` for `POST /api/catalog/items`: authenticated creates listing with `SellerId` from JWT, `AvailableStock = 1`. **Run test — confirm it FAILS before T012.**
- [ ] T009 [P] [US1] Write functional test for `POST /api/catalog/items`: unauthenticated returns HTTP 401.
- [ ] T010 [P] [US1] Write functional test for `POST /api/catalog/items`: missing required fields (name, price, type) returns HTTP 400.
- [ ] T011 [P] [US1] Write functional test for `POST /api/catalog/items`: submission with no photos returns HTTP 400 (Principle IV).

### Implementation for User Story 1

- [ ] T012 [US1] Add `POST /api/catalog/items` endpoint to `src/Catalog.API/Apis/CatalogApi.cs` — requires auth; extracts `SellerId` from JWT `sub` claim; sets `AvailableStock = 1`; validates at least one photo URL is provided; validates required fields.
- [X] T013 [US1] Add request DTO `CreateCatalogItemRequest` to `src/Catalog.API/Apis/CatalogApi.cs` or a new file — include: Name, Price, CatalogTypeId, CatalogBrandId, Condition (New/Excellent/Good/Fair), ManufactureYear (optional), Description (optional), PhotoUrls (required, at least one), Tags (optional).

**Checkpoint**: User Story 1 is independently testable.

---

## Phase 4: User Story 2 — Browse Clubs by Seller (Priority: P2)

**Goal**: Public endpoint returns all clubs listed by a given seller, paginated.

**Independent Test**: Call `GET /api/catalog/items/by-seller/{sellerId}` — returns only that seller's active listings. No auth required.

### Tests for User Story 2

- [X] T014 [P] [US2] Write functional test in `tests/Catalog.FunctionalTests/` for `GET /api/catalog/items/by-seller/{sellerId}`: returns only items matching `SellerId`. **Run test — confirm it FAILS before T016.**
- [X] T015 [P] [US2] Write functional test: seller with no listings returns empty paginated result (not 404).

### Implementation for User Story 2

- [X] T016 [US2] Add `GET /api/catalog/items/by-seller/{sellerId}` endpoint to `src/Catalog.API/Apis/CatalogApi.cs` — public (no auth); paginated using existing `PaginationRequest` pattern; filters `CatalogItem` by `SellerId` and `AvailableStock > 0`.

**Checkpoint**: User Stories 1 and 2 both independently testable.

---

## Phase 5: User Story 3 — View My Own Listings (Priority: P2)

**Goal**: Authenticated user sees only their own listings.

**Independent Test**: Authenticated call to `GET /api/catalog/items/my-listings` returns items where `SellerId` matches caller's identity.

### Tests for User Story 3

- [X] T017 [P] [US3] Write functional test in `tests/Catalog.FunctionalTests/` for `GET /api/catalog/items/my-listings`: returns only caller's items. **Run test — confirm it FAILS before T019.**
- [X] T018 [P] [US3] Write functional test: items belonging to another seller do not appear in caller's my-listings response.

### Implementation for User Story 3

- [X] T019 [US3] Add `GET /api/catalog/items/my-listings` endpoint to `src/Catalog.API/Apis/CatalogApi.cs` — requires auth; extracts `SellerId` from JWT `sub`; returns paginated items where `SellerId` matches caller.

**Checkpoint**: User Stories 1, 2, and 3 all independently testable.

---

## Phase 6: User Story 4 — Remove a Listing (Priority: P3)

**Goal**: Seller can deactivate their own listing; it no longer appears in public catalog.

**Independent Test**: Authenticated DELETE/PATCH sets `AvailableStock = 0` — item excluded from default catalog queries.

### Tests for User Story 4

- [X] T020 [P] [US4] Write functional test in `tests/Catalog.FunctionalTests/` for deactivate endpoint: owner can deactivate their listing; `AvailableStock` becomes 0. **Run test — confirm it FAILS before T022.**
- [X] T021 [P] [US4] Write functional test: non-owner attempting to deactivate returns HTTP 403.

### Implementation for User Story 4

- [ ] T022 [US4] Add `DELETE /api/catalog/items/{id}` (or `PATCH`) endpoint to `src/Catalog.API/Apis/CatalogApi.cs` — requires auth; checks `SellerId` matches caller's JWT `sub` (returns 403 if not owner and not admin); sets `AvailableStock = 0`.

**Checkpoint**: All 4 user stories independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T023 [P] Run full `tests/Catalog.FunctionalTests/` suite — confirm all existing tests still pass after migration and new endpoints (SC-005).
- [ ] T024 [P] Verify admin-seeded items (`SellerId = null`) are unaffected by the new endpoints — admin creation endpoint (`src/Catalog.API/Apis/CatalogApi.cs`) still works.
- [ ] T025 Update `specs/002-seller-club-listings/quickstart.md` with step-by-step validation of the full seller listing flow.
