#!/usr/bin/env bash
# Thin CLI wrapper around security_inventory_dependencies.py (see
# security-findings-store.sh for why the real logic lives in Python).
#
# Usage:
#   security-inventory-dependencies.sh --repo-root DIR --out DIR/dependency-components.json
set -euo pipefail

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_DIR="$(CDPATH="" cd "$SCRIPT_DIR/../python" && pwd)"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required by security-inventory-dependencies.sh but was not found on PATH." >&2
    exit 1
fi

exec python3 "$PYTHON_DIR/security_inventory_dependencies.py" "$@"
