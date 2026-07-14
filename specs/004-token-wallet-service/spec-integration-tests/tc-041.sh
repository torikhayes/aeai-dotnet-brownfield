#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-041: Transaction history shows spend entry with null catalogItemId ==="

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

TXN_RESP=$(curl -sk "$PP/api/tokens/transactions?userId=$ALICE_ID" \
  -H "Authorization: Bearer $TOKEN")

ITEM=$(echo "$TXN_RESP" | jq '[.items[] | select(.amount == -30)] | first')

if [ -z "$ITEM" ] || [ "$ITEM" = "null" ]; then
  echo "FAIL: no spend transaction with amount=-30 found in history"
  exit 1
fi

CATALOG_ITEM_ID=$(echo "$ITEM" | jq -r '.catalogItemId')
REASON=$(echo "$ITEM" | jq -r '.reason')

if [ "$CATALOG_ITEM_ID" != "null" ]; then
  echo "FAIL: expected catalogItemId=null, got $CATALOG_ITEM_ID"
  exit 1
fi

if [ "$REASON" != "purchase debit" ]; then
  echo "FAIL: expected reason=\"purchase debit\", got $REASON"
  exit 1
fi

echo "PASS"
