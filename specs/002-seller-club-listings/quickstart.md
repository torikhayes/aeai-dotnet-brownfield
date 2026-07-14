# Quickstart: Seller Club Listings (Feature 002)

**Prerequisites**: App running via `dotnet run --project src/eShop.AppHost` with Colima started.

---

## Step 1 — Verify the migration applied

```bash
curl http://localhost:5301/api/catalog/items?pageSize=1
```

Response should include `sellerId`, `condition`, `manufactureYear`, `photoUrls` fields on each item (will be `null` for admin-seeded items).

---

## Step 2 — Create a seller listing (authenticated)

Get a token by signing in via Identity.API, or use the test JWT pattern (Bearer token = user ID) in functional tests.

```bash
curl -X POST http://localhost:5301/api/catalog/items/listings \
  -H "Authorization: Bearer alice-user-id" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Callaway Driver Test",
    "price": 149.99,
    "catalogTypeId": 1,
    "catalogBrandId": 1,
    "condition": "Excellent",
    "manufactureYear": 2023,
    "description": "Barely used driver, great condition.",
    "photoUrls": ["https://example.com/photo1.jpg"]
  }'
```

Expected: `201 Created` with `Location: /api/catalog/items/{id}`

Verify: `GET` the returned location — confirm `sellerId = "alice-user-id"` and `availableStock = 1`.

---

## Step 3 — Browse listings by seller (public)

```bash
curl "http://localhost:5301/api/catalog/items/by-seller/alice-user-id"
```

Expected: paginated result containing the listing created in Step 2.  
The listing from Step 2 should appear. Admin-seeded items (with `sellerId = null`) should **not** appear.

---

## Step 4 — View my listings (authenticated)

```bash
curl http://localhost:5301/api/catalog/items/my-listings \
  -H "Authorization: Bearer alice-user-id"
```

Expected: only Alice's listing. Items belonging to other sellers do not appear.

Try with a different user:
```bash
curl http://localhost:5301/api/catalog/items/my-listings \
  -H "Authorization: Bearer bob-user-id"
```

Expected: empty result (Bob has no listings).

---

## Step 5 — Deactivate a listing (owner only)

```bash
# Get the item ID from Step 2's Location header, e.g. id=42
curl -X DELETE http://localhost:5301/api/catalog/items/listings/42 \
  -H "Authorization: Bearer alice-user-id"
```

Expected: `204 No Content`

Verify: item no longer appears in `GET /api/catalog/items/by-seller/alice-user-id` (filtered by `availableStock > 0`).  
Verify: item still appears in `GET /api/catalog/items/my-listings` for Alice (sellers see their own inactive listings).

---

## Step 6 — Ownership enforcement

Attempt to deactivate Alice's listing as Bob:

```bash
curl -X DELETE http://localhost:5301/api/catalog/items/listings/42 \
  -H "Authorization: Bearer bob-user-id"
```

Expected: `403 Forbidden`

---

## Step 7 — Admin items unaffected

```bash
curl http://localhost:5301/api/catalog/items/1
```

Expected: `sellerId: null` — admin-seeded items are unmodified by seller endpoints.

```bash
# Admin creation still works (requires auth)
curl -X POST http://localhost:5301/api/catalog/items \
  -H "Authorization: Bearer admin-user" \
  -H "Content-Type: application/json" \
  -d '{ "name": "Admin Club", "price": 99.99, "catalogTypeId": 1, "catalogBrandId": 1, "availableStock": 10, "restockThreshold": 2, "maxStockThreshold": 50, "onReorder": false }'
```

Expected: `201 Created` with `sellerId: null`.

---

## Automated Validation

Run the full functional test suite to validate all acceptance scenarios:

```bash
cd tests/Catalog.FunctionalTests && dotnet test
```

Expected: **48/48 passing**

Key test classes:
- `SellerListingTests` — T008–T021 (all seller listing flows)
- `CatalogApiTests` — existing admin endpoint tests (SC-005 regression check)
