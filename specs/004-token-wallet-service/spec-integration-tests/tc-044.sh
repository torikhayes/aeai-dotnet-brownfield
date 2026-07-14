#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-044: amount=0 returns 400 validation error ==="

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

SPEND_BODY="{\"userId\":\"$ALICE_ID\",\"amount\":0,\"orderId\":\"order-test-003\"}"

HTTP_STATUS=$(curl -sk -o /tmp/tc044_resp.json -w "%{http_code}" \
  -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SPEND_BODY")

if [ "$HTTP_STATUS" != "400" ]; then
  echo "FAIL: expected HTTP 400 for amount=0, got $HTTP_STATUS"
  exit 1
fi

BODY=$(cat /tmp/tc044_resp.json)

if ! echo "$BODY" | grep -q "validation_error"; then
  echo "FAIL: expected body to contain \"validation_error\", got: $BODY"
  exit 1
fi

echo "PASS"
