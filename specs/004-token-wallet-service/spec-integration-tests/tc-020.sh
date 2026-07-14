#!/usr/bin/env bash
source "$(dirname "$0")/config.env"
echo "=== TC-020: New user balance is zero ==="

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$(dirname "$0")/.alice_token" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "SKIP: TOKEN not set and .alice_token not found. Run get-token.sh first."
  exit 1
fi

RESPONSE=$(curl -sk -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/balance")
BALANCE=$(echo "$RESPONSE" | jq -r '.balance')

if [ "$BALANCE" = "0" ] || [ "$BALANCE" = "0.0" ] || [ "$BALANCE" = "0.00" ]; then
  echo "PASS (balance=$BALANCE)"
else
  echo "FAIL (expected 0, got balance=$BALANCE, response=$RESPONSE)"
fi
