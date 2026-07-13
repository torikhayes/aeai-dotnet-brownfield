#!/usr/bin/env bash
# Validates that a target URL resolves to a loopback address (FR-006, SC-004). Exits 0
# and prints nothing if the target is loopback; exits 1 with an explanatory message on
# stderr otherwise. Must be called *before* any adversarial scenario executes.
#
# Usage:
#   security-assert-loopback.sh <target-url>
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "ERROR: usage: security-assert-loopback.sh <target-url>" >&2
    exit 1
fi

TARGET_URL="$1"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required by security-assert-loopback.sh but was not found on PATH." >&2
    exit 1
fi

python3 - "$TARGET_URL" <<'PYEOF'
import ipaddress
import socket
import sys
from urllib.parse import urlsplit

url = sys.argv[1]
parsed = urlsplit(url)
host = parsed.hostname

if not host:
    print(f"ERROR: could not parse a hostname out of '{url}'.", file=sys.stderr)
    sys.exit(1)

def is_loopback(candidate_host):
    try:
        addr = ipaddress.ip_address(candidate_host)
        return addr.is_loopback
    except ValueError:
        pass
    try:
        resolved = socket.gethostbyname(candidate_host)
        return ipaddress.ip_address(resolved).is_loopback
    except (socket.gaierror, ValueError):
        return False

if host == "localhost" or is_loopback(host):
    sys.exit(0)

print(
    f"ERROR: target '{url}' (host '{host}') does not resolve to a loopback address. "
    "The adversarial review capability refuses to run against anything but a local, "
    "isolated instance (FR-006) — it must never target a shared or production environment.",
    file=sys.stderr,
)
sys.exit(1)
PYEOF
