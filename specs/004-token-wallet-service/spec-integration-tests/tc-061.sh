#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-061: Bob cannot see alice's transaction history ==="

if [ -z "$BOB_TOKEN" ]; then
  BOB_TOKEN=$(cat "$(dirname "$0")/.bob_token" 2>/dev/null)
fi

if [ -z "$BOB_TOKEN" ]; then
  BOB_TOKEN=$("$(dirname "$0")/get-bob-token.sh")
fi

HTTP_STATUS=$(curl -sk -o /tmp/tc061_body.json -w "%{http_code}" \
  "$PP/api/tokens/transactions" \
  -H "Authorization: Bearer $BOB_TOKEN")

TOTAL_COUNT=$(jq -r '.totalCount' /tmp/tc061_body.json)
ITEMS_COUNT=$(jq '.items | length' /tmp/tc061_body.json)

PASS=true

if [ "$HTTP_STATUS" != "200" ]; then
  echo "FAIL: expected HTTP 200, got $HTTP_STATUS"
  PASS=false
fi

if [ "$TOTAL_COUNT" != "0" ]; then
  echo "FAIL: expected totalCount=0 for bob, got $TOTAL_COUNT"
  PASS=false
fi

if [ "$ITEMS_COUNT" != "0" ]; then
  echo "FAIL: expected empty items array for bob, got $ITEMS_COUNT items"
  PASS=false
fi

if [ "$PASS" = "true" ]; then
  echo "PASS: HTTP $HTTP_STATUS, bob sees totalCount=$TOTAL_COUNT and empty items (alice's history is not visible)"
fi
