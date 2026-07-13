#!/usr/bin/env python3
"""Deterministic findings/review-run persistence for the security review skills.

This module backs `security-findings-store.sh`. It is plain Python (not bash+jq)
because jq is not guaranteed to be present (this repo's own `.specify/scripts/bash/
common.sh` already falls back to python3 for the same reason), and the merge /
status-transition logic here is easier to get right in a real language than in
bash+jq string munging.

Schema: see specs/008-adversarial-security-review/data-model.md.
"""
import argparse
import json
import os
import sys
import uuid
from datetime import datetime, timezone

SEVERITIES = ("critical", "high", "medium", "low")
SOURCES = ("code", "dependency", "adversarial")
STATUSES = ("new", "acknowledged", "resolved")
RELEVANCES = ("reachable", "not-reachable", "unknown", "n/a")


def _now_iso():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _findings_path(findings_dir):
    return os.path.join(findings_dir, "findings.json")


def _runs_path(findings_dir):
    return os.path.join(findings_dir, "runs.json")


def _load(path):
    if not os.path.isfile(path):
        return []
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().strip()
        return json.loads(content) if content else []


def _save(path, records):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(records, f, indent=2, sort_keys=False)
        f.write("\n")


def _next_seq_id(records, prefix):
    existing = [
        int(r["id"].split("-")[-1])
        for r in records
        if r.get("id", "").startswith(prefix + "-") and r["id"].split("-")[-1].isdigit()
    ]
    return f"{prefix}-{(max(existing) + 1) if existing else 1:04d}"


def cmd_start_run(args):
    findings_dir = args.findings_dir
    runs = _load(_runs_path(findings_dir))
    run_id = f"run-{_now_iso().replace(':', '-')}-{uuid.uuid4().hex[:6]}"
    record = {
        "id": run_id,
        "timestamp": _now_iso(),
        "capabilities": args.capabilities.split(","),
        "scope": args.scope,
        "trigger": args.trigger,
        "findingIds": [],
        "scenarios": [],
        "failedPartway": False,
    }
    runs.append(record)
    _save(_runs_path(findings_dir), runs)
    print(json.dumps({"id": run_id}))


def _dedupe_key(finding):
    return (finding["source"], finding["title"], finding["evidence"])


def cmd_upsert_finding(args):
    findings_dir = args.findings_dir
    findings = _load(_findings_path(findings_dir))
    runs = _load(_runs_path(findings_dir))

    run = next((r for r in runs if r["id"] == args.run_id), None)
    if run is None:
        print(f"ERROR: unknown run id '{args.run_id}'", file=sys.stderr)
        sys.exit(1)

    candidate_key = (args.source, args.title, args.evidence)
    existing = next(
        (f for f in findings if _dedupe_key(f) == candidate_key and f["status"] != "resolved"),
        None,
    )

    if existing is not None:
        existing["lastSeenRunId"] = args.run_id
        existing["description"] = args.description
        existing["severity"] = args.severity
        if args.relevance:
            existing["relevance"] = args.relevance
        is_new = False
        finding_id = existing["id"]
    else:
        finding_id = _next_seq_id(findings, {"code": "code", "dependency": "dep", "adversarial": "adv"}[args.source])
        new_finding = {
            "id": finding_id,
            "source": args.source,
            "title": args.title,
            "description": args.description,
            "severity": args.severity,
            "evidence": args.evidence,
            "relevance": args.relevance if args.relevance else "n/a",
            "status": "new",
            "relatedFindingIds": [],
            "firstSeenRunId": args.run_id,
            "lastSeenRunId": args.run_id,
            "acknowledgedBy": None,
            "acknowledgedAt": None,
        }
        findings.append(new_finding)
        is_new = True

    if finding_id not in run["findingIds"]:
        run["findingIds"].append(finding_id)

    _save(_findings_path(findings_dir), findings)
    _save(_runs_path(findings_dir), runs)

    result = next(f for f in findings if f["id"] == finding_id)
    print(json.dumps({"id": finding_id, "status": result["status"], "isNew": is_new}))


def cmd_finalize_run(args):
    findings_dir = args.findings_dir
    findings = _load(_findings_path(findings_dir))
    runs = _load(_runs_path(findings_dir))

    run = next((r for r in runs if r["id"] == args.run_id), None)
    if run is None:
        print(f"ERROR: unknown run id '{args.run_id}'", file=sys.stderr)
        sys.exit(1)

    resolved_ids = []
    if args.full:
        for f in findings:
            if (
                f["source"] in run["capabilities"]
                and f["status"] != "resolved"
                and f["lastSeenRunId"] != args.run_id
            ):
                f["status"] = "resolved"
                resolved_ids.append(f["id"])

    _save(_findings_path(findings_dir), findings)
    print(json.dumps({"resolvedFindingIds": resolved_ids}))


def cmd_add_scenario(args):
    """Record an AdversarialScenario (data-model.md) against a run — including
    outcome='no-issue' ones, so a clean adversarial run still reports what it attempted
    (spec.md User Story 3, Acceptance Scenario 4) instead of reporting nothing."""
    findings_dir = args.findings_dir
    runs = _load(_runs_path(findings_dir))
    run = next((r for r in runs if r["id"] == args.run_id), None)
    if run is None:
        print(f"ERROR: unknown run id '{args.run_id}'", file=sys.stderr)
        sys.exit(1)
    if args.outcome == "finding" and not args.finding_id:
        print("ERROR: --finding-id is required when --outcome finding", file=sys.stderr)
        sys.exit(1)

    scenario_id = f"scn-{len(run.setdefault('scenarios', [])) + 1:04d}"
    scenario = {
        "id": scenario_id,
        "description": args.description,
        "targetService": args.target_service,
        "outcome": args.outcome,
        "findingId": args.finding_id if args.outcome == "finding" else None,
    }
    run["scenarios"].append(scenario)
    _save(_runs_path(findings_dir), runs)
    print(json.dumps({"id": scenario_id}))


def cmd_mark_failed(args):
    findings_dir = args.findings_dir
    runs = _load(_runs_path(findings_dir))
    run = next((r for r in runs if r["id"] == args.run_id), None)
    if run is None:
        print(f"ERROR: unknown run id '{args.run_id}'", file=sys.stderr)
        sys.exit(1)
    run["failedPartway"] = True
    _save(_runs_path(findings_dir), runs)
    print(json.dumps({"id": args.run_id, "failedPartway": True}))


def cmd_ack(args):
    findings_dir = args.findings_dir
    findings = _load(_findings_path(findings_dir))
    finding = next((f for f in findings if f["id"] == args.finding_id), None)
    if finding is None:
        print(f"ERROR: unknown finding id '{args.finding_id}'", file=sys.stderr)
        sys.exit(1)
    finding["status"] = "acknowledged"
    finding["acknowledgedBy"] = args.by
    finding["acknowledgedAt"] = _now_iso()
    _save(_findings_path(findings_dir), findings)
    print(json.dumps({"id": finding["id"], "status": finding["status"]}))


def cmd_link(args):
    findings_dir = args.findings_dir
    findings = _load(_findings_path(findings_dir))
    ids = args.finding_ids.split(",")
    by_id = {f["id"]: f for f in findings}
    for fid in ids:
        if fid not in by_id:
            print(f"ERROR: unknown finding id '{fid}'", file=sys.stderr)
            sys.exit(1)
    for fid in ids:
        related = set(by_id[fid].get("relatedFindingIds", []))
        related.update(other for other in ids if other != fid)
        by_id[fid]["relatedFindingIds"] = sorted(related)
    _save(_findings_path(findings_dir), findings)
    print(json.dumps({"linked": ids}))


def main():
    parser = argparse.ArgumentParser(prog="security-findings-store")
    parser.add_argument("--findings-dir", required=True)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("start-run")
    p.add_argument("--capabilities", required=True, help="comma-separated: code,dependency,adversarial")
    p.add_argument("--scope", required=True)
    p.add_argument("--trigger", required=True, choices=["manual"])
    p.set_defaults(func=cmd_start_run)

    p = sub.add_parser("upsert-finding")
    p.add_argument("--run-id", required=True)
    p.add_argument("--source", required=True, choices=SOURCES)
    p.add_argument("--title", required=True)
    p.add_argument("--description", required=True)
    p.add_argument("--severity", required=True, choices=SEVERITIES)
    p.add_argument("--evidence", required=True)
    p.add_argument("--relevance", choices=RELEVANCES, default=None)
    p.set_defaults(func=cmd_upsert_finding)

    p = sub.add_parser("add-scenario")
    p.add_argument("--run-id", required=True)
    p.add_argument("--description", required=True)
    p.add_argument("--target-service", required=True)
    p.add_argument("--outcome", required=True, choices=["no-issue", "finding"])
    p.add_argument("--finding-id", default=None)
    p.set_defaults(func=cmd_add_scenario)

    p = sub.add_parser("finalize-run")
    p.add_argument("--run-id", required=True)
    p.add_argument("--full", type=lambda s: s.lower() == "true", default=True)
    p.set_defaults(func=cmd_finalize_run)

    p = sub.add_parser("mark-failed")
    p.add_argument("--run-id", required=True)
    p.set_defaults(func=cmd_mark_failed)

    p = sub.add_parser("ack")
    p.add_argument("--finding-id", required=True)
    p.add_argument("--by", required=True)
    p.set_defaults(func=cmd_ack)

    p = sub.add_parser("link")
    p.add_argument("--finding-ids", required=True, help="comma-separated, 2 or more")
    p.set_defaults(func=cmd_link)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
