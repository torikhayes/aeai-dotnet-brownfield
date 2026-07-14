#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-052: Page beyond total returns 200 with empty items ==="

HTTP_STATUS=$(curl -sk -o /tmp/tc052_body.json -w "%{http_code}" \
  "$PP/api/tokens/transactions?page=999" \
  -H "Authorization: Bearer $TOKEN")

ITEMS=$(jq '.items' /tmp/tc052_body.json)
ITEMS_COUNT=$(jq '.items | length' /tmp/tc052_body.json)

PASS=true

if [ "$HTTP_STATUS" != "200" ]; then
  echo "FAIL: expected HTTP 200, got $HTTP_STATUS"
  PASS=false
fi

if [ "$ITEMS_COUNT" != "0" ]; then
  echo "FAIL: expected empty items array, got $ITEMS_COUNT items"
  PASS=false
fi

if [ "$PASS" = "true" ]; then
  echo "PASS: HTTP $HTTP_STATUS with empty items array"
fi
