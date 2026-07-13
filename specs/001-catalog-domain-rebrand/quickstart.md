# Quickstart: Catalog Domain Rebrand Validation

## Prerequisites
- .NET SDK installed for the repo (see `global.json`).
- Docker running (required by Aspire stack).

## 1. Start from a clean catalog database state
This feature is data-only and seed-driven. To avoid mixed old/new catalog content, run with a clean catalog DB for first verification.

## 2. Launch the application
Run from repo root:

```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

## 3. Validate taxonomy endpoints
Use the Catalog API once the app is running.

1. `GET /api/catalog/catalogtypes`
- Expect exactly: Driver, Iron Set, Wedge, Putter, Hybrid, Fairway Wood.

2. `GET /api/catalog/catalogbrands`
- Expect exactly: Callaway, TaylorMade, Ping, Titleist, Cobra.

## 4. Validate catalog results
- Open the storefront and confirm product cards/filter labels are golf-domain only.
- Confirm no AdventureWorks-era names are present.

## 5. Validate filtering behavior
- Filter by one type (for example, Putter) and verify only matching club listings appear.
- Combine type + brand filters and verify narrowed results remain correct.

## 6. Validate regression baseline
Run existing catalog tests without modifying endpoint contracts:

```bash
dotnet test tests/Catalog.FunctionalTests/Catalog.FunctionalTests.csproj
```

## Expected outcome
- Golf taxonomy is fully present.
- No AdventureWorks names remain visible.
- Existing catalog endpoints continue functioning unchanged.
