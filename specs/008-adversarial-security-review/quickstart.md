# Quickstart: Adversarial Code & CVE Review

## Prerequisites

- Repo checked out, on any branch (findings store is git-tracked so it diffs like normal
  content).
- For the adversarial capability only: AppHost running locally —
  `dotnet run --project src/eShop.AppHost` — and reachable at a loopback URL.
- No API keys required (OSV.dev's batch/vuln endpoints are unauthenticated).

## Run the code review (User Story 1)

```text
/security-code-review            # full codebase
/security-code-review <PR-number-or-commit-range>   # scoped to a change set
```

Produces a findings report with description, location, and severity per finding — or an
explicit zero-findings statement.

## Run the dependency/CVE review (User Story 2)

```text
/security-cve-review
```

Produces a findings report of matched OSV.dev vulnerabilities across NuGet packages and
Aspire-managed infra (redis, rabbitmq, postgres), each marked `reachable` /
`not-reachable` / `unknown` relative to how this application actually uses the dependency.

## Run the adversarial review (User Story 3)

```text
dotnet run --project src/eShop.AppHost   # in a separate terminal, if not already running
/security-adversarial-review http://localhost:<aspire-dashboard-port>
```

Refuses to run against anything but a loopback URL. Reports any crash, unhandled error,
authorization bypass, or data-integrity issue found, each with exact reproduction steps — or
a list of scenarios attempted with no findings.

## Acknowledge a finding

```text
/security-findings-ack <finding-id>
```

## Link findings that are the same underlying issue

```text
/security-findings-link <finding-id> <finding-id>
```

Use when two capabilities independently surface the same root cause (e.g., the code review
flags a missing authorization check and the adversarial review exploits it at runtime) — the
linked findings then render as one consolidated entry in `/security-report` instead of two
unrelated rows.

## View everything together

```text
/security-report
```

Regenerates `specs/008-adversarial-security-review/findings/report.md` from all three
capabilities' findings, prioritized together, with duplicate underlying issues collapsed.

## Historical dated snapshots

Every time `/security-code-review`, `/security-cve-review`, or `/security-adversarial-review`
runs, it also drops a git-tracked snapshot of just its own findings into
`specs/008-adversarial-security-review/findings/YYYYMMDD-<slug>.md` (`code-review` /
`cve-review` / `adversarial-review`), alongside regenerating `report.md`. Running the same
capability again later the same day overwrites that day's file; a different day gets a new
one. This gives a browsable audit trail as plain repo files — `git log` on that directory
shows every day a given capability found something, without needing to reconstruct history
from `findings.json`.

## Verify SC-003 (no repeat-triage)

1. Run any capability, note a finding's `id`.
2. `/security-findings-ack <finding-id>`.
3. Re-run the same capability against unchanged code/dependencies.
4. Confirm that finding does not reappear under a "new" grouping in `/security-report`.
