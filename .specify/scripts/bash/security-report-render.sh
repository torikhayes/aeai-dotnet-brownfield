#!/usr/bin/env bash
# Thin CLI wrapper around security_report_render.py (see security-findings-store.sh
# for why the real logic lives in Python rather than bash+jq).
#
# Usage:
#   security-report-render.sh --findings-dir DIR --out DIR/report.md
set -euo pipefail

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_DIR="$(CDPATH="" cd "$SCRIPT_DIR/../python" && pwd)"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required by security-report-render.sh but was not found on PATH." >&2
    exit 1
fi

exec python3 "$PYTHON_DIR/security_report_render.py" "$@"
