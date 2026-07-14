#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

TOKEN_FILE="$(dirname "$0")/.alice_token"

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$TOKEN_FILE" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "ERROR: TOKEN not set and .alice_token not found. Run get-token.sh first." >&2
  exit 1
fi

# Base64-decode the payload segment (second part) of the JWT
PAYLOAD=$(echo "$TOKEN" | cut -d. -f2)

# JWT base64url may need padding
PAD=$(( 4 - ${#PAYLOAD} % 4 ))
if [ "$PAD" -ne 4 ]; then
  PAYLOAD="${PAYLOAD}$(printf '=%.0s' $(seq 1 $PAD))"
fi

SUB=$(echo "$PAYLOAD" | base64 -d 2>/dev/null | jq -r '.sub')

if [ -z "$SUB" ] || [ "$SUB" = "null" ]; then
  echo "ERROR: could not extract .sub from JWT payload" >&2
  exit 1
fi

echo "$SUB" > "$(dirname "$0")/.alice_id"
echo "$SUB"
