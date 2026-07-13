#!/usr/bin/env python3
"""Batch-query OSV.dev for NuGet DependencyComponent records (FR-003/FR-004), producing
VulnerabilityMatch records. See specs/008-adversarial-security-review/contracts/osv-dev-api.md.

`--osv-base-url` defaults to the real https://api.osv.dev but is overridable so tests can
point this at a local mock HTTP server instead of hitting the network.
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.request

DEFAULT_BASE_URL = "https://api.osv.dev"
TIMEOUT_SECONDS = 30

# OSV.dev ecosystem names; only components we know how to map are queried. Anything else
# (e.g. our "container" ecosystem) is inventoried but intentionally not queried here — see
# contracts/osv-dev-api.md's "no matching OSV.dev ecosystem" handling.
ECOSYSTEM_MAP = {"nuget": "NuGet"}


def _load_components(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def _post_json(url, payload):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=TIMEOUT_SECONDS) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _get_json(url):
    with urllib.request.urlopen(url, timeout=TIMEOUT_SECONDS) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _severity_from_vuln_detail(detail):
    """Map an OSV.dev vuln record to our critical/high/medium/low scale.

    OSV.dev/GHSA records usually carry a direct qualitative rating at
    `database_specific.severity` (LOW/MODERATE/HIGH/CRITICAL) — prefer that. Only fall
    back to a coarse CVSS vector-string heuristic when it's absent (e.g. NVD-sourced
    records without a GHSA qualitative rating).
    """
    qualitative = str(detail.get("database_specific", {}).get("severity", "")).lower()
    if qualitative == "moderate":
        return "medium"
    if qualitative in ("critical", "high", "medium", "low"):
        return qualitative

    for sev in detail.get("severity", []):
        score = sev.get("score", "")
        if "CVSS" in sev.get("type", ""):
            # Extremely coarse vector-string heuristic: a full CVSS parser is out of
            # scope here; this is refined by the Agent-driven relevance-assessment step
            # in security-cve-review, not by this deterministic script.
            if "/AV:N" in score and "/C:H" in score:
                return "critical"
            return "high"
    return "medium"


def query(components, base_url):
    nuget_components = [c for c in components if c.get("ecosystem") in ECOSYSTEM_MAP]
    skipped = [c for c in components if c.get("ecosystem") not in ECOSYSTEM_MAP]

    matches = []
    if nuget_components:
        queries = [
            {"package": {"name": c["name"], "ecosystem": ECOSYSTEM_MAP[c["ecosystem"]]}, "version": c["version"]}
            for c in nuget_components
        ]
        try:
            batch_response = _post_json(f"{base_url}/v1/querybatch", {"queries": queries})
        except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
            print(f"ERROR: OSV.dev unreachable at {base_url}: {exc}", file=sys.stderr)
            sys.exit(1)

        for component, result in zip(nuget_components, batch_response.get("results", [])):
            for vuln_summary in result.get("vulns", []):
                vuln_id = vuln_summary["id"]
                try:
                    detail = _get_json(f"{base_url}/v1/vulns/{vuln_id}")
                except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
                    print(f"ERROR: OSV.dev unreachable at {base_url}: {exc}", file=sys.stderr)
                    sys.exit(1)
                matches.append(
                    {
                        "id": vuln_id,
                        "dependencyComponentName": component["name"],
                        "dependencyComponentVersion": component["version"],
                        "severity": _severity_from_vuln_detail(detail),
                        "relevance": "unknown",
                        "findingId": None,
                    }
                )

    return matches, [c["name"] for c in skipped]


def main():
    parser = argparse.ArgumentParser(prog="security-osv-query")
    parser.add_argument("--components", required=True, help="path to dependency-components.json from security-inventory-dependencies")
    parser.add_argument("--out", required=True, help="path to write vulnerability-matches.json")
    parser.add_argument("--osv-base-url", default=DEFAULT_BASE_URL)
    args = parser.parse_args()

    components = _load_components(args.components)
    matches, skipped_no_ecosystem = query(components, args.osv_base_url)

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(matches, f, indent=2)
        f.write("\n")

    print(json.dumps({"matchCount": len(matches), "skippedNoEcosystem": skipped_no_ecosystem, "path": args.out}))


if __name__ == "__main__":
    main()
