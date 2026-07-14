#!/usr/bin/env bash
# =============================================================================
# detect-ports.sh — Discover the live Aspire-assigned ports and connection
# secrets for the running eShop stack and (optionally) update config.env.
#
# Aspire assigns new random ports (and, for containers like postgres, new
# host-port mappings) on every `dotnet run` / `dotnet watch run` of
# eShop.AppHost, so the values baked into config.env go stale as soon as the
# app is restarted. This script re-derives them by:
#
#   1. Finding the live process for each known .NET project via its build
#      output path (artifacts/bin/<Project>/debug/<Project>), listing its
#      LISTEN sockets via lsof, and probing each port with curl to determine
#      whether it serves https or http (Kestrel commonly binds both).
#   2. Finding the postgres container's host port mapping via `docker ps`
#      (Colima runs the actual container; DCP just records the mapping).
#   3. Reading the auto-generated postgres password from the AppHost
#      project's user-secrets — this is Aspire's documented mechanism for
#      persisting generated values of parameter resources (e.g. the
#      `postgres-password` parameter), not a workaround.
#
# USAGE:
#   ./detect-ports.sh              Print discovered ports/URLs and PG_CONN
#   ./detect-ports.sh --update     Also rewrite PP, IDENTITY, PG_CONN in config.env
#
# Requires: lsof, curl, ps, docker (via Colima), dotnet (all already used by this repo).
# =============================================================================

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
CONFIG_ENV="$SCRIPT_DIR/config.env"
UPDATE=false

if [[ "${1:-}" == "--update" ]]; then
  UPDATE=true
fi

# Project name -> build output folder name (they match 1:1 in this repo).
PROJECTS=(
  "Identity.API"
  "PaymentProcessor"
  "Catalog.API"
  "Basket.API"
  "Ordering.API"
  "Webhooks.API"
  "WebApp"
  "OrderProcessor"
  "WebhookClient"
  "eShop.AppHost"
)

# Populated per project as we go (parallel arrays — macOS ships bash 3.2,
# which has no associative array support).
# PROJECT_NAMES[i] -> "scheme://localhost:port,scheme://localhost:port,..." in PROJECT_URL_LISTS[i]
PROJECT_NAMES=()
PROJECT_URL_LISTS=()

set_project_urls() {
  local name="$1" urls="$2"
  PROJECT_NAMES+=("$name")
  PROJECT_URL_LISTS+=("$urls")
}

get_project_urls() {
  local name="$1" i
  for i in "${!PROJECT_NAMES[@]}"; do
    if [[ "${PROJECT_NAMES[$i]}" == "$name" ]]; then
      echo "${PROJECT_URL_LISTS[$i]}"
      return 0
    fi
  done
  return 1
}

probe_scheme() {
  # Returns "https", "http", or "unknown" for a given port.
  local port="$1"
  if curl -sk -o /dev/null --max-time 2 "https://localhost:$port"; then
    echo "https"
    return
  fi
  if curl -s -o /dev/null --max-time 2 "http://localhost:$port"; then
    echo "http"
    return
  fi
  echo "unknown"
}

echo "Scanning running eShop processes for listening ports..."
echo

printf "%-18s %-8s %-8s %-32s\n" "PROJECT" "PID" "SCHEME" "URL"
printf "%-18s %-8s %-8s %-32s\n" "------------------" "--------" "--------" "--------------------------------"

FOUND_ANY=false

for name in "${PROJECTS[@]}"; do
  # Match the exact build-output executable path for this project.
  pid=$(ps -eo pid,command | grep -F "/artifacts/bin/${name}/debug/${name}" | grep -v grep | awk '{print $1}' | head -n1)

  if [[ -z "$pid" ]]; then
    printf "%-18s %-8s %-8s %-32s\n" "$name" "-" "-" "(not running)"
    continue
  fi

  FOUND_ANY=true

  # Distinct listening TCP ports for this PID (dedupe IPv4/IPv6 entries).
  ports=$(lsof -nP -iTCP -sTCP:LISTEN -a -p "$pid" 2>/dev/null | awk 'NR>1{print $9}' | sed -E 's/.*://' | sort -un)

  if [[ -z "$ports" ]]; then
    printf "%-18s %-8s %-8s %-32s\n" "$name" "$pid" "-" "(no listening ports found)"
    continue
  fi

  urls=""
  first=true
  while read -r port; do
    [[ -z "$port" ]] && continue
    scheme=$(probe_scheme "$port")
    url="${scheme}://localhost:${port}"
    printf "%-18s %-8s %-8s %-32s\n" "$([[ $first == true ]] && echo "$name" || echo "")" "$([[ $first == true ]] && echo "$pid" || echo "")" "$scheme" "$url"
    first=false
    urls="${urls:+$urls,}$url"
  done <<< "$ports"

  set_project_urls "$name" "$urls"
done

echo

if [[ "$FOUND_ANY" == false ]]; then
  echo "No matching processes found. Is the AppHost running? (task: 'run: AppHost (All Services)')"
  exit 1
fi

# Pick the preferred URL for PP (http scheme) and IDENTITY (https scheme).
pick_url() {
  local project="$1" want_scheme="$2"
  local urls
  urls=$(get_project_urls "$project") || return 1
  [[ -z "$urls" ]] && return 1
  IFS=',' read -ra arr <<< "$urls"
  for u in "${arr[@]}"; do
    if [[ "$u" == "${want_scheme}://"* ]]; then
      echo "$u"
      return 0
    fi
  done
  # Fall back to first URL if the preferred scheme wasn't found.
  echo "${arr[0]}"
}

PP_URL=$(pick_url "PaymentProcessor" "http")
IDENTITY_URL=$(pick_url "Identity.API" "https")

echo "Suggested config.env values:"
echo "  PP=\"${PP_URL:-<not running>}\""
echo "  IDENTITY=\"${IDENTITY_URL:-<not running>}\""

# ---------------------------------------------------------------------------
# Postgres: port comes from the running container (Colima/Docker), password
# comes from the AppHost's user-secrets (Aspire's documented mechanism for
# persisting generated parameter values across runs).
# ---------------------------------------------------------------------------
urlencode() {
  local raw="$1" out="" c i
  for (( i = 0; i < ${#raw}; i++ )); do
    c="${raw:i:1}"
    case "$c" in
      [a-zA-Z0-9.~_-]) out+="$c" ;;
      *) out+="$(printf '%%%02X' "'$c")" ;;
    esac
  done
  echo "$out"
}

PG_PORT=""
if command -v docker &>/dev/null; then
  PG_PORT=$(docker ps --format '{{.Names}} {{.Ports}}' 2>/dev/null \
    | awk '/^postgres/ {print $0}' \
    | grep -oE '127\.0\.0\.1:[0-9]+->5432' \
    | head -n1 \
    | sed -E 's#.*:([0-9]+)->5432#\1#')
fi

PG_PASSWORD=""
if command -v dotnet &>/dev/null; then
  PG_PASSWORD=$(dotnet user-secrets list --project "$REPO_ROOT/src/eShop.AppHost/eShop.AppHost.csproj" 2>/dev/null \
    | awk -F' = ' '/^Parameters:postgres-password/ {print $2}')
fi

PG_CONN=""
if [[ -n "$PG_PORT" && -n "$PG_PASSWORD" ]]; then
  PG_CONN="postgresql://postgres:$(urlencode "$PG_PASSWORD")@localhost:${PG_PORT}"
  echo "  PG_CONN=\"${PG_CONN}\""
else
  echo
  echo "WARN: could not fully derive PG_CONN (port=${PG_PORT:-?}, password=${PG_PASSWORD:+<found>}${PG_PASSWORD:-<missing>})."
  echo "Is the postgres container running (docker ps) and is the AppHost's user-secrets store intact?"
fi

if [[ "$UPDATE" == true ]]; then
  if [[ -z "$PP_URL" || -z "$IDENTITY_URL" ]]; then
    echo
    echo "FAIL: cannot --update, PaymentProcessor and/or Identity.API are not running."
    exit 1
  fi

  if [[ ! -f "$CONFIG_ENV" ]]; then
    echo
    echo "FAIL: $CONFIG_ENV not found."
    exit 1
  fi

  cp "$CONFIG_ENV" "$CONFIG_ENV.bak"
  sed -i '' -E "s#^PP=\"\\\$\\{PP:-[^}]*\\}\"#PP=\"\${PP:-${PP_URL}}\"#" "$CONFIG_ENV"
  sed -i '' -E "s#^IDENTITY=\"\\\$\\{IDENTITY:-[^}]*\\}\"#IDENTITY=\"\${IDENTITY:-${IDENTITY_URL}}\"#" "$CONFIG_ENV"

  if [[ -n "$PG_CONN" ]]; then
    sed -i '' -E "s#^PG_CONN=\"\\\$\\{PG_CONN:-[^}]*\\}\"#PG_CONN=\"\${PG_CONN:-${PG_CONN}}\"#" "$CONFIG_ENV"
  fi

  echo
  echo "Updated $CONFIG_ENV (backup saved to config.env.bak):"
  grep -E '^(PP|IDENTITY|PG_CONN)=' "$CONFIG_ENV"
fi
