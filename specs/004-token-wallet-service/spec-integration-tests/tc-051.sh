#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-051: pageSize is capped at 100 ==="

RESPONSE=$(curl -sk "$PP/api/tokens/transactions?pageSize=500" \
  -H "Authorization: Bearer $TOKEN")

ITEM_COUNT=$(echo "$RESPONSE" | jq '.items | length')

if [ "$ITEM_COUNT" -le 100 ]; then
  echo "PASS: items count is $ITEM_COUNT (<= 100)"
else
  echo "FAIL: expected items count <= 100, got $ITEM_COUNT"
fi
