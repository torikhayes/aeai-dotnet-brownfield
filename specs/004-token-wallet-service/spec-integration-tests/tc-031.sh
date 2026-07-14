#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-031: Balance reflects the award (80 tokens) ==="

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$(dirname "$0")/.alice_token" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "FAIL: TOKEN not set and .alice_token not found. Run get-token.sh first."
  exit 1
fi

RESPONSE=$(curl -sk -o /tmp/tc031_body.json -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  "$PP/api/tokens/balance")

if [ "$RESPONSE" != "200" ]; then
  echo "FAIL: expected HTTP 200, got $RESPONSE"
  exit 1
fi

BALANCE=$(jq -r '.balance' /tmp/tc031_body.json)

if [ "$BALANCE" = "80" ] || [ "$BALANCE" = "80.0" ] || [ "$BALANCE" = "80.00" ]; then
  echo "PASS (balance=$BALANCE)"
else
  echo "FAIL: expected balance=80, got $BALANCE"
fi
