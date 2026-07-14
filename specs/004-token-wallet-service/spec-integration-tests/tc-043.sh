#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-043: Spend that would exceed balance returns 400 ==="

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

SPEND_BODY="{\"userId\":\"$ALICE_ID\",\"amount\":200,\"orderId\":\"order-test-002\"}"

HTTP_STATUS=$(curl -sk -o /tmp/tc043_resp.json -w "%{http_code}" \
  -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SPEND_BODY")

if [ "$HTTP_STATUS" != "400" ]; then
  echo "FAIL: expected HTTP 400 for insufficient balance, got $HTTP_STATUS"
  exit 1
fi

BODY=$(cat /tmp/tc043_resp.json)

if ! echo "$BODY" | grep -q "insufficient_balance"; then
  echo "FAIL: expected body to contain \"insufficient_balance\", got: $BODY"
  exit 1
fi

BAL_RESP=$(curl -sk "$PP/api/tokens/balance?userId=$ALICE_ID" \
  -H "Authorization: Bearer $TOKEN")

BALANCE=$(echo "$BAL_RESP" | jq -r '.balance')

if [ "$BALANCE" != "50" ]; then
  echo "FAIL: expected balance=50 (unchanged after failed spend), got $BALANCE"
  exit 1
fi

echo "PASS"
