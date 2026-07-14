#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-040: Successful spend debits the wallet ==="

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

SPEND_BODY="{\"userId\":\"$ALICE_ID\",\"amount\":30,\"orderId\":\"order-test-001\"}"

SPEND_RESP=$(curl -sk -o /tmp/tc040_spend.json -w "%{http_code}" \
  -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SPEND_BODY")

if [ "$SPEND_RESP" != "200" ]; then
  echo "FAIL: expected HTTP 200 on spend, got $SPEND_RESP"
  exit 1
fi

NEW_BALANCE=$(jq -r '.newBalance' /tmp/tc040_spend.json)

if [ "$NEW_BALANCE" != "50" ]; then
  echo "FAIL: expected newBalance=50, got $NEW_BALANCE"
  exit 1
fi

BAL_RESP=$(curl -sk "$PP/api/tokens/balance?userId=$ALICE_ID" \
  -H "Authorization: Bearer $TOKEN")

BALANCE=$(echo "$BAL_RESP" | jq -r '.balance')

if [ "$BALANCE" != "50" ]; then
  echo "FAIL: expected balance=50 after spend, got $BALANCE"
  exit 1
fi

echo "PASS"
