#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-012: Missing category returns 400 ==="

STATUS=$(curl -sk -o /dev/null -w "%{http_code}" "${PP}/api/tokens/reward-preview?condition=Good")

if [ "$STATUS" = "400" ]; then
  echo "PASS: HTTP status=${STATUS}"
else
  echo "FAIL: expected=400 actual=${STATUS}"
fi
