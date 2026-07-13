# Data Model: Adversarial Code & CVE Review

Source of truth: git-tracked JSON files under
`specs/008-adversarial-security-review/findings/` (see `research.md` §4). No new database.

## Finding

A single reported issue, from any of the three review capabilities.

| Field | Type | Notes |
|---|---|---|
| `id` | string | Stable identifier, e.g. `code-0007`, `dep-0003`, `adv-0002` (prefix encodes source capability + sequence). |
| `source` | enum: `code` \| `dependency` \| `adversarial` | Which review capability produced this finding. |
| `title` | string | Short one-line summary. |
| `description` | string | Full description of the issue. |
| `severity` | enum: `critical` \| `high` \| `medium` \| `low` | Per FR-001/FR-005/FR-007. |
| `evidence` | string | For `code`: file + line reference. For `dependency`: package/ecosystem/version + OSV vulnerability ID(s). For `adversarial`: exact reproduction steps (request sequence). |
| `relevance` | enum: `reachable` \| `not-reachable` \| `unknown` \| `n/a` | Required for `dependency` findings (FR-004); `n/a` for `code`/`adversarial`. |
| `status` | enum: `new` \| `acknowledged` \| `resolved` | Per FR-008. |
| `relatedFindingIds` | string[] | Links to other `Finding.id`s representing the same underlying issue (Edge Case #2). |
| `firstSeenRunId` | string | `ReviewRun.id` where this finding first appeared. |
| `lastSeenRunId` | string | `ReviewRun.id` of the most recent run that still reproduced this finding. |
| `acknowledgedBy` / `acknowledgedAt` | string / timestamp | Set when `status` transitions to `acknowledged`. |

Validation rules:
- `relevance` MUST be set (not `n/a`) when `source == dependency` (FR-004).
- A finding whose `status` is `acknowledged` and reappears unchanged on a later run keeps
  `status: acknowledged` and only updates `lastSeenRunId` — it MUST NOT flip back to `new`
  (FR-008, SC-003).
- A finding not seen on the most recent run for its source/scope combination transitions to
  `resolved` automatically (Acceptance Scenario 3 under User Story 2; Acceptance Scenario 4
  under User Story 1).

## Dependency Component

A third-party library or infrastructure/container image the application uses.

| Field | Type | Notes |
|---|---|---|
| `name` | string | e.g. `Npgsql.EntityFrameworkCore.PostgreSQL`, `redis`, `rabbitmq`. |
| `ecosystem` | enum: `nuget` \| `container` | OSV.dev ecosystem for library lookups; `container` for Aspire-managed infra images. |
| `version` | string | Resolved version in use. |
| `usedBy` | string[] | Project(s)/service(s) referencing this component, e.g. `["Catalog.API", "Ordering.API"]`. |
| `shipsInRuntime` | boolean | `false` for dev/build-only tooling dependencies (Edge Cases §1 — still reported, lower relevance). |

## Vulnerability Match

Links a Dependency Component to a known OSV.dev vulnerability record.

| Field | Type | Notes |
|---|---|---|
| `id` | string | OSV vulnerability ID (e.g. `GHSA-...`, `OSV-...`). |
| `dependencyComponentName` + `dependencyComponentVersion` | string | Composite key back to Dependency Component. |
| `severity` | enum: `critical` \| `high` \| `medium` \| `low` | Derived from OSV.dev's reported severity/CVSS where present. |
| `relevance` | enum: `reachable` \| `not-reachable` \| `unknown` | Same semantics as `Finding.relevance`; a Vulnerability Match with `relevance` set produces exactly one `Finding` with `source: dependency`. |
| `findingId` | string | The `Finding.id` this match was rendered as. |

## Adversarial Scenario

A misuse/attack scenario attempted by the adversarial capability.

| Field | Type | Notes |
|---|---|---|
| `id` | string | e.g. `scn-0014`. |
| `description` | string | What was attempted (malformed input, boundary, sequence misuse, etc.). |
| `targetService` | string | Which service/endpoint was exercised. |
| `outcome` | enum: `no-issue` \| `finding` | Per Acceptance Scenario 4 under User Story 3 — `no-issue` scenarios are recorded, not omitted. |
| `findingId` | string \| null | Set when `outcome == finding`. |

## Review Run

A single execution of one or more review capabilities.

| Field | Type | Notes |
|---|---|---|
| `id` | string | e.g. `run-2026-07-13T18-00-00Z`. |
| `timestamp` | ISO 8601 string | |
| `capabilities` | enum[]: subset of `code` \| `dependency` \| `adversarial` | Which capabilities ran in this invocation (FR-010: each runnable independently). |
| `scope` | string | `"full"` or a change-set reference (e.g. PR number / commit range) for `code`; `"full"` for `dependency`/`adversarial`. |
| `trigger` | enum: `manual` | Only value in this phase (FR-011); future values (`automatic-on-pull-request`) are additive, not a schema change. |
| `findingIds` | string[] | Findings produced or reconfirmed by this run. |
| `scenarios` | Adversarial Scenario[] | Every scenario attempted during this run, embedded inline (1 run : many scenarios) — including `outcome: no-issue` ones, so a clean adversarial run still reports what was attempted rather than nothing (Acceptance Scenario 4, User Story 3). Only populated for `adversarial` runs; empty for `code`/`dependency` runs. |
| `failedPartway` | boolean | `true` if the run did not complete normally (Edge Cases §4 — a crash mid-run is itself a finding, and findings collected so far are still persisted). |

## Relationships

```text
ReviewRun 1---* Finding            (via findingIds)
Finding *---* Finding              (via relatedFindingIds, same underlying issue)
DependencyComponent 1---* VulnerabilityMatch
VulnerabilityMatch 1---1 Finding   (source: dependency)
AdversarialScenario 0..1---1 Finding (source: adversarial, only when outcome: finding)
```
