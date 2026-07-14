# Manual Test Plan: Token Wallet Service (Feature 004)

**Feature**: PaymentProcessor Token Ledger Extension  
**Tester audience**: Developer / QA doing exploratory manual verification  
**Status of T020**: Catalog.API publisher deferred — earn-token flow requires a RabbitMQ workaround (see Section 4)

---

## Prerequisites

| Requirement | How to verify |
|---|---|
| Colima running | `colima status` |
| App running | `dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj` |
| PaymentProcessor healthy | Aspire dashboard → `payment-processor` shows green |
| `tokendb` migrated + seeded | Aspire dashboard → `payment-processor` logs show `"Migration operation TokenDbContext"` completed |

**Seeded test users** (from Identity.API):

| Username | Password | Notes |
|---|---|---|
| `alice` | `Pass123$` | Use as Seller / primary tester |
| `bob` | `Pass123$` | Use as second user for isolation tests |

**Finding the PaymentProcessor port**: Open the Aspire dashboard → Resources → `payment-processor` → copy the `https` endpoint URL. All token endpoints sit at `<payment-processor-url>/api/tokens/`.

---

## Section 1 — App Startup Verification

### TC-001: PaymentProcessor starts healthy

1. Start the app: `dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj`
2. Open the Aspire dashboard (URL printed to terminal on startup)
3. Wait for all services to show green

**Expected**: `payment-processor` resource shows **Running** health state within ~30 seconds.

---

### TC-002: Token lookup table is seeded with 28 rows

1. In the Aspire dashboard, open the `postgres` resource → connect via the connection string, or run:

```bash
# Get connection string from Aspire dashboard → postgres → Connection String
psql "<connection-string>/tokendb" -c 'SELECT COUNT(*) FROM "TokenAwardLookupEntries";'
```

**Expected**: `COUNT = 28`

---

### TC-003: Seed is idempotent on restart

1. Stop and restart the AppHost
2. Re-run the COUNT query from TC-002

**Expected**: Still `COUNT = 28` — no duplicate rows inserted.

---

## Section 2 — Reward Preview (Unauthenticated)

No login required. Run these directly from a terminal or browser.

### TC-010: Valid category + condition returns correct amount

```bash
PP=<payment-processor-url>

curl -k "$PP/api/tokens/reward-preview?category=Driver&condition=Excellent"
```

**Expected** `200`:
```json
{ "tokenAmount": 80, "tableVersion": "1.0.0" }
```

---

### TC-011: All 7 categories return expected v1.0.0 values

Run through each category spot-check:

| `category` | `condition` | Expected `tokenAmount` |
|---|---|---|
| `Driver` | `New` | 100 |
| `Fairway Wood` | `Excellent` | 65 |
| `Hybrid` | `Good` | 40 |
| `Iron Set` | `New` | 120 |
| `Wedge` | `Fair` | 20 |
| `Putter` | `Excellent` | 72 |
| `Other` | `Fair` | 15 |

```bash
curl -k "$PP/api/tokens/reward-preview?category=Iron+Set&condition=New"
# Expected: { "tokenAmount": 120, "tableVersion": "1.0.0" }
```

---

### TC-012: Missing `category` parameter returns 400

```bash
curl -k -o /dev/null -w "%{http_code}" "$PP/api/tokens/reward-preview?condition=Good"
```

**Expected**: `400`

---

### TC-013: Missing `condition` parameter returns 400

```bash
curl -k -o /dev/null -w "%{http_code}" "$PP/api/tokens/reward-preview?category=Driver"
```

**Expected**: `400`

---

### TC-014: Unrecognised category returns 400

```bash
curl -k -o /dev/null -w "%{http_code}" "$PP/api/tokens/reward-preview?category=Skateboard&condition=Good"
```

**Expected**: `400`

---

## Section 3 — Balance & Transaction History (Authenticated)

### Step: Get a JWT for alice

1. Open the WebApp in a browser (URL in Aspire dashboard → `webapp`)
2. Click **Login** → sign in as `alice` / `Pass123$`
3. Open browser DevTools → Network tab → look for any authenticated API call
4. Copy the `Authorization: Bearer <token>` value  

**Or** use Identity.API directly:

```bash
IDENTITY=<identity-api-url>

TOKEN=$(curl -sk -X POST "$IDENTITY/connect/token" \
  -d "grant_type=password&client_id=webapp&client_secret=secret&username=alice&password=Pass123%24&scope=openid profile orders basket webshoppingagg webhooks" \
  | jq -r .access_token)

echo $TOKEN   # should be a JWT string
```

---

### TC-020: New user balance is zero

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/balance"
```

**Expected** `200`:
```json
{ "balance": 0 }
```

---

### TC-021: No wallet row is created by a balance read

```bash
psql "<connection-string>/tokendb" -c 'SELECT COUNT(*) FROM "TokenWallets";'
```

**Expected**: `COUNT = 0` — reading balance for a new user must not write a row.

---

### TC-022: Unauthenticated balance request is rejected

```bash
curl -k -o /dev/null -w "%{http_code}" "$PP/api/tokens/balance"
```

**Expected**: `401`

---

### TC-023: New user transaction history is empty (not 404)

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions?page=1&pageSize=20"
```

**Expected** `200`:
```json
{ "totalCount": 0, "page": 1, "pageSize": 20, "items": [] }
```

---

### TC-024: Unauthenticated transaction history is rejected

```bash
curl -k -o /dev/null -w "%{http_code}" "$PP/api/tokens/transactions"
```

**Expected**: `401`

---

## Section 4 — Earn Tokens via Integration Event

> ⚠️ **T020 is deferred** — Catalog.API does not yet publish `ClubListingVerifiedIntegrationEvent`. Until spec 007 is implemented, use the RabbitMQ workaround below to simulate a verified listing.

### Step: Find alice's user ID (sub claim)

```bash
# Decode the JWT (third party tool or paste at jwt.io)
echo $TOKEN | cut -d. -f2 | base64 -d 2>/dev/null | jq .sub
# Note the `sub` value — e.g. "38d3f835-abc1-..."
ALICE_ID=<sub-value-from-token>
```

---

### TC-030: Publish a ClubListingVerifiedIntegrationEvent via RabbitMQ

**Option A — RabbitMQ Management UI** (if port 15672 is accessible from Aspire dashboard):

1. Open RabbitMQ management at `http://localhost:15672` (credentials: `guest` / `guest`)
2. Go to **Exchanges** → find or create exchange `ClubListingVerifiedIntegrationEvent`
3. Publish the message:

```json
{
  "Id": "a1b2c3d4-0000-0000-0000-000000000001",
  "CreationDate": "2026-07-13T12:00:00Z",
  "SellerId": "<alice-sub-id>",
  "CatalogItemId": "test-club-item-001",
  "Category": "Driver",
  "Condition": "Excellent"
}
```

**Option B — psql direct wallet insert** (faster workaround):

```sql
-- Connect to tokendb
INSERT INTO "TokenWallets" ("UserId", "Balance", "RowVersion")
VALUES ('<alice-sub-id>', 80, decode('00000000', 'hex'));

INSERT INTO "TokenTransactions"
  ("Id", "UserId", "Amount", "Reason", "RelatedEventId", "LookupTableVersion", "CatalogItemId", "CreatedAt")
VALUES
  (gen_random_uuid(), '<alice-sub-id>', 80, 'Driver/Excellent listing verified',
   'evt-manual-001', '1.0.0', 'test-club-item-001', now());
```

---

### TC-031: Balance reflects the award

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/balance"
```

**Expected** `200`:
```json
{ "balance": 80 }
```

---

### TC-032: Transaction history shows the earn entry

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions?page=1&pageSize=20"
```

**Expected** `200` — items array contains one entry:
```json
{
  "totalCount": 1,
  "items": [{
    "amount": 80,
    "reason": "Driver/Excellent listing verified",
    "lookupTableVersion": "1.0.0",
    "catalogItemId": "test-club-item-001",
    "relatedEventId": "evt-manual-001"
  }]
}
```

---

### TC-033: Second award for the same listing is rejected (idempotency)

Publish the same `ClubListingVerifiedIntegrationEvent` a second time (same `Id` and same `CatalogItemId`).

**Expected**: Balance stays at `80` — no double-credit.

---

## Section 5 — Spend Tokens

### TC-040: Successful spend debits the wallet

```bash
curl -k -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId": "<alice-sub-id>", "amount": 30, "orderId": "order-test-001"}'
```

**Expected** `200`:
```json
{ "newBalance": 50 }
```

Then verify balance:
```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/balance"
# Expected: { "balance": 50 }
```

---

### TC-041: Transaction history shows spend entry with null catalogItemId

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions"
```

**Expected**: The spend entry in `items` has:
- `"amount": -30`
- `"reason": "purchase debit"`
- `"lookupTableVersion": null`
- `"catalogItemId": null`

---

### TC-042: Duplicate orderId returns 409

```bash
curl -k -o /dev/null -w "%{http_code}" \
  -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId": "<alice-sub-id>", "amount": 30, "orderId": "order-test-001"}'
```

**Expected**: `409` — balance unchanged at `50`.

---

### TC-043: Spend that would go below zero returns 400

```bash
curl -k -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId": "<alice-sub-id>", "amount": 200, "orderId": "order-test-002"}'
```

**Expected** `400`:
```json
{ "error": "insufficient_balance", "detail": "User has 50 tokens; requested debit of 200 would result in a negative balance." }
```

Balance verified unchanged at `50`.

---

### TC-044: amount = 0 returns 400 validation error

```bash
curl -k -X POST "$PP/api/tokens/spend" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId": "<alice-sub-id>", "amount": 0, "orderId": "order-test-003"}'
```

**Expected** `400`:
```json
{ "error": "validation_error" }
```

No `TokenTransaction` inserted — verify:
```bash
psql "<connection-string>/tokendb" -c "SELECT COUNT(*) FROM \"TokenTransactions\" WHERE \"RelatedEventId\" = 'order-test-003';"
# Expected: COUNT = 0
```

---

## Section 6 — Pagination

### TC-050: pageSize defaults to 20

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions"
```

**Expected**: Response contains `"pageSize": 20`.

---

### TC-051: pageSize is capped at 100

```bash
curl -k -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions?pageSize=500"
```

**Expected**: Response returns at most 100 items regardless of `pageSize=500`.

---

### TC-052: Page beyond total returns empty items, not 404

```bash
curl -k -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" "$PP/api/tokens/transactions?page=999"
```

**Expected**: `200` with `"items": []`.

---

## Section 7 — User Isolation

### TC-060: Bob cannot see alice's balance

1. Get a JWT for bob (repeat the login step with `username=bob`)
2. Call balance with bob's token:

```bash
BOB_TOKEN=<bob-jwt>
curl -k -H "Authorization: Bearer $BOB_TOKEN" "$PP/api/tokens/balance"
```

**Expected**: `{ "balance": 0 }` — bob has no wallet, alice's balance is not returned.

---

### TC-061: Bob cannot see alice's transactions

```bash
curl -k -H "Authorization: Bearer $BOB_TOKEN" "$PP/api/tokens/transactions"
```

**Expected**: `{ "totalCount": 0, "items": [] }` — bob's empty history, not alice's.

---

## Test Summary Checklist

| ID | Area | Pass | Notes |
|---|---|---|---|
| TC-001 | Startup — service healthy | ☐ | |
| TC-002 | Startup — 28 seed rows | ☐ | |
| TC-003 | Startup — idempotent seed | ☐ | |
| TC-010 | Reward preview — valid pair | ☐ | |
| TC-011 | Reward preview — all 7 categories | ☐ | |
| TC-012 | Reward preview — missing category → 400 | ☐ | |
| TC-013 | Reward preview — missing condition → 400 | ☐ | |
| TC-014 | Reward preview — unknown category → 400 | ☐ | |
| TC-020 | Balance — new user = 0 | ☐ | |
| TC-021 | Balance — no wallet row created on read | ☐ | |
| TC-022 | Balance — unauth → 401 | ☐ | |
| TC-023 | Transactions — new user = empty 200 | ☐ | |
| TC-024 | Transactions — unauth → 401 | ☐ | |
| TC-030 | Earn — event credited correctly | ☐ | Workaround needed (T020 deferred) |
| TC-031 | Earn — balance updated | ☐ | |
| TC-032 | Earn — transaction history entry | ☐ | |
| TC-033 | Earn — duplicate event not double-credited | ☐ | |
| TC-040 | Spend — debit succeeds | ☐ | |
| TC-041 | Spend — transaction has null catalogItemId | ☐ | |
| TC-042 | Spend — duplicate orderId → 409 | ☐ | |
| TC-043 | Spend — insufficient balance → 400 | ☐ | |
| TC-044 | Spend — amount=0 → 400 | ☐ | |
| TC-050 | Pagination — default pageSize 20 | ☐ | |
| TC-051 | Pagination — capped at 100 | ☐ | |
| TC-052 | Pagination — page beyond total → 200 empty | ☐ | |
| TC-060 | Isolation — bob can't see alice's balance | ☐ | |
| TC-061 | Isolation — bob can't see alice's transactions | ☐ | |
