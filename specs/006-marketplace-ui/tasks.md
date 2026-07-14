# Tasks: Marketplace UI (List Club + My Listings)

**Input**: Design documents from `/specs/006-marketplace-ui/`
**Prerequisites**: plan.md, spec.md

**Scope**: This task list covers only User Story 1 (List Club) and User Story 2 (My Listings).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1` or `US2`
- Include exact file paths in task descriptions

## Phase 1: Setup and Contracts

**Purpose**: Add service methods and models required by both pages.

- [X] T001 [P] Add seller-listing request/response models in `src/WebAppComponents/Catalog/` (create files for `CreateSellerListingRequest` and `SellerListingItem`/result wrapper as needed).
- [X] T002 Update `src/WebAppComponents/Services/ICatalogService.cs` with methods for `CreateSellerListing` and `GetMyListings`.
- [X] T003 Implement new service methods in `src/WebAppComponents/Services/CatalogService.cs` using `POST /api/catalog/items/listings` and `GET /api/catalog/items/my-listings`.

**Checkpoint**: Page components can call typed service APIs for both user stories.

---

## Phase 2: User Story 1 - List Club Page (Priority: P1) 🎯 MVP

**Goal**: Authenticated seller can submit a listing from the storefront UI.

**Independent Test**: Log in, complete Sell My Club form, submit, and verify redirect to My Listings with the new club visible.

### Tests for User Story 1

- [X] T004 [P] [US1] Add Playwright test in `e2e/` for authenticated listing creation happy path from Sell My Club page.
- [X] T005 [P] [US1] Add Playwright test in `e2e/` for client-side validation (required fields block submit).
- [X] T006 [P] [US1] Add Playwright test in `e2e/` for unauthenticated navigation to Sell My Club redirecting to login.

### Implementation for User Story 1

- [X] T007 [US1] Create route page `src/WebApp/Components/Pages/User/SellMyClub.razor` with `@page` and `[Authorize]`.
- [X] T008 [US1] Create reusable component `src/WebAppComponents/Catalog/ListClubForm.razor` with fields for name, type, brand, condition, manufacture year, price, description, tags, and photo URLs.
- [X] T009 [US1] Add inline validation and API error messaging in `src/WebAppComponents/Catalog/ListClubForm.razor` (required fields, valid condition values, positive price).
- [X] T010 [US1] Wire successful submit flow to redirect from `SellMyClub.razor` to My Listings route.

**Checkpoint**: User Story 1 is independently testable.

---

## Phase 3: User Story 2 - My Listings Page (Priority: P1)

**Goal**: Authenticated seller can view their listings with status and engagement metrics.

**Independent Test**: Log in as seller with listings and verify item count, status labels, and metrics render correctly.

### Tests for User Story 2

- [X] T011 [P] [US2] Add Playwright test in `e2e/` for populated My Listings state (active + sold rows/cards).
- [X] T012 [P] [US2] Add Playwright test in `e2e/` for empty-state message and Sell My Club call-to-action.
- [X] T013 [P] [US2] Add Playwright test in `e2e/` for unauthenticated access to My Listings redirecting to login.

### Implementation for User Story 2

- [X] T014 [US2] Create route page `src/WebApp/Components/Pages/User/MyListings.razor` with `@page` and `[Authorize]`.
- [X] T015 [US2] Create reusable component `src/WebAppComponents/Catalog/MyListingsPage.razor` that loads data from `GetMyListings`.
- [X] T016 [US2] Render listing status (active/sold), `ViewCount`, `FavoriteCount`, `AverageRating`, and token-price pending fallback in `MyListingsPage.razor`.
- [X] T017 [US2] Add empty-state UX and CTA link to Sell My Club in `MyListingsPage.razor`.

**Checkpoint**: User Story 2 is independently testable.

---

## Phase 4: Navigation Integration and Polish

**Purpose**: Make pages discoverable and validate no regressions.

- [X] T018 Add authenticated navigation links to Sell My Club and My Listings in `src/WebApp/Components/Layout/UserMenu.razor`.
- [X] T019 [P] Ensure page header copy/title sections are set in both new route pages under `src/WebApp/Components/Pages/User/`.
- [ ] T020 [P] Run e2e specs covering these scenarios and capture pass/fail notes in `specs/006-marketplace-ui/`.

  **Run results (2026-07-14)**: 3 failed, 1 passed (login.setup ✅).
  - `MyListingsAuthRedirect.spec.ts` ❌ — `ERR_HTTP_RESPONSE_CODE_FAILURE` on `GET /user/my-listings`; Blazor SSR `[Authorize]` returns an error code instead of redirecting to login. Fix: ensure `app.UseAuthentication()` / `app.UseAuthorization()` middleware is configured to redirect (not reject) for non-API routes, or update the test to expect a redirect via `page.waitForNavigation`.
  - `MyListingsTest.spec.ts` ❌ (both tests) — expects heading `'Welcome to Golf Odyssey'` on `/user/sell-my-club` which does not exist on the page. Fix: update assertion to match the actual page heading (e.g. `'Sell My Club'` or remove the heading check if the page loads without a visible h1).

---

## Dependencies and Order

- T001-T003 must complete before page implementation tasks.
- US1 (T007-T010) and US2 (T014-T017) can proceed in parallel after service contract tasks are done.
- Navigation task T018 should complete after route pages exist.
- Polish and full validation (T020) is last.

## Parallel Execution Examples

```text
Task: "Add Playwright test for Sell My Club happy path in e2e/"
Task: "Add Playwright test for Sell My Club validation errors in e2e/"
```

```text
Task: "Create My Listings route page in src/WebApp/Components/Pages/User/MyListings.razor"
Task: "Create reusable MyListingsPage component in src/WebAppComponents/Catalog/MyListingsPage.razor"
```
