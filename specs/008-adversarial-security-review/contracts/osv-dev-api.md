# Contract: OSV.dev Integration (external dependency, per Clarifications 2026-07-13)

The dependency/CVE review capability (FR-003/FR-004) is the only part of this feature that
calls an external service.

## Endpoint

`POST https://api.osv.dev/v1/querybatch` (no API key required).

## Request shape (per Dependency Component resolved from the repo)

```json
{
  "queries": [
    { "package": { "name": "<nuget-package-name>", "ecosystem": "NuGet" }, "version": "<resolved-version>" }
  ]
}
```

Container/infra components (redis, rabbitmq, postgres images from
`src/eShop.AppHost/Program.cs`) are queried the same way against their respective OSV.dev
ecosystem where one exists; components with no matching OSV.dev ecosystem are still
inventoried as a `DependencyComponent` (per FR-003's "inventories... third-party
components") but produce no `VulnerabilityMatch` and are reported as "no known-vulnerability
data source available" rather than silently omitted.

## Response handling

- Each returned vulnerability ID is fetched in full via `GET https://api.osv.dev/v1/vulns/{id}`
  when detail (affected ranges, severity) beyond the batch summary is needed.
- Batch size limits and rate-limit backoff are an implementation detail of the
  dependency-review skill's script, not a contract concern — no client-visible behavior
  depends on batching strategy.

## Failure handling

- If OSV.dev is unreachable, the run fails cleanly with an explicit error in the report
  (never a silent empty dependency-review result) and `ReviewRun.failedPartway: true`.
