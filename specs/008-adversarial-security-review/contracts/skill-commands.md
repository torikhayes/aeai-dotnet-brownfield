# Contract: Review Capability Commands (maintainer-facing interface)

These are the interfaces a maintainer invokes directly (FR-010: each capability runnable
independently). Each is a Claude Code Skill under `.claude/skills/`, invoked as a slash
command. This is the only "external interface" this feature exposes — there is no new HTTP
API and no change to any existing service's endpoints.

**Dated snapshots**: each of the three review capabilities (`security-code-review`,
`security-cve-review`, `security-adversarial-review`), after persisting its findings, writes
a git-tracked, point-in-time snapshot of just its own findings to
`findings/YYYYMMDD-<slug>.md` (`code-review` / `cve-review` / `adversarial-review`), in
addition to regenerating the always-current `findings/report.md`. Re-running the same
capability again later the same day overwrites that day's file (date granularity, not
per-run); a different day gets a new file. This gives a browsable historical trail directly
as repo files, independent of `report.md`'s single mutable "latest" view.

## `/security-code-review [scope]`

- **Input**: `scope` — omit for full codebase (FR-001), or a change-set reference such as a
  PR number, branch name, or commit range (FR-002).
- **Behavior**: Scans source under `scope` for concrete bugs, vulnerabilities, and
  likely-issue patterns. Writes/updates `Finding` records with `source: code` in
  `findings/findings.json`. Appends a `ReviewRun` record with `capabilities: ["code"]`.
- **Output**: Findings report (see "Consolidated report" contract below), filtered to
  `source: code` for this run, or an explicit zero-findings statement (Acceptance Scenario 3,
  User Story 1) — never an empty/ambiguous output.

## `/security-cve-review`

- **Input**: none (always full dependency inventory — FR-003 does not scope this to a
  change set).
- **Behavior**: Enumerates NuGet packages (via `Directory.Packages.props` + each
  `*.csproj`) and Aspire-managed container/infra components (via
  `src/eShop.AppHost/Program.cs`), queries OSV.dev per `contracts/osv-dev-api.md`, assesses
  reachability, writes `DependencyComponent` / `VulnerabilityMatch` / `Finding` records with
  `source: dependency`.
- **Output**: Findings report filtered to `source: dependency`, each annotated with
  `relevance` (FR-004) — never presented as an undifferentiated raw match list.

## `/security-adversarial-review <target-url>`

- **Input**: `target-url` — MUST resolve to a loopback address (localhost/127.0.0.1); the
  skill refuses to run otherwise (FR-006).
- **Precondition**: caller has already started the AppHost locally
  (`dotnet run --project src/eShop.AppHost`); this command does not start it.
- **Behavior**: Enumerates reachable service endpoints from the Aspire dashboard at
  `target-url`, attempts malformed/boundary/misuse scenarios (FR-005), recording every
  attempted `AdversarialScenario` (including `outcome: no-issue` ones — Acceptance Scenario
  4, User Story 3) and any resulting `Finding` with `source: adversarial`.
- **Output**: Findings report filtered to `source: adversarial`, or — if none produced — an
  explicit list of scenarios attempted with a "no runtime issue found" statement.
- **Failure mode**: if the target crashes mid-run, the crash itself becomes a `Finding`
  (`severity: critical`, `source: adversarial`) and all `Finding`/`AdversarialScenario`
  records collected before the crash are still written (Edge Cases §4) — the `ReviewRun`
  record is written with `failedPartway: true`.

## `/security-findings-ack <finding-id>`

- **Input**: `finding-id` — a `Finding.id`.
- **Behavior**: Sets that finding's `status` to `acknowledged` in `findings/findings.json`
  (FR-008). Does not delete or hide the record.
- **Output**: Confirmation of the new status; the finding remains visible in
  `/security-report` under an "acknowledged" grouping.

## `/security-findings-link <finding-id> <finding-id> [...]`

- **Input**: two or more `Finding.id`s.
- **Behavior**: Sets `relatedFindingIds` bidirectionally across the given findings in
  `findings/findings.json`, marking them as the same underlying issue (Edge Case §2 — e.g.,
  the code review flags a missing authorization check and the adversarial capability
  independently exploits it at runtime).
- **Output**: Confirmation of the link; `/security-report` subsequently renders the linked
  findings as one consolidated entry with multiple "detected by" tags instead of separate
  rows.

## `/security-report`

- **Input**: none.
- **Behavior**: Regenerates `findings/report.md` from the current `findings/findings.json`
  (FR-009) — all three capabilities' findings together, grouped by severity, with
  `relatedFindingIds` collapsed into single consolidated entries (Edge Cases §2).
- **Output**: The consolidated report, prioritized across all three sources, not three
  separate lists.
