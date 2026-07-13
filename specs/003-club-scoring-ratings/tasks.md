# Tasks: Club Scoring & Ratings

**Input**: Design documents from `/specs/003-club-scoring-ratings/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included because the feature spec defines independent test criteria for each user story.

**Organization**: Tasks are grouped by user story to keep each increment independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other tasks that do not depend on incomplete work
- **[Story]**: `US1`, `US2`, `US3` for user story phases only
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the catalog domain model surface for scoring, favorites, and tags.

- [ ] T001 [P] Extend `src/Catalog.API/Model/CatalogItem.cs` with `ViewCount`, `FavoriteCount`, `AverageRating`, `RatingCount`, `Tags`, and tag-normalization helpers.
- [ ] T002 [P] Add `src/Catalog.API/Model/CatalogItemRating.cs` and `src/Catalog.API/Model/CatalogItemFavorite.cs` with the fields defined in `specs/003-club-scoring-ratings/data-model.md`.
- [ ] T003 Add EF Core entity configuration files in `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogItemRatingEntityTypeConfiguration.cs` and `src/Catalog.API/Infrastructure/EntityConfigurations/CatalogItemFavoriteEntityTypeConfiguration.cs`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire persistence, migration support, and authenticated request handling before any user story behavior is implemented.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [ ] T004 Update `src/Catalog.API/Infrastructure/CatalogContext.cs` to register `DbSet<CatalogItemRating>` and `DbSet<CatalogItemFavorite>` and apply the new entity configurations.
- [ ] T005 Create the EF Core migration and snapshot update under `src/Catalog.API/Infrastructure/Migrations/` for the new `CatalogItem` columns plus `CatalogItemRating` and `CatalogItemFavorite` tables.
- [ ] T006 [P] Add authorization wiring to `src/Catalog.API/Program.cs` and `src/Catalog.API/Extensions/Extensions.cs` so rating, favorite, and tag endpoints can require authenticated users.
- [ ] T007 [P] Add authenticated test support in `tests/Catalog.FunctionalTests/CatalogApiFixture.cs` and `tests/Catalog.FunctionalTests/TestAuthHandler.cs` so functional tests can exercise protected endpoints.

**Checkpoint**: Catalog persistence, migrations, and auth/test plumbing are ready for story work.

---

## Phase 3: User Story 1 - Rate a Club (Priority: P1) 🎯 MVP

**Goal**: An authenticated buyer can submit or update a star rating for a club listing and the item aggregates update immediately.

**Independent Test**: `POST /api/catalog/items/{id}/rate` as an authenticated user updates `AverageRating` and `RatingCount`, and a second rating from the same user replaces the first rating instead of duplicating it.

### Tests for User Story 1

- [ ] T008 [P] [US1] Add functional coverage for authenticated rating submission and invalid star values in `tests/Catalog.FunctionalTests/CatalogApiRatingTests.cs`.
- [ ] T009 [P] [US1] Add functional coverage for rating upsert behavior when the same user rates the same item again in `tests/Catalog.FunctionalTests/CatalogApiRatingTests.cs`.

### Implementation for User Story 1

- [ ] T010 [US1] Add rating aggregate helpers and authenticated user resolution in `src/Catalog.API/Apis/CatalogApi.cs`.
- [ ] T011 [US1] Implement `POST /api/catalog/items/{id}/rate` in `src/Catalog.API/Apis/CatalogApi.cs` and persist `CatalogItemRating` rows with immediate `AverageRating` and `RatingCount` updates.

**Checkpoint**: Story 1 is complete and independently testable.

---

## Phase 4: User Story 2 - View Demand Signals on a Club (Priority: P2)

**Goal**: Viewing a club increments its raw view count, and authenticated users can toggle favorites that update the favorite count.

**Independent Test**: `GET /api/catalog/items/{id}` increments `ViewCount`, and `POST /api/catalog/items/{id}/favorite` toggles the caller's favorite state while keeping `FavoriteCount` accurate.

### Tests for User Story 2

- [ ] T012 [P] [US2] Add functional coverage for `ViewCount` increments in `tests/Catalog.FunctionalTests/CatalogApiEngagementTests.cs`.
- [ ] T013 [P] [US2] Add functional coverage for favorite toggle behavior and `FavoriteCount` updates in `tests/Catalog.FunctionalTests/CatalogApiEngagementTests.cs`.

### Implementation for User Story 2

- [ ] T014 [US2] Update `GET /api/catalog/items/{id}` in `src/Catalog.API/Apis/CatalogApi.cs` so a successful read increments `ViewCount` before returning the item.
- [ ] T015 [US2] Implement `POST /api/catalog/items/{id}/favorite` in `src/Catalog.API/Apis/CatalogApi.cs` and persist or remove `CatalogItemFavorite` rows while maintaining `FavoriteCount`.

**Checkpoint**: Story 2 is complete and independently testable.

---

## Phase 5: User Story 3 - Tag a Club (Priority: P3)

**Goal**: A seller can update listing tags and users can discover items by tag through the existing catalog query surface.

**Independent Test**: `PATCH /api/catalog/items/{id}/tags` persists normalized tags, and a tag-aware list request returns only items that contain the requested tag token.

### Tests for User Story 3

- [ ] T016 [P] [US3] Add functional coverage for tag update persistence and tag-aware filtering in `tests/Catalog.FunctionalTests/CatalogApiTagTests.cs`.

### Implementation for User Story 3

- [ ] T017 [US3] Normalize and carry `Tags` through catalog item create/update paths in `src/Catalog.API/Model/CatalogItem.cs` and `src/Catalog.API/Apis/CatalogApi.cs`.
- [ ] T018 [US3] Implement `PATCH /api/catalog/items/{id}/tags` and tag-aware filtering on the catalog list endpoint in `src/Catalog.API/Apis/CatalogApi.cs`.

**Checkpoint**: Story 3 is complete and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency checks across all stories.

- [ ] T019 [P] Update `specs/003-club-scoring-ratings/quickstart.md` and `specs/003-club-scoring-ratings/contracts/catalog-api-club-scoring.md` if the implemented request or response shapes differ from the plan.
- [ ] T020 Validate the finished feature with `dotnet build src/Catalog.API/Catalog.API.csproj` and `dotnet test tests/Catalog.FunctionalTests/Catalog.FunctionalTests.csproj`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion and blocks all user stories.
- **User Stories (Phase 3+)**: Depend on Foundational completion.
- **Polish (Phase 6)**: Depends on the user stories selected for implementation being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational; no dependency on other stories.
- **User Story 2 (P2)**: Can start after Foundational; independent of Story 1 aside from shared auth plumbing.
- **User Story 3 (P3)**: Can start after Foundational; independent of Stories 1 and 2.

### Within Each User Story

- Tests are written before the implementation tasks for that story.
- Shared model/configuration work comes before endpoint logic.
- Endpoint behavior is implemented before polish or doc adjustments.
- Each story should be left in a shippable state before moving to the next priority.

### Parallel Opportunities

- `T001` and `T002` can run in parallel because they touch different files.
- `T006` and `T007` can run in parallel because they touch different startup/test files.
- Story test tasks marked `[P]` can run in parallel within each story.
- After Foundational work, the story branches can proceed independently.

---

## Parallel Example: User Story 1

```text
Task: "Add functional coverage for authenticated rating submission and invalid star values in tests/Catalog.FunctionalTests/CatalogApiRatingTests.cs"
Task: "Add functional coverage for rating upsert behavior when the same user rates the same item again in tests/Catalog.FunctionalTests/CatalogApiRatingTests.cs"
```

## Parallel Example: User Story 2

```text
Task: "Add functional coverage for ViewCount increments in tests/Catalog.FunctionalTests/CatalogApiEngagementTests.cs"
Task: "Add functional coverage for favorite toggle behavior and FavoriteCount updates in tests/Catalog.FunctionalTests/CatalogApiEngagementTests.cs"
```

## Parallel Example: User Story 3

```text
Task: "Add functional coverage for tag update persistence and tag-aware filtering in tests/Catalog.FunctionalTests/CatalogApiTagTests.cs"
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate Story 1 independently before moving on.

### Incremental Delivery

1. Setup + Foundational establish the database and auth/test plumbing.
2. Ship Story 1 first for rating trust signals.
3. Add Story 2 for reads and favorites.
4. Add Story 3 for tag management and discovery.
5. Finish with polish and a full build/test pass.

### Suggested MVP Scope

- Deliver only User Story 1 first if you need the smallest meaningful increment.

### Format Validation

- All tasks follow the required checklist format with a checkbox, task ID, and file path.
- Story-phase tasks include the required `[USx]` label.
- Parallelizable tasks are marked with `[P]` only when they touch different files and do not depend on incomplete work.