#!/usr/bin/env bash
source "$(dirname "$0")/config.env"
echo "=== TC-023: New user transaction history is empty 200 (not 404) ==="

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$(dirname "$0")/.alice_token" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "SKIP: TOKEN not set and .alice_token not found. Run get-token.sh first."
  exit 1
fi

RESPONSE=$(curl -sk -w "\n%{http_code}" -H "Authorization: Bearer $TOKEN" \
  "$PP/api/tokens/transactions?page=1&pageSize=20")

HTTP_STATUS=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')

TOTAL=$(echo "$BODY" | jq -r '.totalCount')
ITEMS_LENGTH=$(echo "$BODY" | jq '.items | length')

if [ "$HTTP_STATUS" = "200" ] && [ "$TOTAL" = "0" ] && [ "$ITEMS_LENGTH" = "0" ]; then
  echo "PASS (status=$HTTP_STATUS, totalCount=$TOTAL, items=[])"
else
  echo "FAIL (status=$HTTP_STATUS, totalCount=$TOTAL, items_length=$ITEMS_LENGTH, body=$BODY)"
fi
