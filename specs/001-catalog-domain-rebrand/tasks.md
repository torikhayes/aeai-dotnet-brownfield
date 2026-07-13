# Tasks: Catalog Domain Rebrand

**Input**: Design documents from `/specs/001-catalog-domain-rebrand/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`)
- Include exact file paths in each task description

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm baseline files and test surface for the data-only rebrand.

- [X] T001 Capture current catalog domain baseline in `src/Catalog.API/Setup/catalog.json` and identify old AdventureWorks-era names to replace.
- [X] T002 Inventory current catalog validation tests in `tests/Catalog.FunctionalTests/` and `e2e/*.spec.ts` for domain-term assertions.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish shared data/assets required by all user stories.

- [X] T003 Define canonical golf taxonomy and enforce it in seed data source `src/Catalog.API/Setup/catalog.json` (types: Driver, Iron Set, Wedge, Putter, Hybrid, Fairway Wood; brands: Callaway, TaylorMade, Ping, Titleist, Cobra).
- [X] T004 Create/update placeholder golf images in `src/Catalog.API/Pics/` for all seeded item IDs referenced by `src/Catalog.API/Setup/catalog.json`.
- [X] T005 Verify seed mechanism remains data-only with no schema/migration changes by validating `src/Catalog.API/Infrastructure/CatalogContextSeed.cs` behavior is unchanged.

**Checkpoint**: Foundation ready - story implementation can proceed.

---

## Phase 3: User Story 1 - Browse Golf Club Catalog (Priority: P1) 🎯 MVP

**Goal**: Users browse catalog and see only golf-domain types, brands, names, and images.

**Independent Test**: Start app with clean catalog DB and confirm `/api/catalog/catalogtypes`, `/api/catalog/catalogbrands`, and catalog item responses are golf-only.

### Tests for User Story 1

- [X] T006 [P] [US1] Add/adjust functional test assertions for golf-only brands/types in `tests/Catalog.FunctionalTests/`.
- [X] T007 [P] [US1] Add/adjust UI/E2E assertion coverage for golf-only catalog terminology in `e2e/BrowseItemTest.spec.ts`.

### Implementation for User Story 1

- [X] T008 [US1] Replace all catalog items with golf club listings in `src/Catalog.API/Setup/catalog.json` while preserving existing JSON schema.
- [X] T009 [US1] Ensure each seeded item references only required golf brand/type vocabulary in `src/Catalog.API/Setup/catalog.json`.
- [X] T010 [US1] Ensure every seeded item has corresponding golf placeholder image assets in `src/Catalog.API/Pics/`.

**Checkpoint**: User Story 1 is independently testable as MVP.

---

## Phase 4: User Story 2 - Search for Clubs by Type (Priority: P2)

**Goal**: Type and type+brand filtering continues to work against the golf taxonomy.

**Independent Test**: Query filtered catalog endpoints and verify result sets contain only selected type/brand combinations.

### Tests for User Story 2

- [X] T011 [P] [US2] Add/adjust filter behavior tests for type-only and type+brand combinations in `tests/Catalog.FunctionalTests/`.

### Implementation for User Story 2

- [X] T012 [US2] Validate no API contract changes are required in `src/Catalog.API/Apis/CatalogApi.cs` and keep filtering logic unchanged.
- [X] T013 [US2] Tune seed item distribution in `src/Catalog.API/Setup/catalog.json` so each required type appears in filtered results.

**Checkpoint**: User Stories 1 and 2 both function independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Regression verification and documentation alignment.

- [X] T014 [P] Run catalog functional regression tests from `tests/Catalog.FunctionalTests/` and capture outcomes for SC-003.
- [ ] T015 [P] Re-validate quickstart scenarios in `specs/001-catalog-domain-rebrand/quickstart.md` against implemented data/assets.
- [X] T016 Confirm no EF Core migrations were added and no schema files changed under `src/Catalog.API/Infrastructure/Migrations/`.

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5
- User stories depend on Foundational phase completion.

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 2; no dependency on US2.
- **US2 (P2)**: Starts after Phase 2 and relies on US1 seed taxonomy being established.

### Within Each User Story

- Tests first (T006/T007 and T011), then implementation tasks.
- Data changes in `src/Catalog.API/Setup/catalog.json` should be completed before image/test finalization.

### Parallel Opportunities

- T006 and T007 can run in parallel.
- T014 and T015 can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Parallel test updates
Task: "Add/adjust functional test assertions in tests/Catalog.FunctionalTests/"
Task: "Add/adjust e2e assertions in e2e/BrowseItemTest.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Setup + Foundational.
2. Complete US1 tests and implementation.
3. Validate golf-only catalog behavior end-to-end.

### Incremental Delivery

1. Deliver US1 (golf rebrand baseline).
2. Deliver US2 filter-focused validation.
3. Finish polish/regression checks.

### Parallel Team Strategy

1. One developer updates seed data/images.
2. One developer updates functional/e2e assertions.
3. Integrate and run regression tests.

---

## Notes

- This feature is data-only; no schema changes or migrations are permitted.
- Preserve existing API endpoint contracts and filtering logic.
- Keep task updates checked off in this file as implementation progresses.
