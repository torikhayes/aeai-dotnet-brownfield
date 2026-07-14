#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-050: pageSize defaults to 20 ==="

RESPONSE=$(curl -sk "$PP/api/tokens/transactions" \
  -H "Authorization: Bearer $TOKEN")

PAGE_SIZE=$(echo "$RESPONSE" | jq -r '.pageSize')

if [ "$PAGE_SIZE" = "20" ]; then
  echo "PASS: pageSize defaulted to 20"
else
  echo "FAIL: expected pageSize=20, got $PAGE_SIZE"
fi
