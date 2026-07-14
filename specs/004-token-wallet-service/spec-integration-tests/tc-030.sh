#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-030: Seed alice's wallet via psql (T020 DEFERRED workaround) ==="

# T020 DEFERRED — Using psql workaround to seed alice's wallet
# The Catalog.API ClubListingVerifiedIntegrationEvent publisher (T020) is deferred.
# This script inserts wallet + transaction rows directly into tokendb to unblock
# TC-031 / TC-032 / TC-033 earn-path integration testing.

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

if [ -z "$ALICE_ID" ]; then
  echo "FAIL: ALICE_ID not set and .alice_id not found. Run get-alice-id.sh first."
  exit 1
fi

psql "$PG_CONN/tokendb" <<SQL
INSERT INTO "TokenWallets" ("UserId", "Balance")
VALUES ('$ALICE_ID', 80)
ON CONFLICT ("UserId") DO UPDATE SET "Balance" = 80;

INSERT INTO "TokenTransactions"
  ("Id", "UserId", "Amount", "Reason", "RelatedEventId", "LookupTableVersion", "CatalogItemId", "CreatedAt")
VALUES
  (gen_random_uuid(), '$ALICE_ID', 80, 'Driver/Excellent listing verified',
   'evt-manual-001', '1.0.0', 'test-club-item-001', now())
ON CONFLICT ("RelatedEventId") DO NOTHING;
SQL

PSQL_EXIT=$?

if [ $PSQL_EXIT -eq 0 ]; then
  echo "PASS: wallet seeded for alice ($ALICE_ID)"
else
  echo "FAIL: psql exited with code $PSQL_EXIT"
fi
