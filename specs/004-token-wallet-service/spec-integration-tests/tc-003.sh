#!/usr/bin/env bash
# Restart the AppHost first, then run this script
source "$(dirname "$0")/config.env"

echo "=== TC-003: Seed is idempotent (count stays at 28 after restart) ==="

if ! command -v psql &>/dev/null; then
  echo "WARNING: psql not found — skipping test"
  exit 0
fi

count=$(psql "$PG_CONN/tokendb" -tAc 'SELECT COUNT(*) FROM "TokenAwardLookupEntries";' 2>&1)

if [ "$count" = "28" ]; then
  echo "PASS: TokenAwardLookupEntries still contains 28 rows after restart (seed is idempotent)"
else
  echo "FAIL: Expected 28 rows after restart, got: $count"
fi
