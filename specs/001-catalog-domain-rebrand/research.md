# Research: Catalog Domain Rebrand

## Decision 1: Domain data replacement strategy
- Decision: Replace `Setup/catalog.json` with golf-only catalog entries and keep the existing `CatalogContextSeed` flow (brands/types regenerated from the JSON source).
- Rationale: This is a data-only rebrand; the existing seed path already derives brands/types/items from one source file and preserves endpoint/schema behavior.
- Alternatives considered:
  - Add new DB tables for golf taxonomy (rejected: violates no-schema-change requirement).
  - Add a feature flag to switch domains at runtime (rejected: adds complexity for a one-time rebrand baseline).

## Decision 2: Mixed old/new data handling
- Decision: Treat mixed old/new catalog state as unsupported for rollout; enforce clean-start guidance for validation and deployments where this feature is enabled.
- Rationale: `CatalogContextSeed` seeds only when `CatalogItems` is empty. If old rows remain, the new JSON is not applied, which would violate SC-001.
- Alternatives considered:
  - Add destructive runtime cleanup logic (rejected for this phase: increases operational risk and changes startup behavior).
  - Add a migration to transform old rows in place (rejected: FR-006 explicitly forbids migrations for this feature).

## Decision 3: Missing image handling during transition
- Decision: Use golf-labeled placeholder images where production golf photography is unavailable; keep existing picture naming (`{Id}.webp`) and static image endpoint contract.
- Rationale: Satisfies FR-004 without API or schema changes and keeps image retrieval behavior stable.
- Alternatives considered:
  - Defer image replacement entirely (rejected: fails FR-004).
  - Introduce new image storage/upload workflow (rejected: out of scope for data-only rebrand).

## Decision 4: Verification/test approach
- Decision: Validate with existing catalog API tests plus focused checks that catalog types/brands and search/filter behavior reflect golf-only taxonomy.
- Rationale: SC-003 requires existing endpoint tests to pass unchanged; this change should be validated as behavioral data substitution, not API redesign.
- Alternatives considered:
  - Rely on manual storefront checks only (rejected: insufficient regression confidence).
  - Rewrite endpoint tests around new APIs (rejected: no new APIs are introduced by this feature).
