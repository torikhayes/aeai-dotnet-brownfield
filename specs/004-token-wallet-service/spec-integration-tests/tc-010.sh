#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-010: Valid category+condition returns correct amount ==="

RESPONSE=$(curl -sk "${PP}/api/tokens/reward-preview?category=Driver&condition=Excellent")

AMOUNT=$(echo "$RESPONSE" | jq -r '.tokenAmount')
VERSION=$(echo "$RESPONSE" | jq -r '.tableVersion')

PASS=true

if [ "$AMOUNT" != "80" ]; then
  echo "FAIL: tokenAmount expected=80 actual=${AMOUNT}"
  PASS=false
fi

if [ "$VERSION" != "1.0.0" ]; then
  echo "FAIL: tableVersion expected=1.0.0 actual=${VERSION}"
  PASS=false
fi

if [ "$PASS" = "true" ]; then
  echo "PASS: tokenAmount=${AMOUNT} tableVersion=${VERSION}"
fi
