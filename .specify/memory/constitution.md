<!--
Sync Impact Report
Version change: 1.1.0 → 2.0.0 (MAJOR: updated the technology constraint from .NET 9 to
  .NET 10 to match the current repo target)
Modified principles:
  - Technology Constraints — updated the required solution/runtime baseline from .NET 9 to
    .NET 10 while preserving the existing Aspire service topology
Added sections: none
Removed sections: none
Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check remains valid; no structural
    template change required
  ✅ .specify/templates/spec-template.md — no constitution-specific references found
  ✅ .specify/templates/tasks-template.md — no constitution-specific references found
  ✅ README.md — updated .NET 9 setup references to .NET 10
  ✅ docs/dev-workflow.md — updated SDK guidance to .NET 10
  ✅ docs/architecture.md — updated runtime references to .NET 10
Follow-up TODOs: none
-->

<!--
Sync Impact Report (1.0.0)
Version change: [TEMPLATE] → 1.0.0 (initial ratification)
Modified principles: none (first fill of template placeholders)
Added sections:
  - Core Principles I–VI (Microservices Continuity; Token Ledger Integrity & Non-Convertibility;
    Attribute-Based, Anti-Fraud Valuation; Trust & Safety in Physical Trades; Risk-Based Testing
    Discipline; Marketplace Scope Boundary: No Fulfillment Ownership)
  - Technology Constraints
  - Development Workflow & Governance
Removed sections: none
Follow-up TODOs: none blocking. RATIFICATION_DATE set to the date of this ratification session.
-->

# 2001: A Golf Odyssey Constitution

## Core Principles

### I. Microservices Continuity
Repurpose eShop's existing .NET Aspire microservices rather than rewriting from scratch:
Catalog.API becomes the club listing service, Basket.API becomes the trade cart, Ordering.API
becomes the trade/transaction service, PaymentProcessor becomes the token wallet & ledger, and
Identity.API remains the authentication/authorization boundary. New golf-specific domain logic
MUST be added within these service boundaries — or as new services following the same
Aspire/EventBus conventions — not as a parallel architecture.

Rationale: preserves the proven Aspire orchestration, EventBus integration-event patterns, and
service boundaries already established in this codebase, minimizing risk while re-theming the
domain.

### II. Token Ledger Integrity & Non-Convertibility (NON-NEGOTIABLE)
Tokens are the marketplace's unit of trust and MUST be handled with the same rigor as real
money, even though they aren't real money:

- Every token-affecting action (listing reward, purchase debit, refund, cancellation) MUST be
  idempotent and fully auditable — replaying the same request MUST NOT double-credit or
  double-debit a balance.
- Tokens MUST NOT be purchased with real currency and MUST NOT be cashed out or redeemed for
  real currency, gift cards, or any other real-world value. Tokens exist only to circulate
  within the marketplace.

Rationale: idempotency and auditability prevent silent economic corruption and give a reliable
trail for investigating disputes; non-convertibility keeps the platform outside
money-transmission and gambling regulatory territory.

### III. Attribute-Based, Anti-Fraud Valuation
Token rewards for a listed club MUST be computed from verifiable club attributes (category,
condition grade) rather than a flat rate or a seller's arbitrary declared value. Valuation
logic MUST run server-side and be re-derivable from stored attributes — a client MUST NOT be
able to supply the token amount directly. Sellers who misrepresent attributes to inflate their
reward are subject to listing removal and token clawback.

Token issuance MUST follow these specifics:

- **Valuation mechanism**: the token amount for a listing MUST come from a maintained lookup
  table keyed on (club category × condition grade) — e.g., driver / fairway wood / hybrid /
  iron set / wedge / putter / other, crossed with New / Excellent / Good / Fair. The table MAY
  be retuned over time, but any given listing's reward MUST remain re-derivable from the table
  version in effect when that listing was awarded (audit trail per Principle II).
- **Award timing**: tokens MUST be credited only after an automated verification step passes
  (e.g., condition-grade plausibility check, required photo count/presence) — not at the instant
  a listing is submitted, and MUST NOT be gated on manual/human review.
- **Anti-farming caps**: no per-user rate caps, active-listing limits, or unsold-listing
  clawbacks are mandated by this constitution today. This is a deliberate choice, not an
  omission — such limits MAY be introduced later as an ordinary feature/policy change without
  requiring a constitutional amendment, unless they would conflict with a Core Principle.

Rationale: rewards must reflect real club value to make the marketplace credible, but that only
holds if valuation is tamper-resistant. A category × condition lookup table gives differentiated,
auditable rewards without the maintenance burden of full brand/model pricing. Automated (not
instant, not human-gated) verification balances fraud resistance against seller-experience
latency. Leaving anti-farming caps unconstrained keeps the constitution focused on what's
actually non-negotiable today, while Principle II's audit trail still lets clawback/caps be
added later as a normal feature.

### IV. Trust & Safety in Physical Trades
Clubs are physical goods, and condition claims materially affect trade outcomes. The following
are constitutional requirements, not optional feature polish:

- A listing's condition grade MUST be backed by evidence (photos) sufficient for a reasonable
  buyer to verify the claim.
- Opening a dispute on a completed trade MUST freeze the associated token settlement (both the
  buyer's spent tokens and the seller's earned tokens) until the dispute is resolved.
- A seller found to have misrepresented a club's condition is accountable for the trade outcome
  (token reversal, listing/account penalties per trust & safety policy).

Rationale: unresolved disputes over physical-goods condition are the single biggest threat to
marketplace trust, so these guarantees are locked in at the constitutional level rather than
left to a later feature spec.

### V. Risk-Based Testing Discipline
Test rigor scales with financial/trust blast radius rather than being applied uniformly:

- Code paths touching the token ledger, trade state machine, or valuation calculation MUST
  follow test-first development (TDD): a failing test is written and reviewed before
  implementation, and the suite MUST cover idempotent-retry and concurrent-balance-mutation
  scenarios.
- All other code (browsing, UI, notifications, etc.) follows standard PR review with tests
  required, but not strictly test-first.

Rationale: focuses the most expensive engineering discipline on the paths where correctness has
real economic and trust consequences, rather than mandating TDD everywhere.

### VI. Marketplace Scope Boundary: No Fulfillment Ownership
The platform's responsibility ends at the token transaction (listing reward and purchase
debit/credit). Shipping, delivery tracking, and physical hand-off between buyer and seller are
explicitly out of scope for the platform to own or guarantee.

Rationale: keeps scope bounded — the platform is a trust/valuation/token layer over an existing
peer-to-peer shipping reality, not a logistics company. This intentionally departs from eShop's
original Ordering delivery-tracking flow.

## Technology Constraints

- The application MUST remain a .NET 10 / .NET Aspire solution, reusing the existing service
  topology (Catalog.API, Basket.API, Ordering.API, Identity.API, PaymentProcessor,
  EventBus/EventBusRabbitMQ, WebApp) as the starting point for the golf marketplace re-theme.
- Inter-service communication crossing a service boundary MUST continue to use the existing
  integration-event/EventBus pattern (e.g., `ClubListed`, `TradeCompleted`, `TokensAwarded`),
  consistent with how Ordering/Basket/Catalog already communicate.
- Token balances and the token ledger MUST be persisted in a durable, transactional store (never
  cache-only), consistent with Principle II.

## Development Workflow & Governance

- This constitution supersedes ad hoc practice. Any plan, spec, or PR that conflicts with a Core
  Principle MUST either change to comply, or document the conflict and rationale in the plan's
  Complexity Tracking section for explicit approval.
- Amendments require team consensus via pull request review — no single-person unilateral change
  to a Core Principle. The amending PR's description MUST state the version bump
  (MAJOR/MINOR/PATCH) and the rationale for it.
- Every PR touching token, trade, or valuation code MUST self-report compliance with Principles
  II, III, and V in its description; reviewers MUST block merge if evidence of idempotency/audit
  test coverage is missing.

**Version**: 1.1.0 | **Ratified**: 2026-07-13 | **Last Amended**: 2026-07-13
