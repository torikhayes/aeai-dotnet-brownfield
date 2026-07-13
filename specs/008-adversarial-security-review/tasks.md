---

description: "Task list template for feature implementation"
---

# Tasks: Adversarial Code & CVE Review

**Input**: Design documents from `/specs/008-adversarial-security-review/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included for the deterministic bash scripts (findings store, OSV.dev query, dependency inventory, loopback guard), per plan.md's Testing strategy and Constitution Principle V ("tests required" for non-ledger code, not strict TDD). The judgment-driven Agent behavior inside each Skill is validated via the Independent Test criteria from spec.md (manual quickstart validation), not unit tests — an LLM agent's output isn't a deterministic unit-test target.

**Organization**: Tasks are grouped by user story (US1 = code review, US2 = dependency/CVE review, US3 = adversarial review) to enable independent implementation and testing of each.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and relative to the repo root

## Path Conventions

This is tooling layered on the existing single-solution .NET repo. New files only, under:
- `.claude/skills/` — maintainer-facing Skill commands
- `.specify/scripts/bash/` — deterministic logic the skills call into
- `tests/Security.Tooling.UnitTests/` — new MSTest project for the deterministic scripts
- `specs/008-adversarial-security-review/findings/` and `review-config.json` — findings store + config

No existing `src/` service project is modified.

---

## Phase 1: Setup

**Purpose**: Scaffolding the findings store, trigger config, and test project shell.

- [X] T001 [P] Create `specs/008-adversarial-security-review/findings/findings.json` and `specs/008-adversarial-security-review/findings/runs.json`, each initialized to `[]`, matching the `Finding`/`ReviewRun` schemas in `data-model.md`.
- [X] T002 [P] Create `specs/008-adversarial-security-review/review-config.json` with one entry per capability (`codeReview`, `dependencyReview`, `adversarialReview`), each `{ "trigger": "on-demand" }`, per FR-011.
- [X] T003 [P] Create `tests/Security.Tooling.UnitTests/Security.Tooling.UnitTests.csproj` as a new MSTest project (mirroring the `MSTest.Sdk`/`Microsoft.Testing.Platform` convention already used by `tests/Ordering.UnitTests`), with one placeholder passing test to confirm the project builds and runs under `dotnet test`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared findings persistence, acknowledgement, cross-capability linking, and consolidated reporting — used by all three review capabilities (FR-007, FR-008, FR-009, FR-010).

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Implement `.specify/scripts/bash/security-findings-store.sh` in the repo: append/update `Finding` and `ReviewRun` records in `specs/008-adversarial-security-review/findings/findings.json` and `runs.json` per `data-model.md`'s validation rules — a finding not seen on the latest run for its scope auto-transitions to `resolved`; an `acknowledged` finding reconfirmed by a later run only updates `lastSeenRunId` and never flips back to `new` (FR-008, SC-003). (depends on T001)
- [X] T005 [P] Write `tests/Security.Tooling.UnitTests/FindingsStoreTests.cs` covering: insert-new-finding, reconfirm-existing-finding (lastSeenRunId updates, status unchanged), acknowledged-finding-survives-rerun (FR-008/SC-003), and finding-absent-from-latest-run-transitions-to-resolved. (depends on T003, T004)
- [X] T006 Create `.claude/skills/security-findings-ack/SKILL.md` implementing `/security-findings-ack <finding-id>`, calling `security-findings-store.sh`'s acknowledge operation and confirming the new status without deleting the record (FR-008). (depends on T004)
- [X] T007 Implement `.specify/scripts/bash/security-report-render.sh`: regenerate `specs/008-adversarial-security-review/findings/report.md` from `findings.json`, grouped by severity across all three `source` values, collapsing findings linked via `relatedFindingIds` into a single consolidated entry with multiple "detected by" tags (FR-007, FR-009, Edge Case §2). Must render an explicit zero-findings statement per source when none exist, never an empty section. (depends on T004)
- [X] T008 [P] Write `tests/Security.Tooling.UnitTests/ReportRenderTests.cs` covering: zero-findings explicit statement, multi-source grouping by severity, and `relatedFindingIds` collapse into one entry. (depends on T003, T007)
- [X] T009 Create `.claude/skills/security-report/SKILL.md` implementing `/security-report`, calling `security-report-render.sh` (FR-007, FR-009). (depends on T007)
- [X] T010 Extend `.specify/scripts/bash/security-findings-store.sh` (T004) with a link operation that sets `relatedFindingIds` bidirectionally across two or more `Finding.id`s in `findings.json`, so the same underlying issue found by more than one capability can be marked as such rather than left as unrelated duplicates (Edge Case §2 — this is the producer that `security-report-render.sh`'s (T007) collapsing logic was previously missing). (depends on T004)
- [X] T011 Create `.claude/skills/security-findings-link/SKILL.md` implementing `/security-findings-link <finding-id> <finding-id> [...]`, calling the link operation from T010 (Edge Case §2). (depends on T010)
- [X] T012 [P] Extend `tests/Security.Tooling.UnitTests/FindingsStoreTests.cs` (T005) with cases for the link operation: bidirectional linking, and — combined with `ReportRenderTests.cs` (T008) — that two linked findings render as one consolidated `/security-report` entry. (depends on T005, T008, T010)

**Checkpoint**: Findings store, acknowledgement, cross-capability linking, and consolidated report are all in place — any user story can now persist, link, and surface findings.

---

## Phase 3: User Story 1 - Automated Code & Vulnerability Review (Priority: P1) 🎯 MVP

**Goal**: A repeatable, on-demand code review that surfaces concrete bugs, vulnerabilities, and likely-issue patterns, scoped to the full codebase or a specific change set.

**Independent Test**: Point `/security-code-review` at the current codebase (or a change set), confirm the report lists findings with description/location/severity, confirm a deliberately-introduced vulnerability fixture is caught, and confirm this all works with no other capability having ever been run (FR-010).

### Implementation for User Story 1

- [X] T013 [P] [US1] Create `.claude/skills/security-code-review/SKILL.md` implementing `/security-code-review [scope]`: on invocation, first reads the `codeReview` entry from `specs/008-adversarial-security-review/review-config.json` (FR-011 — trigger mode is a config read, not a hardcoded assumption; today the only defined value is `on-demand`, which this manual invocation satisfies). Defaults to full codebase, delegates bug/vulnerability/pattern-risk detection to an Agent invocation, and reports each finding with description, location, and severity (FR-001). (depends on T004, T002)
- [X] T014 [US1] Add scope resolution to `security-code-review/SKILL.md`: when `scope` is a PR number/branch/commit range, restrict the Agent's reviewed file set to `git diff`/`gh pr diff` output for that range so the report reflects only issues introduced or touched by that change set (FR-002). (depends on T013)
- [X] T015 [US1] Wire `security-code-review`'s findings through `security-findings-store.sh` (T004) to persist as `source: code` Findings, and through `security-report-render.sh` (T007) so a zero-findings run states so explicitly (Acceptance Scenario 3, US1). (depends on T013, T004, T007)
- [X] T016 [P] [US1] Create fixture `tests/Security.Tooling.UnitTests/Fixtures/IntentionalVulnerability.cs` containing one deliberately-introduced issue (e.g., unsafe deserialization of untrusted input) for manually validating US1's Independent Test.
- [X] T017 [US1] Manually validate: run `/security-code-review` against the fixture (T016) and confirm it's caught; then acknowledge that finding (T006) and re-run against unchanged code to confirm it does not resurface as new (Acceptance Scenario 4, US1); and explicitly confirm this entire flow works with `findings.json`/`runs.json` containing no `dependency`- or `adversarial`-sourced entries, demonstrating FR-010's independence requirement — per `quickstart.md`'s "Run the code review" and "Verify SC-003" sections. (depends on T015, T016, T006)

**Checkpoint**: User Story 1 is fully functional and testable independently.

---

## Phase 4: User Story 2 - Dependency CVE Monitoring & Risk Assessment (Priority: P2)

**Goal**: Identify which of the application's third-party dependencies (NuGet packages and Aspire-managed infra) have OSV.dev-published vulnerabilities that are actually relevant, not just a raw name match.

**Independent Test**: Run `/security-cve-review` and confirm the report lists matched vulnerabilities per dependency with severity and a `reachable`/`not-reachable`/`unknown` relevance call, distinguishing low-relevance findings from actionable ones, with no other capability having ever been run (FR-010).

### Implementation for User Story 2

- [X] T018 [P] [US2] Implement `.specify/scripts/bash/security-inventory-dependencies.sh`: enumerate NuGet packages from `Directory.Packages.props` crossed with every `src/**/*.csproj` and `tests/**/*.csproj`, plus Aspire-managed infra components (Redis, RabbitMQ, and the Postgres-backed `identityDb`/`catalogDb`/`orderDb`/`webhooksDb`) parsed from `src/eShop.AppHost/Program.cs`, into `DependencyComponent` records including `usedBy` and `shipsInRuntime` (FR-003).
- [X] T019 [P] [US2] Write `tests/Security.Tooling.UnitTests/InventoryTests.cs` covering NuGet package resolution against `Directory.Packages.props` and Aspire resource parsing from a `Program.cs`-shaped fixture. (depends on T003, T018)
- [X] T020 [US2] Implement `.specify/scripts/bash/security-osv-query.sh`: batch-query `POST https://api.osv.dev/v1/querybatch` and fetch detail via `GET https://api.osv.dev/v1/vulns/{id}` per `contracts/osv-dev-api.md`, producing `VulnerabilityMatch` records; on an unreachable OSV.dev, fail the run cleanly with an explicit error rather than a silent empty result. (depends on T018)
- [X] T021 [P] [US2] Write `tests/Security.Tooling.UnitTests/OsvQueryTests.cs` mocking OSV.dev responses for matched, unmatched, and unreachable-endpoint cases. (depends on T003, T020)
- [X] T022 [US2] Create `.claude/skills/security-cve-review/SKILL.md` implementing `/security-cve-review`: on invocation, first reads the `dependencyReview` entry from `review-config.json` (FR-011, mirroring T013's config-read pattern). Orchestrates `security-inventory-dependencies.sh` + `security-osv-query.sh`, delegates reachability/relevance assessment to an Agent invocation given how the dependency is actually used, and persists `source: dependency` Findings with `relevance` set via `security-findings-store.sh` (FR-004). (depends on T020, T004, T002)
- [X] T023 [US2] Handle Edge Case §1 in `security-cve-review/SKILL.md`: dependencies with `shipsInRuntime: false` (dev/build-tooling-only) are still reported, but marked lower relevance than a shipped runtime dependency rather than omitted. (depends on T022)
- [X] T024 [US2] Wire `security-cve-review`'s findings through `security-report-render.sh` (T007) so dependency findings are never presented as an undifferentiated raw match list, satisfying SC-005. (depends on T022, T007)
- [X] T025 [US2] Manually validate: run `/security-cve-review` per `quickstart.md`, then bump one flagged package to its patched version and re-run to confirm the finding auto-resolves rather than staying open (Acceptance Scenario 3, US2); and explicitly confirm this entire flow works with `findings.json`/`runs.json` containing no `code`- or `adversarial`-sourced entries, demonstrating FR-010's independence requirement. (depends on T022, T023, T024)

**Checkpoint**: User Stories 1 AND 2 both work independently.

---

## Phase 5: User Story 3 - Adversarial Runtime Testing (Priority: P3)

**Goal**: Actively try to break a running, isolated instance of the application and report any runtime issue found, with exact reproduction steps.

**Independent Test**: Run `/security-adversarial-review <loopback-url>` against a locally-running AppHost, confirm it attempts a range of malformed/boundary/misuse interactions, confirm any finding includes exact reproduction steps (or, if none, an explicit list of attempted scenarios), confirm a non-loopback URL is refused before any scenario executes (SC-004), and confirm this all works with no other capability having ever been run (FR-010).

### Implementation for User Story 3

- [X] T026 [P] [US3] Implement `.specify/scripts/bash/security-assert-loopback.sh`: validates a given `target-url` resolves to `localhost`/`127.0.0.1`, exiting non-zero for anything else, and is invoked *before* any scenario executes (FR-006, SC-004).
- [X] T027 [P] [US3] Write `tests/Security.Tooling.UnitTests/LoopbackGuardTests.cs` covering accepted (`localhost`, `127.0.0.1`) and rejected (remote host, staging URL) inputs. (depends on T003, T026)
- [X] T028 [US3] Create `.claude/skills/security-adversarial-review/SKILL.md` implementing `/security-adversarial-review <target-url>`: on invocation, first reads the `adversarialReview` entry from `review-config.json` (FR-011, mirroring T013/T022's config-read pattern), then calls `security-assert-loopback.sh` and refuses to proceed if it fails, enumerates reachable service endpoints from the Aspire dashboard at `target-url`, and delegates malformed/boundary/misuse scenario generation and execution to an Agent invocation (FR-005, FR-006). (depends on T026, T004, T002)
- [X] T029 [US3] Record every attempted `AdversarialScenario` (including `outcome: no-issue` ones) and any resulting `source: adversarial` Finding via `security-findings-store.sh`, ensuring exact reproduction steps (the request sequence) are captured for any bypass/data-integrity finding (Acceptance Scenario 2, US3). (depends on T028)
- [X] T030 [US3] Implement crash/partial-run handling in `security-adversarial-review/SKILL.md`: on a target crash mid-run, record the crash itself as a `critical` Finding, persist all Findings/Scenarios collected before the crash, and set `ReviewRun.failedPartway: true` (Edge Case §4). (depends on T029)
- [X] T031 [US3] Ensure a clean run (zero findings) still reports the full list of attempted scenarios via `security-report-render.sh` rather than reporting nothing (Acceptance Scenario 4, US3). (depends on T029, T007)
- [X] T032 [US3] Manually validate: start the AppHost locally (`dotnet run --project src/eShop.AppHost`), run `/security-adversarial-review` against its loopback URL per `quickstart.md`, confirm a remote/staging URL is refused before any scenario runs (SC-004), and explicitly confirm this entire flow works with `findings.json`/`runs.json` containing no `code`- or `dependency`-sourced entries, demonstrating FR-010's independence requirement. (depends on T026, T028, T029, T030, T031)

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Wire the new test project into CI and validate the full cross-capability picture.

- [X] T033 [P] Add `tests/Security.Tooling.UnitTests` to `eShop.slnx` and `eShop.Web.slnf` so it's picked up by the existing `dotnet build`/`dotnet test` steps in `.github/workflows/pr-validation.yml`. (depends on T003)
- [X] T034 [P] Add the six new skills to `CLAUDE.md`'s `## Skills` section, following the existing `local-setup` entry's format. (depends on T006, T009, T011, T013, T022, T028)
- [X] T035 Run `specs/008-adversarial-security-review/quickstart.md`'s "Verify SC-003" section end-to-end across all three capabilities together; then, using `/security-findings-link` (T011), link a code-review finding to an adversarial finding representing the same underlying issue, and confirm `/security-report` collapses them into one consolidated entry (Edge Case §2). (depends on T015, T024, T031, T011)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001) — BLOCKS all user stories.
- **User Stories (Phase 3-5)**: All depend on Foundational (Phase 2) completion, including the config-read pattern (T002) and the link operation (T010); independent of each other after that.
- **Polish (Phase 6)**: Depends on the user stories it references (see per-task dependencies above).

### User Story Dependencies

- **User Story 1 (P1)**: Depends only on Foundational. No dependency on US2/US3.
- **User Story 2 (P2)**: Depends only on Foundational. No dependency on US1/US3.
- **User Story 3 (P3)**: Depends only on Foundational. No dependency on US1/US2.

### Parallel Opportunities

- T001, T002, T003 (Setup) can all run in parallel.
- T005 and T008 (Foundational tests) can run in parallel with each other once their respective implementation tasks (T004, T007) land; T012 depends on both plus T010.
- Once Phase 2 is complete, all of US1 (Phase 3), US2 (Phase 4), and US3 (Phase 5) can be worked in parallel — they touch entirely disjoint new files.
- Within US2: T018/T019 (inventory) and T020/T021 (OSV query) touch different files but T020 depends on T018's output shape, so treat T018→T020 as sequential; T019 and T021 (their respective tests) can each run parallel to sibling test tasks.
- Within US3: T026/T027 (loopback guard) can run fully in parallel with early US1/US2 work.

---

## Parallel Example: Foundational Phase

```bash
# After T001 completes, in parallel:
Task: "Implement security-findings-store.sh in .specify/scripts/bash/security-findings-store.sh"
Task: "Create Security.Tooling.UnitTests.csproj in tests/Security.Tooling.UnitTests/"
```

## Parallel Example: User Stories (post-Foundational)

```bash
# Once Phase 2 is complete, three independent workstreams:
Task: "Implement security-code-review/SKILL.md (US1)"
Task: "Implement security-inventory-dependencies.sh (US2)"
Task: "Implement security-assert-loopback.sh (US3)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational — findings store, ack, link, report).
2. Complete Phase 3 (User Story 1 — code review).
3. **STOP and VALIDATE**: run `/security-code-review` against the fixture and the real repo; confirm the Independent Test passes.
4. This alone satisfies SC-001's "consolidated findings report" for one source and is demoable.

### Incremental Delivery

1. Setup + Foundational → shared findings/report/ack/link infrastructure ready.
2. Add US1 (code review) → validate independently → demo.
3. Add US2 (dependency/CVE review) → validate independently → demo; `/security-report` now shows two sources.
4. Add US3 (adversarial review) → validate independently → demo; `/security-report` now shows all three sources (SC-001 fully realized).
5. Polish: wire CI, cross-link skills docs, run the full cross-capability SC-003/Edge-Case-§2 validation (now demonstrable via `/security-findings-link`).

### Parallel Team Strategy

Once Foundational (Phase 2) is done, three people can take US1/US2/US3 independently — none of their new files overlap, and none of the three Skills call into another story's Skill.

---

## Notes

- [P] tasks touch different files with no unmet dependencies.
- [Story] labels map tasks to spec.md's US1/US2/US3 for traceability; Setup/Foundational/Polish tasks intentionally have none.
- The judgment-driven parts of each Skill (what counts as a bug, what's reachable, what scenario to try next) are Agent-invocation behavior, not deterministic code — validated via the manual quickstart steps (T017, T025, T032, T035), not asserted in unit tests.
- Every capability-invoking Skill (T013, T022, T028) reads its trigger-mode entry from `review-config.json` (T002) before running, so FR-011's "configuration setting, not hardcoded" requirement is actually exercised, not just represented by an inert file.
- `relatedFindingIds` (Edge Case §2) has both a producer (`/security-findings-link`, T010-T011) and a consumer (`security-report-render.sh`'s collapse logic, T007) — T035 is the first task that exercises both ends together.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently before continuing.
