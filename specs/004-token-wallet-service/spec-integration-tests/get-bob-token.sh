#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

RESPONSE=$(curl -sk -X POST "$IDENTITY/connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=testscript&client_secret=secret&username=bob&password=Pass123%24&scope=openid%20profile%20orders%20basket")

TOKEN=$(echo "$RESPONSE" | jq -r '.access_token')

if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "ERROR: Failed to obtain bob's token. Response: $RESPONSE" >&2
  exit 1
fi

echo "$TOKEN" > "$(dirname "$0")/.bob_token"
echo "$TOKEN"
