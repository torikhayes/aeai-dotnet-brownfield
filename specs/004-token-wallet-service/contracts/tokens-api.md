# API Contracts: Token Ledger Endpoints

**Service**: `PaymentProcessor` (internal Aspire mesh — not externally exposed)  
**Base path**: `/api/tokens`  
**Auth**: JWT Bearer (Identity.API) — required on balance and transactions endpoints  
**Feature**: 004-paymentprocessor-token-ledger

---

## GET /api/tokens/balance

Returns the authenticated user's current token balance. Returns `0` for users with no wallet row — does not create a row.

**Auth**: Required (Bearer JWT)

**Request**: No body or query parameters.

**Response 200**:
```json
{
  "balance": 150
}
```

**Response 401**: Unauthenticated request.

**Notes**:
- `balance` is always a non-negative integer.
- Response is not cached server-side — always reflects the live DB value.

---

## GET /api/tokens/transactions

Returns a paginated, reverse-chronological list of token transactions for the authenticated user.

**Auth**: Required (Bearer JWT)

**Query parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | `1` | 1-based page number |
| `pageSize` | int | `20` | Items per page (max 100) |

**Response 200**:
```json
{
  "totalCount": 7,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "amount": 80,
      "reason": "Driver/Excellent listing verified",
      "lookupTableVersion": "1.0.0",
      "createdAt": "2026-07-13T14:22:00Z",
      "relatedEventId": "evt-abc123",
      "catalogItemId": "catalog-item-uuid-abc"
    },
    {
      "id": "7cb1e3a2-...",
      "amount": -80,
      "reason": "purchase debit",
      "lookupTableVersion": null,
      "createdAt": "2026-07-13T16:05:00Z",
      "relatedEventId": "order-xyz789",
      "catalogItemId": null
    }
  ]
}
```

**Response 401**: Unauthenticated request.

**Notes**:
- `amount` is positive for earn transactions, negative for spend.
- `lookupTableVersion` is `null` on spend transactions.
- `reason` on earn transactions uses format `"{Category}/{Condition} listing verified"`.
- `reason` on spend transactions is `"purchase debit"`.
- Returns HTTP 200 with `{ "totalCount": 0, "page": 1, "pageSize": 20, "items": [] }` when the user has no transactions — never HTTP 404.
- When `page` exceeds the total page count, returns HTTP 200 with an empty `items` array.

---

## GET /api/tokens/reward-preview

Returns the current token reward amount for a given club category and condition grade. Used by the listing form UI to show sellers their reward upfront. No authentication required.

**Auth**: None

**Query parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `category` | string | ✅ | Club category: `Driver`, `Fairway Wood`, `Hybrid`, `Iron Set`, `Wedge`, `Putter`, `Other` |
| `condition` | string | ✅ | Condition grade: `New`, `Excellent`, `Good`, `Fair` |

**Response 200**:
```json
{
  "tokenAmount": 80,
  "tableVersion": "1.0.0"
}
```

**Response 400**: Missing, empty, or unrecognised `category` or `condition` value (not one of the defined enum values).

**Response 404**: Both parameters are valid enum values, but no seeded lookup row exists for that combination. This should not occur for any of the 28 defined category × condition combinations.

**Response 503**: Service temporarily unavailable — `tokendb` migrations have not yet completed (startup race). Callers should retry after a short backoff.

**Example**: `GET /api/tokens/reward-preview?category=Driver&condition=Excellent` → `{ "tokenAmount": 80, "tableVersion": "1.0.0" }`

---

## POST /api/tokens/spend

**Internal endpoint — Aspire mesh only. Not accessible outside the Aspire host.**

Atomically debits tokens from a user's wallet. Called service-to-service from Ordering.API during order creation when `TokensApplied > 0`. Idempotent on `orderId`.

**Auth**: Bearer JWT (caller must be authenticated; endpoint not externally reachable)

**Request body**:
```json
{
  "userId": "user-sub-claim-string",
  "amount": 80,
  "orderId": "order-guid-string"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `userId` | string | ✅ | The buyer's Identity sub claim |
| `amount` | int | ✅ | Tokens to debit (must be > 0) |
| `orderId` | string | ✅ | Used as `RelatedEventId` for idempotency |

**Response 200** (success):
```json
{
  "newBalance": 70
}
```

**Response 400** (insufficient balance or invalid input):
```json
{
  "error": "insufficient_balance",
  "detail": "User has 30 tokens; requested debit of 80 would result in a negative balance."
}
```

> Also returned with `"error": "validation_error"` when `amount` is 0 or negative.

**Response 409** (already debited for this orderId):
```json
{
  "error": "already_processed",
  "detail": "A spend transaction for orderId 'order-xyz' already exists."
}
```

**Response 503** (concurrency retries exhausted):
```json
{
  "title": "Service Unavailable",
  "detail": "Could not commit balance update after maximum retries. Try again shortly."
}
```

**Notes**:
- If `amount` would bring balance below 0, returns 400 (insufficient balance) — no partial debit.
- `amount` MUST be a positive integer (> 0); `amount = 0` returns 400 (validation_error).
- If the same `orderId` has already been processed, returns 409 regardless of whether the `amount` matches the original request. Callers should treat 409 as a successful prior debit only if they can independently confirm the original amount.
- Uses optimistic concurrency retry internally (max retries from `TokenOptions:MaxConcurrencyRetries`, default 3). When retries are exhausted, returns 503.
- Ordering.API MUST forward the buyer's JWT Bearer token from the incoming order request when calling this endpoint. A machine-identity token is not used.
