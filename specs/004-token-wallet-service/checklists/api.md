# API Checklist: PaymentProcessor Token Ledger Extension

**Purpose**: Thorough release-gate requirements quality review — validates completeness, clarity, consistency, and measurability of API contracts, idempotency/concurrency, data integrity, and TDD requirements before implementation begins.
**Audience**: Author self-review
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md) | [contracts/tokens-api.md](../contracts/tokens-api.md) | [data-model.md](../data-model.md)
**Focus priority**: API contracts (B) → Concurrency & idempotency (A) → Data integrity & audit trail (D) → TDD coverage (C)

---

## Requirement Completeness

- [ ] CHK001 — Are error response formats (HTTP status + ProblemDetails shape) specified in the spec for all failure modes of `POST /api/tokens/spend`? (insufficient balance, already processed, concurrency retries exhausted) [Completeness, Spec §FR-011]
- [ ] CHK002 — Is a response format defined in the spec for `GET /api/tokens/transactions` when the user has zero transactions? (empty `items` array vs. 404 is unresolved in spec FR-004) [Completeness, Gap]
- [ ] CHK003 — Is the maximum allowed `pageSize` for `GET /api/tokens/transactions` documented in spec FR-004? (Currently defined only in contracts/tokens-api.md, not in the spec itself.) [Completeness, Spec §FR-004]
- [ ] CHK004 — Is the behavior of `POST /api/tokens/spend` defined for a user who has never earned tokens (no `TokenWallet` row)? (Should the handler treat the effective balance as 0 and return 400, or create a wallet? Spec is silent.) [Completeness, Gap]
- [ ] CHK005 — Are the valid enumeration values for `Category` and `Condition` in `ClubListingVerifiedIntegrationEvent` defined in spec FR-005? (Currently stated only in data-model.md seed table and contracts/tokens-api.md — not in the spec.) [Completeness, Spec §FR-005]
- [ ] CHK006 — Are the config key name, config section, and default value for "configurable maximum retries" documented in the spec or plan? (FR-010a requires configurability but gives no implementation anchor.) [Completeness, Spec §FR-010a]
- [ ] CHK007 — Are rate limiting or throttling requirements defined for any of the four token endpoints? [Completeness, Gap]
- [ ] CHK008 — Is a data retention requirement defined for `TokenTransaction` records? (Needed to prevent ambiguity during data-related changes — even "retain indefinitely in phase 1" is a stated decision.) [Completeness, Gap]

---

## Requirement Clarity

- [ ] CHK009 — Is the JWT claim used as the `UserId` key in `TokenWallet` and `TokenTransaction` explicitly named in the spec? (e.g., the `sub` claim — required to unambiguously implement `GET /api/tokens/balance` and caller identity resolution) [Clarity, Spec §FR-009]
- [ ] CHK010 — Is "active lookup table version at processing time" defined with a precise query rule in the spec, not only in data-model.md? (The `max(EffectiveFrom) ≤ now` semantics must be traceable to a spec FR for the rule to be authoritative.) [Clarity, Spec §FR-006]
- [ ] CHK011 — Is "configurable maximum number of retries" in FR-010a quantified with a default value? ("Configurable" without a default makes the behavior untestable and makes the SC-005 test indeterminate.) [Clarity, Spec §FR-010a]
- [ ] CHK012 — Is the `TokenTransaction.Reason` format string for earn transactions (e.g., `"{Category}/{Condition} listing verified"`) defined in a spec FR, rather than only in contracts/tokens-api.md? (If the format is authoritative, it belongs in the spec.) [Clarity, Gap]
- [ ] CHK013 — Is the behavior defined for `POST /api/tokens/spend` when the same `orderId` is resubmitted with a *different* `amount`? (The 409 idempotency path in contracts/tokens-api.md does not distinguish matched vs. mismatched amount retries.) [Clarity, Spec §FR-011]
- [ ] CHK014 — Is the source of the JWT used to call `POST /api/tokens/spend` from Ordering.API specified? (Does Ordering.API forward the buyer's JWT, or obtain a machine-identity token? The auth mechanism for service-to-service calls is undefined.) [Clarity, Spec §FR-011]

---

## Requirement Consistency

- [ ] CHK015 — Are the valid `Category` enum values consistent across spec FR-005 (`ClubListingVerifiedIntegrationEvent`), contracts/tokens-api.md (`reward-preview` query params), and data-model.md (seed data categories)? [Consistency]
- [ ] CHK016 — Is the inclusion of `relatedEventId` in the `GET /api/tokens/transactions` response explicitly required by spec FR-004 or FR-009? (Spec FR-009 defines `RelatedEventId` as a storage field; contracts expose it in the API response — is this intentional and authorised?) [Consistency, Spec §FR-004, §FR-009]
- [ ] CHK017 — Do the idempotency keys for the spend path (`orderId` in FR-011) and the earn path (`EventId` + `CatalogItemId` in FR-008) derive consistently from the same framing in FR-008? (FR-008 scopes "all token award operations" to earn side — is spend idempotency governed by its own FR or a gap?) [Consistency, Spec §FR-008, §FR-011]
- [ ] CHK018 — Is FR-011's network isolation requirement ("not registered on the public-facing port") expressed precisely enough to drive implementation, or does it defer to the Aspire mesh conventions described only in research.md? (Spec FR should be the authority; research.md is non-normative.) [Consistency, Spec §FR-011]

---

## Acceptance Criteria Quality

- [ ] CHK019 — Is SC-004 ("balance endpoint responds in under 200ms") measurable without a defined load baseline? (200ms at what concurrency level or request rate? Without a baseline, the criterion cannot be independently verified.) [Measurability, Spec §SC-004]
- [ ] CHK020 — Is SC-005 ("exactly the correct net debit applied") unambiguous? (When two concurrent spends of 80 each are attempted against a balance of 80, the expected outcome should be stated explicitly: exactly one succeeds, one returns a specified error code.) [Measurability, Spec §SC-005]
- [ ] CHK021 — Is SC-006 ("100% path coverage") quantified using a specific coverage metric? ("Path coverage" could mean line, branch, or mutation coverage — the difference in rigor is significant.) [Measurability, Spec §SC-006]
- [ ] CHK022 — Is SC-003 ("every earn transaction record includes a `LookupTableVersion`") verifiable given that `LookupTableVersion` is nullable in FR-009's entity definition? (SC-003 must explicitly scope itself to earn transactions, or conflict with the nullable field.) [Conflict, Spec §SC-003, §FR-009]

---

## Scenario Coverage

- [ ] CHK023 — Are requirements defined for the scenario where `PaymentProcessor` crashes mid-award (DB write succeeded, RabbitMQ ack not sent)? (At-least-once delivery will redeliver — FR-008 RelatedEventId deduplication covers this, but the scenario is not explicitly stated as a covered case.) [Coverage, Spec §FR-008]
- [ ] CHK024 — Are requirements defined for two concurrent `ClubListingVerifiedIntegrationEvent` messages arriving simultaneously for the same `CatalogItemId`? (Both handlers attempt to insert into `TokenAwardedListing`; only one should succeed — the expected resolution is not documented.) [Coverage, Gap]
- [ ] CHK025 — Is the response defined for `GET /api/tokens/transactions` when `page` exceeds the total number of pages? (Empty `items` array vs. 400 validation error — not addressed in spec FR-004 or contracts.) [Coverage, Gap]
- [ ] CHK026 — Is the behavior defined for `GET /api/tokens/reward-preview` when a valid-looking but unrecognised `category` or `condition` value is supplied? (contracts/tokens-api.md documents a 404 as "should not occur" — that is not a requirement.) [Coverage, Spec §FR-012]
- [ ] CHK027 — Is the "platform admin listing (no SellerId)" scenario (User Story 1, Scenario 3) traceable to a named FR? (The no-credit outcome appears only in acceptance scenarios, not as a functional requirement.) [Coverage, Gap]
- [ ] CHK028 — Are requirements defined for the error returned to the caller when optimistic concurrency retries are exhausted, as required by FR-010a? (The retry loop is specified; the terminal failure path is not.) [Coverage, Spec §FR-010a]

---

## Edge Case Coverage

- [ ] CHK029 — Is the behavior of `GET /api/tokens/reward-preview` defined for a cold-start race where `tokendb` migrations have not yet completed? (Startup ordering via `WaitFor(tokenDb)` is in the plan, but the endpoint behaviour during the window is not specified.) [Edge Case, Gap]
- [ ] CHK030 — Is the handler behaviour defined for `SellerId` values that are null, empty, or whitespace in `ClubListingVerifiedIntegrationEvent`? (User Story 1, Scenario 3 addresses "no SellerId" but the spec does not define whether empty/whitespace values receive the same treatment.) [Edge Case, Spec §FR-006]
- [ ] CHK031 — Is the behaviour of `GET /api/tokens/balance` defined when the `tokendb` connection is unavailable? (503 vs. ProblemDetails propagation — not addressed in the spec.) [Edge Case, Gap]
- [ ] CHK032 — Is the behaviour defined for `POST /api/tokens/spend` when `amount = 0`? (A zero-amount debit does not reduce the balance but the idempotency logic, validation, and response are unspecified.) [Edge Case, Gap]

---

## Non-Functional Requirements

- [ ] CHK033 — Are response-time requirements specified for `GET /api/tokens/transactions` and `GET /api/tokens/reward-preview`? (SC-004 covers only the balance endpoint; the other two are unconstrained.) [Non-Functional, Gap]
- [ ] CHK034 — Is the performance target in SC-004 (200ms) defined against a specific infrastructure tier or load level? (Without a baseline environment or concurrency definition, the target is not independently verifiable.) [Non-Functional, Spec §SC-004]
- [ ] CHK035 — Are observability requirements defined for token ledger operations? (e.g., structured log on every award/debit, OpenTelemetry traces for balance mutations — not addressed in any spec FR, though Aspire service defaults provide the pipeline.) [Non-Functional, Gap]

---

## Dependencies & Assumptions

- [ ] CHK036 — Is the obligation for `Catalog.API` to publish `ClubListingVerifiedIntegrationEvent` cross-referenced between spec 004 (FR-005) and spec 002 (Seller Club Listings)? (FR-005 defines the event shape from `PaymentProcessor`'s perspective but does not name the owning spec.) [Dependency, Gap]
- [ ] CHK037 — Is the "lookup entries are never deleted or updated" rule expressed as an explicit spec constraint in FR-007, rather than only as an implementation note in data-model.md? [Assumption, Spec §FR-007]
- [ ] CHK038 — Is the "no token expiry in this phase" assumption documented as a bounded decision with a reference to which future spec will introduce expiry? [Assumption]

---

## Ambiguities & Conflicts

- [ ] CHK039 — Is the "reviewed before implementation" gate in FR-013 defined? (Who reviews? A PR check, a pairing session, or a named role? Without a definition, this requirement cannot be objectively satisfied.) [Ambiguity, Spec §FR-013]
- [ ] CHK040 — Does SC-003 ("every earn transaction includes a `LookupTableVersion`") conflict with FR-009's definition of `LookupTableVersion` as nullable? (If nullable is required to accommodate spend transactions, SC-003 must explicitly scope itself to earn entries only — currently it does not.) [Conflict, Spec §SC-003, §FR-009]
