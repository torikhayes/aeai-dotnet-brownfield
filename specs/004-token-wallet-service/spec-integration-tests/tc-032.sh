#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-032: Transaction history shows the earn entry ==="

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$(dirname "$0")/.alice_token" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "FAIL: TOKEN not set and .alice_token not found. Run get-token.sh first."
  exit 1
fi

RESPONSE=$(curl -sk -o /tmp/tc032_body.json -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  "$PP/api/tokens/transactions?page=1&pageSize=20")

if [ "$RESPONSE" != "200" ]; then
  echo "FAIL: expected HTTP 200, got $RESPONSE"
  exit 1
fi

TOTAL=$(jq -r '.totalCount' /tmp/tc032_body.json)

if [ -z "$TOTAL" ] || [ "$TOTAL" = "null" ] || [ "$TOTAL" -lt 1 ] 2>/dev/null; then
  echo "FAIL: expected totalCount >= 1, got $TOTAL"
  exit 1
fi

# Check the first item in the items array
AMOUNT=$(jq -r '.items[0].amount' /tmp/tc032_body.json)
REASON=$(jq -r '.items[0].reason' /tmp/tc032_body.json)
CATALOG_ITEM_ID=$(jq -r '.items[0].catalogItemId' /tmp/tc032_body.json)

AMOUNT_OK=false
REASON_OK=false
ITEM_OK=false

if [ "$AMOUNT" = "80" ] || [ "$AMOUNT" = "80.0" ] || [ "$AMOUNT" = "80.00" ]; then
  AMOUNT_OK=true
fi

if echo "$REASON" | grep -qi "listing verified"; then
  REASON_OK=true
fi

if [ "$CATALOG_ITEM_ID" = "test-club-item-001" ]; then
  ITEM_OK=true
fi

if $AMOUNT_OK && $REASON_OK && $ITEM_OK; then
  echo "PASS (totalCount=$TOTAL, amount=$AMOUNT, reason=\"$REASON\", catalogItemId=$CATALOG_ITEM_ID)"
else
  echo "FAIL:"
  $AMOUNT_OK || echo "  amount: expected 80, got $AMOUNT"
  $REASON_OK || echo "  reason: expected to contain 'listing verified', got \"$REASON\""
  $ITEM_OK   || echo "  catalogItemId: expected 'test-club-item-001', got $CATALOG_ITEM_ID"
fi
