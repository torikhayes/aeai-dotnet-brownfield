# Implementation Plan: Club Scoring & Ratings

**Branch**: `003-club-scoring-ratings` | **Date**: 2026-07-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-club-scoring-ratings/spec.md`

## Summary

Extend `Catalog.API` with trust and discovery signals for club listings: raw view counting on item reads, authenticated per-user ratings with upsert semantics and aggregate maintenance, per-user favorites with toggle behavior, seller-managed tags persisted on the catalog item, and tag-filtered discovery on the existing catalog listing surface. The work stays inside the existing Catalog/EF Core boundary and ships with a schema migration plus functional coverage.

## Technical Context

**Language/Version**: C# on .NET 10.0 (current repo target)  
**Primary Dependencies**: ASP.NET Core minimal APIs, Entity Framework Core, Npgsql/PostgreSQL, Aspire hosting, xUnit v3 functional tests, MSTest unit-test infrastructure  
**Storage**: PostgreSQL via `CatalogContext` with EF Core migrations  
**Testing**: `dotnet test` with `tests/Catalog.FunctionalTests` plus existing solution-level test projects  
**Target Platform**: Server-side web service in the existing Aspire solution  
**Project Type**: Web service / microservice  
**Performance Goals**: Keep item read and mutation flows to a small, bounded number of database round-trips; preserve existing catalog API responsiveness  
**Constraints**: Preserve existing catalog endpoints, require authentication for rating/favorite/tag mutation paths, keep one rating per user per item, and keep token valuation logic unchanged  
**Scale/Scope**: Single service change in `src/Catalog.API` plus its functional test surface

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- Principle II: Pass. This feature does not touch token issuance, settlement, or convertibility.
- Principle III: Pass. Ratings, views, and favorites are informational signals only and do not feed token value.
- Principle IV: Pass. The feature does not alter dispute or fulfillment flows.
- Principle V: Pass. The affected code paths are data and API paths, so standard tests plus functional coverage are appropriate.
- Principle VI: Pass. The scope remains inside the catalog service boundary.

## Project Structure

### Documentation (this feature)

```text
specs/003-club-scoring-ratings/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── catalog-api-club-scoring.md
```

### Source Code (repository root)

```text
src/
└── Catalog.API/
    ├── Apis/
    ├── Infrastructure/
    │   ├── EntityConfigurations/
    │   └── Migrations/
    └── Model/

tests/
└── Catalog.FunctionalTests/
```

**Structure Decision**: Implement the feature entirely inside `Catalog.API` with matching functional tests in `tests/Catalog.FunctionalTests`. The new persistence objects live alongside the existing catalog entity, API, and EF Core migration conventions.

## Complexity Tracking

No constitution violations are expected for this feature.
