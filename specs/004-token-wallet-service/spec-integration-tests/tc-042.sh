#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-042: Duplicate orderId returns 409 ==="

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

SPEND_BODY="{\"userId\":\"$ALICE_ID\",\"amount\":30,\"orderId\":\"order-test-001\"}"

HTTP_STATUS=$(curl -sk -o /dev/null -w "%{http_code}" \
  -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SPEND_BODY")

if [ "$HTTP_STATUS" != "409" ]; then
  echo "FAIL: expected HTTP 409 for duplicate orderId, got $HTTP_STATUS"
  exit 1
fi

BAL_RESP=$(curl -sk "$PP/api/tokens/balance?userId=$ALICE_ID" \
  -H "Authorization: Bearer $TOKEN")

BALANCE=$(echo "$BAL_RESP" | jq -r '.balance')

if [ "$BALANCE" != "50" ]; then
  echo "FAIL: expected balance=50 (unchanged after duplicate), got $BALANCE"
  exit 1
fi

echo "PASS"
