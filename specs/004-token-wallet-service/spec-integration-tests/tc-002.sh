#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-002: Token lookup table seeded with 28 rows ==="

if ! command -v psql &>/dev/null; then
  echo "WARNING: psql not found — skipping test"
  exit 0
fi

count=$(psql "$PG_CONN/tokendb" -tAc 'SELECT COUNT(*) FROM "TokenAwardLookupEntries";' 2>&1)

if [ "$count" = "28" ]; then
  echo "PASS: TokenAwardLookupEntries contains 28 rows"
else
  echo "FAIL: Expected 28 rows, got: $count"
fi
