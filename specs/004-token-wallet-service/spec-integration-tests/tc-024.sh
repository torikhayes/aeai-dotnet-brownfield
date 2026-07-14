#!/usr/bin/env bash
source "$(dirname "$0")/config.env"
echo "=== TC-024: Unauthenticated transaction history returns 401 ==="

HTTP_STATUS=$(curl -sk -o /dev/null -w "%{http_code}" "$PP/api/tokens/transactions")

if [ "$HTTP_STATUS" = "401" ]; then
  echo "PASS (status=$HTTP_STATUS)"
else
  echo "FAIL (expected 401, got status=$HTTP_STATUS)"
fi
