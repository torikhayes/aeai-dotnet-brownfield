# Implementation Plan: Marketplace UI (List Club + My Listings)

**Branch**: `006-marketplace-ui` | **Date**: 2026-07-14 | **Spec**: `/specs/006-marketplace-ui/spec.md`
**Input**: Feature specification from `/specs/006-marketplace-ui/spec.md`

## Summary

Implement the two P1 storefront experiences in WebApp: (1) a protected **Sell My Club** page where authenticated users submit a listing through the existing Catalog API seller endpoint, and (2) a protected **My Listings** page where sellers view their own listings and listing status/metrics. Reusable UI should live in `WebAppComponents` and be consumed by `WebApp` route pages.

This plan intentionally scopes to User Story 1 and User Story 2 only.

## Scope

### In Scope

- Sell My Club page and listing form UX
- My Listings page and listing summary cards/table
- Navigation entry points for authenticated sellers
- Catalog service client extensions needed by these pages
- Validation and error handling for listing create flow
- Playwright coverage for these two pages

### Out of Scope

- Token wallet widget and transaction panel (spec US3)
- Semantic search UI/copy changes (spec US4)
- Checkout payment toggle updates (FR-007)

## Technical Context

**Language/Version**: C# (.NET 10), Razor Components (Blazor Server interactivity)
**Primary Dependencies**: Existing `CatalogService` HTTP client, ASP.NET Core auth (`Authorize`), `Antiforgery`
**Storage/API**: Existing Catalog API endpoints
- `POST /api/catalog/items/listings`
- `GET /api/catalog/items/my-listings`
- `GET /api/catalogbrands`
- `GET /api/catalogtypes`

**Project Areas**:
- `src/WebApp/Components/Pages/` for route pages
- `src/WebApp/Components/Layout/` for navigation entry points
- `src/WebAppComponents/` for reusable listing form and listing card/table components
- `src/WebAppComponents/Services/` for catalog client contract updates
- `e2e/` for Playwright test additions

## Existing Baseline

- Auth is already configured in WebApp (`AddAuthenticationServices`, `AddCascadingAuthenticationState`).
- Route-level protection exists via `[Authorize]` on pages like `/user/orders`.
- Catalog client already supports list/catalog lookup methods (`GetCatalogItems`, `GetBrands`, `GetTypes`).
- Catalog API already exposes seller listing endpoints from spec 002.

## Design Decisions

1. **Route ownership**
- Add route pages in `WebApp` for URL ownership and auth attributes.
- Keep reusable form/list visual components in `WebAppComponents`.

2. **Auth enforcement**
- Use route-level `[Authorize]` on both pages.
- Optionally hide nav links for anonymous users with `AuthorizeView`, but route guard remains authoritative.

3. **Request DTO alignment**
- Add a WebApp-side request model mirroring `CreateSellerListingRequest` fields.
- Condition values must align with API constraints: `New`, `Excellent`, `Good`, `Fair`.

4. **Listings status display**
- Active/sold visual state derives from `AvailableStock` (`>0` active, `0` sold).
- Token price behavior is forward-compatible: when not available, show "verification pending".

5. **Error handling**
- Inline validation for required fields and valid ranges.
- API errors surfaced as form-level messages without page crash.

## Implementation Phases

### Phase 1 - Foundation and Service Contracts

- Extend `ICatalogService` and `CatalogService` with:
  - `CreateSellerListing(...)`
  - `GetMyListings(pageIndex, pageSize)`
- Add reusable request/response models in `WebAppComponents` as needed for seller listing fields and listing metrics.

### Phase 2 - Sell My Club Page (US1)

- Add `Sell My Club` route page in `WebApp` with `[Authorize]`.
- Create `ListClubForm` component in `WebAppComponents`:
  - Brand/type dropdowns loaded from catalog metadata endpoints
  - Condition dropdown constrained to API-supported values
  - Required field validation and long-description boundary handling
- On successful submit:
  - POST to `/api/catalog/items/listings`
  - Redirect to `My Listings` route and optionally surface success state

### Phase 3 - My Listings Page (US2)

- Add `My Listings` route page in `WebApp` with `[Authorize]`.
- Create `MyListingsPage` component in `WebAppComponents`:
  - Fetch from `/api/catalog/items/my-listings`
  - Show empty state with CTA to Sell My Club
  - Display per-item: active/sold status, sold date placeholder strategy, `ViewCount`, `FavoriteCount`, `AverageRating`, token-price/pending state

### Phase 4 - Navigation and UX Integration

- Add authenticated navigation links to seller pages in existing layout/menu.
- Ensure links are discoverable from header user menu.
- Keep anonymous experience unchanged.

### Phase 5 - Testing and Validation

- Add/extend Playwright specs for:
  - Authenticated listing creation happy path
  - Validation errors on missing fields
  - My Listings populated state
  - My Listings empty state
  - Unauthenticated redirects for protected pages

## Risks and Mitigations

- **API contract drift risk**: Mirror DTO fields exactly and keep types nullable/required aligned with Catalog API request contract.
- **Long-description payload edge case**: Add client-side max length and clear validation message before submit.
- **Post-create eventual consistency**: After create, redirect to My Listings and trigger explicit refresh; optionally retry once before empty-state message.

## Definition of Done (for this scope)

- Authenticated users can submit Sell My Club form successfully.
- Submitted listing appears in My Listings.
- Anonymous users are redirected when accessing either route.
- My Listings clearly distinguishes active vs sold listings.
- Required Playwright scenarios for these two pages pass.
- No regressions in existing catalog, cart, or orders pages.
