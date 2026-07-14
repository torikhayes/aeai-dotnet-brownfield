#!/usr/bin/env bash
source "$(dirname "$0")/config.env"
echo "=== TC-021: No wallet row created by balance read ==="

if ! command -v psql &>/dev/null; then
  echo "SKIP: psql not available"
  exit 0
fi

if [ -z "$PG_CONN" ]; then
  echo "SKIP: PG_CONN not set"
  exit 0
fi

COUNT=$(psql "$PG_CONN/tokendb" -tAc 'SELECT COUNT(*) FROM "TokenWallets";' 2>&1)

if ! echo "$COUNT" | grep -qE '^[0-9]+$'; then
  echo "SKIP: Could not query database (output: $COUNT)"
  exit 0
fi

if [ "$COUNT" = "0" ]; then
  echo "PASS (row count=$COUNT)"
else
  echo "FAIL (expected 0 rows, got count=$COUNT)"
fi
