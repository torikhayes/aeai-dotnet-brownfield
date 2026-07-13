# Implementation Plan: Catalog Domain Rebrand

**Branch**: `001-catalog-domain-rebrand` | **Date**: 2026-07-13 | **Spec**: `/specs/001-catalog-domain-rebrand/spec.md`
**Input**: Feature specification from `/specs/001-catalog-domain-rebrand/spec.md`

## Summary

Replace Catalog seed domain data and images from AdventureWorks-style products to golf clubs while preserving all existing Catalog API contracts, schemas, and startup migration/seed flow. The approach is intentionally data-only: update `Setup/catalog.json` and `Pics/` assets, then validate existing list/filter endpoints and storefront rendering with the golf taxonomy.

## Technical Context

**Language/Version**: C# (.NET 9 application stack; repo SDK pinned via `global.json`)  
**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core + Npgsql, .NET Aspire orchestration, EventBusRabbitMQ  
**Storage**: PostgreSQL (`catalogdb`) plus static image files served from Catalog API web root  
**Testing**: .NET test projects (xUnit v3 + Microsoft.Testing.Platform) and existing catalog functional tests  
**Target Platform**: Linux containers orchestrated by Aspire; local development on macOS/Windows/Linux  
**Project Type**: Distributed microservices web application  
**Performance Goals**: Preserve current catalog endpoint responsiveness; no additional request-path processing introduced by this feature  
**Constraints**: No schema changes, no new migrations, no endpoint contract changes, zero AdventureWorks terms visible post-seed  
**Scale/Scope**: Catalog taxonomy replacement to 6 club types and 5 brands, with representative seeded item set and mapped images

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Gate Review

- Principle I (Microservices Continuity): PASS. Changes are contained to existing Catalog.API data/assets and keep current service boundaries.
- Principle II (Token Ledger Integrity): PASS (N/A). No token-affecting logic or ledger mutations are introduced.
- Principle III (Attribute-Based Valuation): PASS (N/A). No token valuation path touched.
- Principle IV (Trust & Safety): PASS (N/A for this phase). Listing evidence/dispute logic is unaffected.
- Principle V (Risk-Based Testing): PASS. No high-risk token/trade code changes; regression verification uses existing catalog endpoint tests.
- Principle VI (Scope Boundary): PASS. No fulfillment ownership behavior introduced.

**Gate Result**: PASS. Phase 0 may proceed.

### Post-Phase 1 Re-Check

- Research, data model, contracts, and quickstart remain data-only and preserve API/schema behavior.
- No constitutional violations introduced by design artifacts.

**Gate Result**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/001-catalog-domain-rebrand/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── catalog-api-domain-rebrand.md
└── tasks.md             # created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Catalog.API/
│   ├── Setup/catalog.json
│   ├── Infrastructure/CatalogContextSeed.cs
│   ├── Apis/CatalogApi.cs
│   └── Pics/
├── WebApp/
└── WebAppComponents/

tests/
└── Catalog.FunctionalTests/

e2e/
└── *.spec.ts
```

**Structure Decision**: Use the existing distributed service structure and modify only catalog data/assets and related verification tests. No new projects or service boundaries are required.

## Complexity Tracking

No constitutional violations or complexity exceptions identified.
