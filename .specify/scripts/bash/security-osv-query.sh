#!/usr/bin/env bash
# Thin CLI wrapper around security_osv_query.py (see security-findings-store.sh for
# why the real logic lives in Python).
#
# Usage:
#   security-osv-query.sh --components DIR/dependency-components.json --out DIR/vulnerability-matches.json [--osv-base-url URL]
set -euo pipefail

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_DIR="$(CDPATH="" cd "$SCRIPT_DIR/../python" && pwd)"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required by security-osv-query.sh but was not found on PATH." >&2
    exit 1
fi

exec python3 "$PYTHON_DIR/security_osv_query.py" "$@"
