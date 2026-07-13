# Research: Adversarial Code & CVE Review

**Input**: `specs/008-adversarial-security-review/spec.md` (incl. `## Clarifications`)
**Date**: 2026-07-13

This feature adds three review *capabilities* on top of the existing golf-marketplace
Aspire solution (formerly eShop). It does not change the running application's runtime
behavior — it is tooling/process automation. Each unknown below was resolved by inspecting
the actual repo state rather than assumption.

## 1. What runs each review capability?

- **Decision**: Implement each of the three capabilities as a repo-local Claude Code Skill
  under `.claude/skills/` (same pattern as the existing `local-setup` and `speckit-*`
  skills), each backed by a deterministic script for the parts that must not hallucinate
  (dependency enumeration, OSV.dev querying, findings-file read/write) and an Agent
  invocation for the parts that require judgment (bug/vulnerability pattern recognition,
  reachability assessment, adversarial scenario generation).
- **Rationale**: The repo already has a working skill/agent ecosystem (speckit commands,
  `local-setup`) and this environment's built-in `code-review` / `security-review` skills
  demonstrate the same shape of capability the spec asks for. Reusing that pattern avoids
  introducing a new service, tool, or hosting requirement — directly satisfying
  Constitution Principle I (Microservices Continuity: extend within existing conventions,
  don't stand up a parallel architecture).
- **Alternatives considered**:
  - A new standalone .NET console tool/service — rejected: adds a new deployable unit for
    something that is advisory tooling, not a marketplace capability; conflicts with
    Principle I's intent.
  - A GitHub Action running on every push — rejected for this phase: FR-011 explicitly
    scopes this feature to on-demand only, with automatic-on-PR deferred to a future
    configuration change, not built now.

## 2. How is the dependency/CVE review capability (FR-003/FR-004) scoped and sourced?

- **Decision**: Vulnerability data source is **OSV.dev** (already pinned in spec.md via
  `/speckit-clarify`, 2026-07-13). The dependency inventory itself is built from:
  - Application libraries: NuGet packages resolved via `Directory.Packages.props` (central
    package management) crossed with each `*.csproj`'s `<PackageReference>` entries — 19
    project files under `src/` and `tests/`.
  - Container/infrastructure components: the Aspire resources declared in
    `src/eShop.AppHost/Program.cs` — currently Redis (`AddRedis`), RabbitMQ
    (`AddRabbitMQ`), and Postgres (`AddNpgsql`-backed `identityDb`/`catalogDb`/`orderDb`/
    `webhooksDb`) — rather than Dockerfiles (this repo has none; Aspire manages container
    images directly).
  - Each resolved `(ecosystem, package, version)` triple is queried against OSV.dev's batch
    query endpoint (`POST https://api.osv.dev/v1/querybatch`), which supports the NuGet
    ecosystem needed here.
- **Rationale**: OSV.dev has first-class NuGet ecosystem coverage, requires no API key, and
  returns machine-readable `affected` ranges suitable for automated version matching —
  needed for FR-004's "assess reachability" step to have something concrete to reason over.
- **Alternatives considered**: GitHub Advisory Database / Dependabot alerts (already
  configured in `.github/dependabot.yml` for weekly NuGet updates) — viable and already
  partially present, but the user explicitly chose OSV.dev during clarification; Dependabot
  remains a complementary, separate mechanism and is out of scope here. NVD directly — more
  CVE-centric than package-version-centric, worse fit for automated version-range matching.

## 3. How does the adversarial capability (FR-005/FR-006) target a running instance safely?

- **Decision**: The adversarial capability targets a locally-run Aspire AppHost instance
  (`dotnet run --project src/eShop.AppHost`) bound to localhost only. The skill MUST refuse
  to run unless it can positively confirm the target's Aspire dashboard/resource endpoints
  resolve to a loopback address; it never accepts a remote/staging URL as a target.
- **Rationale**: Directly satisfies FR-006 and the Edge Cases/Assumptions constraints
  (isolated instance only, state-changing actions permitted only there). Aspire's local
  dashboard already exposes per-service endpoint URLs, which the adversarial agent needs to
  enumerate attack surface (HTTP endpoints across Basket.API, Catalog.API, Ordering.API,
  Identity.API, Webhooks.API, WebApp).
- **Alternatives considered**: Targeting a shared docker-compose/staging deployment —
  rejected outright, forbidden by FR-006.

## 4. Where do Findings/Review Runs persist (needed for FR-007/FR-008/FR-009)?

- **Decision**: A git-tracked findings store at
  `specs/008-adversarial-security-review/findings/findings.json`, one array of `Finding`
  records (schema in `data-model.md`), plus a `runs.json` log of `Review Run` records. No
  new database or service.
- **Rationale**: Acknowledgement state (FR-008) and cross-run diffing (SC-003) need to
  persist between invocations; a git-tracked JSON file gives that for free — it's diffable
  in PRs, requires no new infra (consistent with Principle I), and matches this repo's
  existing convention of tracking spec-adjacent state as files under `specs/`. This is
  findings *about* the repo, not application data, so it does not touch the constitution's
  "durable transactional store" requirement (Principle II/Technology Constraints), which
  applies only to the token ledger.
- **Alternatives considered**: A new Postgres table — rejected as disproportionate
  infrastructure for advisory tooling metadata; would also imply a new migration path in a
  service that doesn't otherwise need one.

## 5. How does the consolidated view (FR-009) and cross-capability dedup (Edge Case #2) work?

- **Decision**: A generated Markdown report (`specs/008-adversarial-security-review/findings/report.md`),
  regenerated on demand from `findings.json`, grouped by severity across all three source
  capabilities. Findings that a maintainer or a capability links via a shared
  `relatedFindingIds` field (see `data-model.md`) are rendered as one consolidated entry
  with multiple "detected by" tags rather than duplicate rows.
- **Rationale**: Satisfies FR-009 and Edge Case #2 without building a separate UI/service —
  the report is just a derived view over the same findings store.

## 6. Trigger-mode configuration (FR-011)

- **Decision**: A `specs/008-adversarial-security-review/review-config.json` with one entry
  per capability, e.g. `{ "codeReview": { "trigger": "on-demand" }, "dependencyReview": {
  "trigger": "on-demand" }, "adversarialReview": { "trigger": "on-demand" } }`. Each skill
  reads its own entry at invocation time and is written so that adding an
  `"automatic-on-pull-request"` value later is a config change only — no code path in the
  skill itself branches on trigger mode today beyond reading this value.
- **Rationale**: Directly satisfies FR-011/SC-006 (config-driven, not hardcoded) while
  deliberately not building the PR-trigger mechanism itself (out of scope per Assumptions).

## Open items deferred to `/speckit-tasks` / implementation

- Exact severity-rating scale (e.g., Critical/High/Medium/Low vs. numeric CVSS-aligned) is
  left for `data-model.md` to fix as an enum; not a planning blocker.
- Exact OSV.dev batch size/rate-limit handling is an implementation detail of the
  dependency-review skill's script, not a design decision requiring research.
