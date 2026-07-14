#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-011: All 7 categories return expected v1.0.0 values ==="

PASS_COUNT=0
FAIL_COUNT=0

check() {
  local CATEGORY_ENC="$1"
  local CONDITION="$2"
  local EXPECTED_AMOUNT="$3"
  local DISPLAY_CATEGORY="$4"

  RESPONSE=$(curl -sk "${PP}/api/tokens/reward-preview?category=${CATEGORY_ENC}&condition=${CONDITION}")
  AMOUNT=$(echo "$RESPONSE" | jq -r '.tokenAmount')
  VERSION=$(echo "$RESPONSE" | jq -r '.tableVersion')

  if [ "$AMOUNT" = "$EXPECTED_AMOUNT" ] && [ "$VERSION" = "1.0.0" ]; then
    echo "  PASS: ${DISPLAY_CATEGORY}/${CONDITION} tokenAmount=${AMOUNT} tableVersion=${VERSION}"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo "  FAIL: ${DISPLAY_CATEGORY}/${CONDITION} expected tokenAmount=${EXPECTED_AMOUNT} actual=${AMOUNT} tableVersion=${VERSION}"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

check "Driver"       "New"       "100" "Driver"
check "Fairway+Wood" "Excellent" "65"  "Fairway Wood"
check "Hybrid"       "Good"      "40"  "Hybrid"
check "Iron+Set"     "New"       "120" "Iron Set"
check "Wedge"        "Fair"      "20"  "Wedge"
check "Putter"       "Excellent" "72"  "Putter"
check "Other"        "Fair"      "15"  "Other"

echo ""
if [ "$FAIL_COUNT" -eq 0 ]; then
  echo "PASS: all 7 category checks passed (${PASS_COUNT}/7)"
else
  echo "FAIL: ${FAIL_COUNT} of 7 category checks failed (${PASS_COUNT} passed)"
fi
