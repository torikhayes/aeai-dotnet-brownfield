#!/usr/bin/env python3
"""Regenerate findings/report.md from findings.json (FR-007, FR-009, Edge Case §2).

Groups findings by severity across all three `source` values. Findings linked via
`relatedFindingIds` are rendered once, with a "Detected by" line listing every
contributing source, instead of once per source (Edge Case §2).

Also supports rendering a single capability's section on its own (`--capability`),
used by each review skill to drop a dated per-capability snapshot file into the repo
(specs/008-adversarial-security-review/findings/YYYYMMDD-<capability-slug>.md) in
addition to regenerating the always-current consolidated `report.md`.
"""
import argparse
import json
import os

SEVERITY_ORDER = ["critical", "high", "medium", "low"]
SOURCE_LABEL = {"code": "Code Review", "dependency": "Dependency/CVE Review", "adversarial": "Adversarial Review"}


def _load(path):
    if not os.path.isfile(path):
        return []
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().strip()
        return json.loads(content) if content else []


def _group_linked(findings):
    """Collapse findings connected via relatedFindingIds into single groups."""
    by_id = {f["id"]: f for f in findings}
    seen = set()
    groups = []
    for f in findings:
        if f["id"] in seen:
            continue
        group = [f["id"]]
        seen.add(f["id"])
        frontier = list(f.get("relatedFindingIds", []))
        while frontier:
            rid = frontier.pop()
            if rid in seen or rid not in by_id:
                continue
            seen.add(rid)
            group.append(rid)
            frontier.extend(by_id[rid].get("relatedFindingIds", []))
        groups.append([by_id[i] for i in group])
    return groups


def _render_group(group):
    primary = min(group, key=lambda f: SEVERITY_ORDER.index(f["severity"]))
    sources = sorted({SOURCE_LABEL[f["source"]] for f in group})
    lines = [f"### {primary['title']} — {primary['severity'].upper()}", ""]
    lines.append(f"- **Status**: {primary['status']}")
    lines.append(f"- **Detected by**: {', '.join(sources)}")
    for f in group:
        lines.append(f"- **[{SOURCE_LABEL[f['source']]}] {f['id']}**: {f['description']}")
        lines.append(f"  - Evidence/reproduction: {f['evidence']}")
        if f.get("relevance") and f["relevance"] != "n/a":
            lines.append(f"  - Relevance: {f['relevance']}")
    lines.append("")
    return "\n".join(lines)


def _most_recent_run_with_capability(runs, capability):
    matching = [r for r in runs if capability in r.get("capabilities", [])]
    return max(matching, key=lambda r: r["timestamp"]) if matching else None


def _render_source_section(source, findings, runs, heading_level="##"):
    """Render one capability's own section — reused by both the full consolidated
    report and a standalone per-capability dated snapshot. Pass heading_level=None to
    omit the section heading (e.g. when the caller already printed an equivalent title)."""
    source_findings = [f for f in findings if f["source"] == source]
    lines = [f"{heading_level} {SOURCE_LABEL[source]}", ""] if heading_level else []

    last_run = _most_recent_run_with_capability(runs, source)

    if not source_findings:
        if last_run is None:
            lines.append("_No findings from this capability have been recorded yet._")
            lines.append("")
        else:
            lines.append("_No findings from this capability's most recent run._")
            lines.append("")
            # Acceptance Scenario 4 (User Story 3): a clean run must still report
            # what was attempted, not report nothing. Scenarios are only recorded
            # for the adversarial capability today, but this applies generically to
            # any future capability that records attempted-but-clean activity.
            scenarios = last_run.get("scenarios", [])
            if scenarios:
                lines.append(f"Scenarios attempted in run `{last_run['id']}` ({last_run['timestamp']}):")
                lines.append("")
                for s in scenarios:
                    lines.append(f"- **{s['id']}** [{s['targetService']}] {s['description']} — outcome: {s['outcome']}")
                lines.append("")
            if last_run.get("failedPartway"):
                lines.append(f"⚠️ Run `{last_run['id']}` did not complete normally (crashed/failed partway through) — findings collected before the failure are listed above; this run's failure is itself recorded as a finding if one was raised for it.")
                lines.append("")
    else:
        for f in sorted(source_findings, key=lambda x: SEVERITY_ORDER.index(x["severity"])):
            lines.append(f"- **{f['id']}** [{f['severity'].upper()}] {f['title']} — status: {f['status']}")
            lines.append(f"  - {f['description']}")
            lines.append(f"  - Evidence/reproduction: {f['evidence']}")
            if f.get("relevance") and f["relevance"] != "n/a":
                lines.append(f"  - Relevance: {f['relevance']}")
        lines.append("")

    return lines


def render(findings_dir):
    findings = _load(os.path.join(findings_dir, "findings.json"))
    runs = _load(os.path.join(findings_dir, "runs.json"))

    lines = ["# Security Review — Consolidated Findings Report", ""]

    for source in ("code", "dependency", "adversarial"):
        lines.extend(_render_source_section(source, findings, runs))

    lines.append("## All Findings (grouped by severity, linked findings collapsed)")
    lines.append("")

    if not findings:
        lines.append("_Zero findings across all three review capabilities._")
        lines.append("")
    else:
        groups = _group_linked(findings)
        groups.sort(key=lambda g: min(SEVERITY_ORDER.index(f["severity"]) for f in g))
        for group in groups:
            lines.append(_render_group(group))

    return "\n".join(lines) + "\n"


def render_capability_snapshot(findings_dir, source, snapshot_date):
    """Render a standalone dated snapshot for a single capability."""
    findings = _load(os.path.join(findings_dir, "findings.json"))
    runs = _load(os.path.join(findings_dir, "runs.json"))

    lines = [f"# Security Review — {SOURCE_LABEL[source]} — {snapshot_date}", ""]
    lines.extend(_render_source_section(source, findings, runs, heading_level=None))
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(prog="security-report-render")
    parser.add_argument("--findings-dir", required=True)
    parser.add_argument("--out", required=True, help="path to write the report")
    parser.add_argument(
        "--capability",
        choices=["code", "dependency", "adversarial"],
        default=None,
        help="if set, render only this capability's section as a standalone snapshot instead of the full consolidated report",
    )
    parser.add_argument(
        "--snapshot-date",
        default=None,
        help="YYYYMMDD label used in the snapshot's title (required with --capability)",
    )
    args = parser.parse_args()

    if args.capability:
        if not args.snapshot_date:
            parser.error("--snapshot-date is required when --capability is set")
        report = render_capability_snapshot(args.findings_dir, args.capability, args.snapshot_date)
    else:
        report = render(args.findings_dir)

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(report)
    print(json.dumps({"path": args.out}))


if __name__ == "__main__":
    main()
