---
description: "Task list for 004-paymentprocessor-token-ledger"
---

# Tasks: PaymentProcessor Token Ledger Extension

**Feature**: 004-paymentprocessor-token-ledger  
**Input**: `specs/004-token-wallet-service/` — plan.md, spec.md, data-model.md, contracts/tokens-api.md, research.md, quickstart.md  
**Tests**: TDD mandatory on all ledger, award-calculation, and balance-mutation paths (FR-013 / Principle V)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: User story label (US1–US4)
- Exact file paths included in every task

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new test project and add NuGet packages required by every subsequent phase.

- [X] T001 Create tests/PaymentProcessor.UnitTests/PaymentProcessor.UnitTests.csproj (xUnit, Microsoft.Testing.Platform, Moq, EF Core InMemory — follow tests/Basket.UnitTests pattern)
- [X] T002 Update src/PaymentProcessor/PaymentProcessor.csproj — add `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Relational` package references (follow src/Webhooks.API pattern)

**Checkpoint**: Test project compiles; PaymentProcessor.csproj references EF Core + Npgsql.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: AppHost wiring, all four entity model classes, DbContext, seeder, migrations, DI registration, and config updates that every user-story phase depends on.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T003 Update src/eShop.AppHost/Program.cs — declare `var tokenDb = postgres.AddDatabase("tokendb")`, add `.WithReference(tokenDb)` and `.WithEnvironment("Identity__Url", identityEndpoint)` to the `payment-processor` resource (follow `webhooks-api` reference pattern)
- [X] T004 [P] Create src/PaymentProcessor/TokenLedger/Model/TokenWallet.cs — `UserId` string PK, `Balance` int, `[Timestamp] byte[] RowVersion` per data-model.md
- [X] T005 [P] Create src/PaymentProcessor/TokenLedger/Model/TokenTransaction.cs — `Guid Id`, `string UserId`, `int Amount`, `string Reason`, `string RelatedEventId`, `string? LookupTableVersion`, `string? CatalogItemId`, `DateTime CreatedAt` per data-model.md (`CatalogItemId` populated on earn transactions only)
- [X] T006 [P] Create src/PaymentProcessor/TokenLedger/Model/TokenAwardLookupEntry.cs — `Guid Id`, `string ClubCategory`, `string ConditionGrade`, `int TokenAmount`, `string TableVersion`, `DateTime EffectiveFrom` per data-model.md
- [X] T007 [P] Create src/PaymentProcessor/TokenLedger/Model/TokenAwardedListing.cs — `string CatalogItemId` PK, `Guid TransactionId`, `DateTime AwardedAt` per data-model.md
- [X] T008 Create src/PaymentProcessor/TokenLedger/Infrastructure/TokenDbContext.cs — register all four entities; configure HasKey, HasIndex, IsRowVersion(), and HasDefaultValueSql per data-model.md EF Core configuration blocks
- [X] T009 Create src/PaymentProcessor/TokenLedger/Infrastructure/TokenDbSeeder.cs — seed all 28 TokenAwardLookupEntry rows (v1.0.0, EffectiveFrom = epoch) per the seed table in data-model.md; follow `CatalogContextSeed` pattern; seeder MUST be idempotent (check row count or use `ON CONFLICT DO NOTHING` before inserting to avoid re-seeding on restart)
- [X] T010 Add EF Core initial migration for TokenDbContext — run `dotnet ef migrations add InitialTokenLedger --context TokenDbContext` from src/PaymentProcessor and commit generated files under src/PaymentProcessor/TokenLedger/Infrastructure/Migrations/
- [X] T011 Create src/PaymentProcessor/TokenLedger/Services/TokenLedgerExtensions.cs — `AddTokenLedger()` extension: calls `builder.AddNpgsqlDbContext<TokenDbContext>("tokendb")` and `builder.Services.AddMigration<TokenDbContext, TokenDbSeeder>()`; registers `TokenLedgerService` as scoped
- [X] T012 Update src/PaymentProcessor/appsettings.json — add `"TokenOptions": { "MaxConcurrencyRetries": 3 }` section and bind to a `TokenOptions` record
- [X] T013 Update src/PaymentProcessor/GlobalUsings.cs — add global usings for `PaymentProcessor.TokenLedger.Model`, `PaymentProcessor.TokenLedger.Infrastructure`, `PaymentProcessor.TokenLedger.Services`

**Checkpoint**: `dotnet build src/PaymentProcessor` passes; AppHost adds `tokendb`; all entity and context classes compile.

---

## Phase 3: User Story 1 — Earn Tokens When a Listing Is Verified (Priority: P1) 🎯 MVP

**Goal**: When `ClubListingVerifiedIntegrationEvent` is received, the seller's balance is credited once using the active lookup table version. Idempotent on `EventId`; one award per `CatalogItemId`.

**Independent Test**: Process a `ClubListingVerifiedIntegrationEvent` for a Driver/Excellent listing → call `GET /api/tokens/balance` as the seller → balance increased by 80 (v1.0.0 lookup value).

> **TDD gate**: T014 and T015 MUST produce failing tests before T017–T019 are implemented.

### Tests for User Story 1 ⚠️ Write first — must FAIL before implementation

- [X] T014 [US1] Write failing unit tests in tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs covering: (a) AwardTokens creates wallet on first award; (b) duplicate EventId does not double-credit; (c) duplicate CatalogItemId from a resubmitted listing is rejected; (d) DbUpdateConcurrencyException triggers retry up to MaxConcurrencyRetries; (e) no SellerId → no credit, no error; (f) successful award emits a structured `Information` log entry with UserId, Amount, Reason, and RelatedEventId fields (FR-015)
- [X] T015 [P] [US1] Write failing unit tests in tests/PaymentProcessor.UnitTests/TokenLedger/TokenAwardLookupTests.cs covering: (a) correct seed values returned for all 28 (category × condition) pairs; (b) active version is latest EffectiveFrom ≤ UtcNow; (c) future EffectiveFrom row is not returned

### Implementation for User Story 1

- [X] T016 [P] [US1] Create src/PaymentProcessor/IntegrationEvents/ClubListingVerifiedIntegrationEvent.cs — `record ClubListingVerifiedIntegrationEvent(Guid Id, DateTime CreationDate, string SellerId, string CatalogItemId, string Category, string Condition) : IntegrationEvent(Id, CreationDate)` per data-model.md
- [X] T017 [US1] Implement `TokenLedgerService.AwardTokens(ClubListingVerifiedIntegrationEvent)` in src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs — resolve active lookup entry, atomically insert TokenTransaction + TokenAwardedListing + upsert TokenWallet with RowVersion retry loop (max = TokenOptions.MaxConcurrencyRetries); return early on duplicate EventId or CatalogItemId; log when no SellerId; emit FR-015 structured `Information` log entry (UserId, Amount, Reason, RelatedEventId) and OpenTelemetry span attributes on successful award
- [X] T018 [US1] Create src/PaymentProcessor/IntegrationEvents/EventHandling/ClubListingVerifiedIntegrationEventHandler.cs — `IIntegrationEventHandler<ClubListingVerifiedIntegrationEvent>` that calls `TokenLedgerService.AwardTokens()`; follow `OrderStatusChangedToStockConfirmedIntegrationEventHandler` pattern
- [X] T019 [US1] Register `ClubListingVerifiedIntegrationEventHandler` and `ClubListingVerifiedIntegrationEvent` subscription in src/PaymentProcessor/Program.cs via `app.UseSubscription<ClubListingVerifiedIntegrationEvent, ClubListingVerifiedIntegrationEventHandler>()`; call `AddTokenLedger()` on the builder
- [ ] T020 [US1] Add `ClubListingVerifiedIntegrationEvent` publisher to Catalog.API — create src/Catalog.API/IntegrationEvents/Events/ClubListingVerifiedIntegrationEvent.cs (matching payload shape) and publish from the listing verification code path when `photoCount >= 2` (uniform 2-photo minimum across all condition grades, per spec 007 edge cases); follow `OrderStartedIntegrationEvent` publish pattern in Catalog.API. ⚠️ **Cross-spec dependency**: requires spec 007 (Trust & Safety) to implement the listing verification flow in Catalog.API first — defer T020 if spec 007 is not yet implemented

**Checkpoint**: Run app; submit a club listing; confirm `GET /api/tokens/balance` returns the correct lookup amount. Duplicate event delivery must not double-credit.

---

## Phase 4: User Story 2 — Check Token Balance (Priority: P1)

**Goal**: Authenticated users can call `GET /api/tokens/balance` and receive their current integer balance. Returns `0` for users with no wallet row without writing to the database.

**Independent Test**: Call `GET /api/tokens/balance` with a valid JWT — returns `{ "balance": 0 }` for a new user; returns correct value after awards.

> **TDD gate**: T021 MUST produce a failing test before T022 is implemented.

### Tests for User Story 2 ⚠️ Write first — must FAIL before implementation

- [X] T021 [US2] Add failing unit tests to tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs: (a) GetBalance returns 0 for unknown UserId without DB write; (b) GetBalance returns correct value when wallet exists

### Implementation for User Story 2

- [X] T022 [US2] Implement `TokenLedgerService.GetBalance(string userId)` in src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs — `FirstOrDefaultAsync` by UserId; return `wallet?.Balance ?? 0`; no write on read
- [X] T023 [US2] Add `GET /api/tokens/balance` route to src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs — extract `UserId` from JWT `sub` claim; call `GetBalance()`; return `{ "balance": n }` (200) or 401 (no JWT); follow contracts/tokens-api.md response shape
- [X] T024 [US2] Wire auth and `TokensApi.MapTokensApi()` into src/PaymentProcessor/Program.cs — call `builder.Services.AddDefaultAuthentication()` (requires `Identity__Url` env var added in T003); call `app.MapTokensApi()` after `app.MapDefaultEndpoints()`

**Checkpoint**: `GET /api/tokens/balance` with a valid JWT returns 200 with correct balance. Unauthenticated request returns 401.

---

## Phase 5: User Story 3 — View Token Transaction History (Priority: P2)

**Goal**: Authenticated users can retrieve a reverse-chronological, paginated transaction log showing amounts, reasons, timestamps, and lookup table version for earn entries.

**Independent Test**: After earning and spending tokens, call `GET /api/tokens/transactions?page=1&pageSize=20` — response includes both entries in reverse-chronological order; earn entry has non-null `lookupTableVersion`, non-null `relatedEventId`, and non-null `catalogItemId`; spend entry has `null` for both `lookupTableVersion` and `catalogItemId`.

> **TDD gate**: T025a MUST produce failing tests before T025 is implemented.

### Tests for User Story 3 ⚠️ Write first — must FAIL before implementation

- [X] T025a [US3] Write failing unit tests in tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs covering: (a) GetTransactions returns empty result (totalCount 0, empty items) for unknown UserId; (b) results are returned in reverse-chronological order; (c) earn transaction item has non-null `catalogItemId` and `relatedEventId`; (d) spend transaction item has `null` `catalogItemId`; (e) `pageSize` cap of 100 is enforced; (f) page beyond total count returns empty items array

### Implementation for User Story 3

- [X] T025 [US3] Implement `TokenLedgerService.GetTransactions(string userId, int page, int pageSize)` in src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs — query `TokenTransactions` filtered by `UserId`, ordered by `CreatedAt DESC`, paginated; return `(totalCount, items)` including `relatedEventId` and `catalogItemId` (null on spend transactions) per FR-004 and FR-009
- [X] T026 [US3] Add `GET /api/tokens/transactions` route to src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs — accept `page` (default 1) and `pageSize` (default 20, max 100) query params; call `GetTransactions()`; return paginated response per contracts/tokens-api.md `GET /api/tokens/transactions` shape

**Checkpoint**: `GET /api/tokens/transactions` returns paginated reverse-chronological log. `lookupTableVersion` is populated on earn rows and `null` on spend rows.

---

## Phase 6: User Story 4 — Preview Token Reward Before Listing (Priority: P2)

**Goal**: Unauthenticated callers can query `GET /api/tokens/reward-preview?category=X&condition=Y` and receive the current lookup table amount. No wallet or user context required.

**Independent Test**: `GET /api/tokens/reward-preview?category=Driver&condition=Excellent` → `{ "tokenAmount": 80, "tableVersion": "1.0.0" }`.

> **TDD gate**: T026a MUST produce failing tests before T027 is implemented (valuation calculation — Principle V).

### Tests for User Story 4 ⚠️ Write first — must FAIL before implementation

- [X] T026a [US4] Write failing unit tests in tests/PaymentProcessor.UnitTests/TokenLedger/TokenAwardLookupTests.cs covering: (a) GetRewardPreview returns correct tokenAmount and tableVersion for a valid (category, condition) pair; (b) GetRewardPreview returns null for unknown combination; (c) active entry is the latest EffectiveFrom ≤ UtcNow, not a future-dated row

### Implementation for User Story 4

- [X] T027 [P] [US4] Implement `TokenLedgerService.GetRewardPreview(string category, string condition)` in src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs — run the active-entry query (EffectiveFrom ≤ UtcNow, ordered desc, FirstOrDefaultAsync); return `(tokenAmount, tableVersion)` or null if not found
- [X] T028 [P] [US4] Add `GET /api/tokens/reward-preview` route to src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs — validate `category` and `condition` query params (400 on missing/invalid); call `GetRewardPreview()`; return 200 `{ tokenAmount, tableVersion }` or 404 when no entry found; no `[Authorize]` attribute per contracts/tokens-api.md

**Checkpoint**: `GET /api/tokens/reward-preview?category=Iron+Set&condition=Good` returns `{ "tokenAmount": 70, "tableVersion": "1.0.0" }` without authentication.

---

## Phase 7: Internal Spend Endpoint (FR-011 — prerequisite for spec 005)

**Goal**: `POST /api/tokens/spend` atomically debits a user's wallet. Idempotent on `orderId`. Rejects debits that would result in a negative balance. Accessible only within the Aspire mesh.

> **TDD gate**: T029 MUST produce failing tests before T030 is implemented.

### Tests for Spend Path ⚠️ Write first — must FAIL before implementation

- [X] T029 Add failing unit tests to tests/PaymentProcessor.UnitTests/TokenLedger/TokenLedgerServiceTests.cs: (a) SpendTokens debits balance and inserts TokenTransaction; (b) insufficient balance returns explicit error, balance unchanged; (c) duplicate orderId (RelatedEventId) returns 409 without double-debit; (d) concurrent debit triggers RowVersion retry; (e) final balance is ≥ 0 after concurrent debits exhausting balance; (f) `amount = 0` or negative returns validation error with no DB write and no TokenTransaction inserted (FR-011); (g) successful debit emits a structured `Information` log entry with UserId, Amount, Reason, and RelatedEventId fields (FR-015)

### Implementation for Spend Endpoint

- [X] T030 Implement `TokenLedgerService.SpendTokens(string userId, int amount, string orderId)` in src/PaymentProcessor/TokenLedger/Services/TokenLedgerService.cs — check duplicate `RelatedEventId`; load wallet; reject if `balance - amount < 0`; decrement balance; insert TokenTransaction (`amount` as negative, `reason = "purchase debit"`, `CatalogItemId = null`); wrap in RowVersion retry loop up to `MaxConcurrencyRetries`; emit FR-015 structured `Information` log entry (UserId, Amount, Reason, RelatedEventId) and OpenTelemetry span attributes on successful debit
- [X] T031 Add `POST /api/tokens/spend` route to src/PaymentProcessor/TokenLedger/Apis/TokensApi.cs — accept `{ userId, amount, orderId }` body; validate fields (400 on invalid); call `SpendTokens()`; return 200 `{ newBalance }` on success, 400 on insufficient balance, 409 on duplicate orderId per contracts/tokens-api.md

**Checkpoint**: Concurrent spend requests never push balance below zero (SC-005). Duplicate `orderId` returns 409 without re-debiting.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, format compliance, and docs.

- [ ] T032 [P] Run `dotnet test tests/PaymentProcessor.UnitTests` — confirm 100% **branch** coverage (as reported by `dotnet-coverage`) of idempotency, concurrent debit, and award calculation paths (SC-006); fix any failing tests
- [ ] T033 [P] Validate EF Core migration runs cleanly end-to-end — `dotnet run --project src/eShop.AppHost` → Aspire dashboard shows `payment-processor` healthy, `tokendb` migrations applied, lookup table seeded with 28 rows
- [ ] T034 [P] Run quickstart.md manual smoke tests — balance endpoint, reward-preview endpoint, award via listing event — confirm responses match contracts/tokens-api.md shapes

**Checkpoint**: All tests green; Aspire stack healthy; all endpoints respond per contract.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately; T001 and T002 are parallel
- **Phase 2 (Foundational)**: Depends on Phase 1 — T003 first; T004–T007 parallel after T003; T008 depends on T004–T007; T009 depends on T006 **and T008** (seeder references TokenDbContext); T010 depends on T008–T009; T011 depends on T008; T012–T013 independent
- **Phase 3 (US1)**: Depends on Phase 2 — T014 first (write failing test); T015–T016 parallel; T017 depends on T014–T016; T018 depends on T017; T019 depends on T018; T020 independent of T014–T019 (Catalog.API side)
- **Phase 4 (US2)**: Depends on Phase 2 — T021 first (write failing test); T022 depends on T021; T023 depends on T022; T024 depends on T023
- **Phase 5 (US3)**: Depends on Phase 2 and Phase 4 (auth wiring from T024) — T025a first (write failing test); T025 depends on T025a; T025 → T026
- **Phase 6 (US4)**: Depends on Phase 2 — T026a first (write failing test); T027 and T028 are parallel after T026a
- **Phase 7 (Spend)**: Depends on Phase 2 and Phase 4 (TokenLedgerService scaffolded in T022) — T029 first; T030 depends on T029; T031 depends on T030
- **Phase 8 (Polish)**: Depends on all prior phases — T032–T034 parallel

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 2. Independent — no dependency on US2–US4
- **US2 (P1)**: Starts after Phase 2. Independent — no dependency on US1, but Phase 4 auth wiring (T024) is shared by US3
- **US3 (P2)**: Starts after Phase 2 + T024 (auth). Uses `TokensApi.cs` started in Phase 4
- **US4 (P2)**: Starts after Phase 2. Fully independent — unauthenticated endpoint, only needs the seeded lookup table

### Parallel Opportunities

- **Phase 1**: T001 ‖ T002
- **Phase 2**: T004 ‖ T005 ‖ T006 ‖ T007 (all four entity files); T012 ‖ T013
- **Phase 3 US1**: T015 ‖ T016 (tests + event class); US1 (Phase 3) ‖ US2 (Phase 4) ‖ US4 (Phase 6) once Phase 2 is done
- **Phase 6 US4**: T026a (then T027 ‖ T028)
- **Phase 8**: T032 ‖ T033 ‖ T034

---

## Parallel Example: Working on US1 + US4 simultaneously after Phase 2

```
# Engineer A — US1 earn flow
T014 → T017 → T018 → T019 → T020

# Engineer B — US4 preview endpoint (fully independent)
T026a → T027 → T028

# Engineer C — US2 balance endpoint
T021 → T022 → T023 → T024
```

---

## Implementation Strategy

### MVP Scope (deliver this first)

**Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2)** gives a shippable vertical slice:

1. AppHost declares `tokendb`
2. Entities, DbContext, seeder, migrations in place
3. `ClubListingVerifiedIntegrationEvent` processed → wallet credited
4. `GET /api/tokens/balance` returns live balance

All four phases together validate the core token-earn loop end-to-end.

### Incremental Delivery Order

1. **Phase 1–2**: Foundation (must complete before anything else)
2. **Phase 3 + Phase 4** (parallel): US1 earn flow + US2 balance read — MVP complete
3. **Phase 5**: US3 transaction history
4. **Phase 6**: US4 reward preview (low risk, independent)
5. **Phase 7**: Spend endpoint (needed by spec 005 checkout)
6. **Phase 8**: Polish and validation

---

## Task Summary

| Phase | Tasks | Count |
|---|---|---|
| Phase 1: Setup | T001–T002 | 2 |
| Phase 2: Foundational | T003–T013 | 11 |
| Phase 3: US1 — Earn Tokens | T014–T020 | 7 |
| Phase 4: US2 — Check Balance | T021–T024 | 4 |
| Phase 5: US3 — Transaction History | T025a, T025–T026 | 3 |
| Phase 6: US4 — Reward Preview | T026a, T027–T028 | 3 |
| Phase 7: Spend Endpoint | T029–T031 | 3 |
| Phase 8: Polish | T032–T034 | 3 |
| **Total** | | **36** |

**TDD tasks** (must fail before implementation): T014, T015, T021, T025a, T026a, T029  
**Parallel-eligible tasks**: T001, T002, T004–T007, T012–T013, T015–T016, T027–T028, T032–T034  
**Suggested MVP**: Phases 1–4 (T001–T024) — delivers earn + balance read end-to-end
