#!/usr/bin/env bash
# Thin CLI wrapper around security_findings_store.py.
#
# jq is not guaranteed to be present (see common.sh's own jq -> python3 fallback),
# and the merge/status-transition logic here is easier to get right in a real
# language than in bash+jq string munging, so the actual logic lives in the
# co-located Python module. This script is the stable entrypoint contract.
#
# Usage:
#   security-findings-store.sh --findings-dir DIR start-run --capabilities code --scope full --trigger manual
#   security-findings-store.sh --findings-dir DIR upsert-finding --run-id ID --source code --title T --description D --severity high --evidence E [--relevance reachable]
#   security-findings-store.sh --findings-dir DIR finalize-run --run-id ID [--full true|false]
#   security-findings-store.sh --findings-dir DIR mark-failed --run-id ID
#   security-findings-store.sh --findings-dir DIR ack --finding-id ID --by NAME
#   security-findings-store.sh --findings-dir DIR link --finding-ids ID1,ID2[,ID3...]
set -euo pipefail

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required by security-findings-store.sh but was not found on PATH." >&2
    exit 1
fi

exec python3 "$SCRIPT_DIR/security_findings_store.py" "$@"
