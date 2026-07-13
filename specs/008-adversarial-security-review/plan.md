# Implementation Plan: Adversarial Code & CVE Review

**Branch**: `main` (feature pinned via `.specify/feature.json`, no dedicated feature branch exists yet) | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-adversarial-security-review/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add three independently-runnable, on-demand review capabilities — static code/vulnerability
review, dependency/CVE review, and adversarial runtime testing — plus a shared findings
store and consolidated report, without adding a new microservice or database. Each
capability is implemented as a repo-local Claude Code Skill (matching the existing
`local-setup`/`speckit-*` skill pattern) backed by deterministic scripts for data
gathering/persistence and Agent invocations for judgment calls. The dependency/CVE
capability's vulnerability data source is **OSV.dev** (pinned via `/speckit-clarify`,
2026-07-13). Findings persist as git-tracked JSON so acknowledgement state and cross-run
diffing work without new infrastructure, consistent with the constitution's Microservices
Continuity principle.

## Technical Context

**Language/Version**: Target application under review: C# / .NET 10 (`global.json` pins SDK `10.0.100`; all `src/*.csproj` target `net10.0` — note this is newer than the constitution's stated ".NET 9", a pre-existing discrepancy out of scope for this feature). Review tooling itself: Claude Code Agent Skills (Markdown-defined) + Bash scripts (matching `.specify/scripts/bash/` convention), no new application-code language introduced.
**Primary Dependencies**: OSV.dev REST API (`api.osv.dev`, unauthenticated) for vulnerability matching; `dotnet list package` / `Directory.Packages.props` parsing for NuGet inventory; Aspire's `src/eShop.AppHost/Program.cs` resource graph for container/infra inventory (Redis, RabbitMQ, Postgres).
**Storage**: Git-tracked JSON files under `specs/008-adversarial-security-review/findings/` (`findings.json`, `runs.json`) — no new database. This is tooling metadata about the repo, not application/token data, so it is outside the constitution's "durable transactional store" requirement (which applies specifically to the token ledger).
**Testing**: Each capability's deterministic script (NuGet/Aspire inventory parsing, OSV.dev query/response handling, findings-file read/write, status-transition logic) gets unit tests under `tests/` following this repo's existing MSTest convention (`MSTest.Sdk` per `global.json`). The judgment-driven Agent behavior (bug pattern recognition, reachability assessment, adversarial scenario generation) is validated via the Independent Test scenarios already defined per user story in spec.md (e.g., a deliberately-introduced bug/vulnerability fixture that must be caught), not unit-tested directly.
**Target Platform**: Same as the rest of the repo — cross-platform .NET tooling invoked from a developer/maintainer workstation; the adversarial capability additionally targets a locally-run Aspire AppHost instance (loopback only, per FR-006).
**Project Type**: Tooling/process automation layered on an existing multi-service web application (.NET Aspire microservices) — not a new user-facing service.
**Performance Goals**: N/A — this is on-demand maintainer tooling (FR-011: on-demand only in this phase), not a latency-sensitive user-facing path. No performance targets are set by the spec.
**Constraints**: Adversarial capability MUST refuse to target anything but a loopback address (FR-006). All three capabilities are advisory-only and MUST NOT block merge/build/release (Assumptions). Findings from all three capabilities MUST be viewable together (FR-009) and re-runs MUST NOT resurface acknowledged findings (FR-008/SC-003).
**Scale/Scope**: Current repo: 19 `.csproj` projects, 4 Aspire-managed infra components (Redis, RabbitMQ, 4 Postgres databases), 8 running services in the AppHost graph. Scale is bounded by this repo's own dependency/service count, not an external load target.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applicability | Assessment |
|---|---|---|
| I. Microservices Continuity | Applies | PASS — no new service added; capabilities are Skills + scripts layered on the existing repo, following the same convention as existing `local-setup`/`speckit-*` skills. No parallel architecture introduced. |
| II. Token Ledger Integrity & Non-Convertibility | N/A | This feature does not touch token issuance, balances, or the ledger. |
| III. Attribute-Based, Anti-Fraud Valuation | N/A | This feature does not touch club listing valuation. |
| IV. Trust & Safety in Physical Trades | N/A | This feature does not touch listings, condition grading, or disputes. |
| V. Risk-Based Testing Discipline | Applies | PASS — this feature's code does not touch the token ledger, trade state machine, or valuation calculation, so standard PR review with required tests applies (not mandatory TDD). Deterministic scripts (inventory parsing, OSV.dev handling, findings persistence) get unit tests per the Testing section above. |
| VI. Marketplace Scope Boundary: No Fulfillment Ownership | N/A | This feature does not touch shipping/fulfillment. |
| Technology Constraints (EventBus for cross-service comm) | N/A | No new service is added, so no new cross-service communication is introduced. |
| Technology Constraints (durable transactional store for token ledger) | N/A | This feature's findings store holds tooling metadata, not token/ledger data. |

**Result**: PASS. No violations; no entries required in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/008-adversarial-security-review/
├── spec.md               # Feature spec (incl. Clarifications: OSV.dev decision)
├── plan.md               # This file (/speckit-plan command output)
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   ├── skill-commands.md # Maintainer-facing command contracts
│   └── osv-dev-api.md    # External OSV.dev API integration contract
├── findings/             # Created at first run, not by /speckit-plan
│   ├── findings.json
│   ├── runs.json
│   ├── report.md                    # Always-current consolidated view
│   └── YYYYMMDD-<slug>.md           # Per-capability dated snapshots (code-review/cve-review/adversarial-review)
├── review-config.json    # Trigger-mode config per capability (FR-011); created during implementation
└── checklists/
    └── requirements.md   # Spec quality checklist (already present)
```

### Source Code (repository root)

No changes to `src/` or `tests/` service projects. New files only:

```text
.claude/skills/
├── security-code-review/
│   └── SKILL.md           # User Story 1 (FR-001, FR-002)
├── security-cve-review/
│   └── SKILL.md           # User Story 2 (FR-003, FR-004)
├── security-adversarial-review/
│   └── SKILL.md           # User Story 3 (FR-005, FR-006)
├── security-findings-ack/
│   └── SKILL.md           # FR-008
├── security-findings-link/
│   └── SKILL.md           # Edge Case §2 (relatedFindingIds producer)
└── security-report/
    └── SKILL.md           # FR-007, FR-009

.specify/scripts/bash/
├── security-inventory-dependencies.sh   # NuGet + Aspire infra enumeration (feeds security-cve-review)
├── security-osv-query.sh                # OSV.dev querybatch + vuln detail calls
└── security-findings-store.sh           # Shared read/write/status-transition helper over findings/*.json

tests/
└── Security.Tooling.UnitTests/          # New MSTest project: unit tests for the three scripts above
    └── Security.Tooling.UnitTests.csproj
```

**Structure Decision**: New capabilities live entirely under `.claude/skills/` (agent-facing
commands) and `.specify/scripts/bash/` (deterministic logic the skills call into), following
this repo's existing convention for repo-tooling (as opposed to `src/`, which is reserved for
the golf-marketplace application services themselves). A single new MSTest project covers
the deterministic scripts' logic; the judgment-driven Agent behavior is exercised through the
Independent Test scenarios in spec.md, not unit tests.

## Complexity Tracking

*No violations — table intentionally empty.*
