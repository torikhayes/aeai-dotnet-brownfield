#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-033: Second seed for same CatalogItemId is rejected (idempotency) ==="

# T020 DEFERRED — Using psql workaround to seed alice's wallet
# Re-runs the same INSERT from TC-030. The ON CONFLICT DO NOTHING on RelatedEventId
# should silently ignore the duplicate; balance must remain 80 (not 160).

if [ -z "$ALICE_ID" ]; then
  ALICE_ID=$(cat "$(dirname "$0")/.alice_id" 2>/dev/null)
fi

if [ -z "$ALICE_ID" ]; then
  echo "FAIL: ALICE_ID not set and .alice_id not found. Run get-alice-id.sh first."
  exit 1
fi

if [ -z "$TOKEN" ]; then
  TOKEN=$(cat "$(dirname "$0")/.alice_token" 2>/dev/null)
fi

if [ -z "$TOKEN" ]; then
  echo "FAIL: TOKEN not set and .alice_token not found. Run get-token.sh first."
  exit 1
fi

# Attempt duplicate insert — same RelatedEventId 'evt-manual-001'
psql "$PG_CONN/tokendb" <<SQL
INSERT INTO "TokenTransactions"
  ("Id", "UserId", "Amount", "Reason", "RelatedEventId", "LookupTableVersion", "CatalogItemId", "CreatedAt")
VALUES
  (gen_random_uuid(), '$ALICE_ID', 80, 'Driver/Excellent listing verified',
   'evt-manual-001', '1.0.0', 'test-club-item-001', now())
ON CONFLICT ("RelatedEventId") DO NOTHING;
SQL

PSQL_EXIT=$?

if [ $PSQL_EXIT -ne 0 ]; then
  echo "FAIL: psql exited with code $PSQL_EXIT on duplicate insert attempt"
  exit 1
fi

RESPONSE=$(curl -sk -o /tmp/tc033_body.json -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  "$PP/api/tokens/balance")

if [ "$RESPONSE" != "200" ]; then
  echo "FAIL: expected HTTP 200 on balance check, got $RESPONSE"
  exit 1
fi

BALANCE=$(jq -r '.balance' /tmp/tc033_body.json)

if [ "$BALANCE" = "80" ] || [ "$BALANCE" = "80.0" ] || [ "$BALANCE" = "80.00" ]; then
  echo "PASS: balance is still $BALANCE after duplicate seed (idempotency confirmed)"
else
  echo "FAIL: expected balance=80 (no double-credit), got $BALANCE"
fi
