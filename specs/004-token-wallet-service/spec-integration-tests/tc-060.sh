#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-060: Bob cannot see alice's balance ==="

if [ -z "$BOB_TOKEN" ]; then
  BOB_TOKEN=$(cat "$(dirname "$0")/.bob_token" 2>/dev/null)
fi

if [ -z "$BOB_TOKEN" ]; then
  BOB_TOKEN=$("$(dirname "$0")/get-bob-token.sh")
fi

HTTP_STATUS=$(curl -sk -o /tmp/tc060_body.json -w "%{http_code}" \
  "$PP/api/tokens/balance" \
  -H "Authorization: Bearer $BOB_TOKEN")

BALANCE=$(jq -r '.balance' /tmp/tc060_body.json)

PASS=true

if [ "$HTTP_STATUS" != "200" ]; then
  echo "FAIL: expected HTTP 200, got $HTTP_STATUS"
  PASS=false
fi

if [ "$BALANCE" != "0" ]; then
  echo "FAIL: expected balance=0 for bob (alice's balance should not be visible to bob), got $BALANCE"
  PASS=false
fi

if [ "$PASS" = "true" ]; then
  echo "PASS: HTTP $HTTP_STATUS, bob's balance=$BALANCE (alice's balance is not visible to bob)"
fi
