# Feature Specification: Marketplace UI

**Feature Branch**: `006-marketplace-ui`
**Created**: 2026-07-13
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - List a Club via the Storefront (Priority: P1)

An authenticated user navigates to "Sell My Club" in the WebApp, fills in a form with club details, and submits. They are redirected to their new listing.

**Why this priority**: The API for listing clubs exists after Phase 2, but without a UI it is inaccessible to non-technical sellers.

**Independent Test**: Log in to the WebApp, complete the "List a Club" form, and verify the new listing appears in the catalog.

**Acceptance Scenarios**:

1. **Given** a logged-in user, **When** they navigate to "Sell My Club" and submit a valid form, **Then** the new club listing appears in the public catalog and in "My Listings".
2. **Given** a user submits the form with missing required fields, **When** the form is validated, **Then** inline validation errors are shown and submission is blocked.
3. **Given** a user is not logged in, **When** they navigate to "Sell My Club", **Then** they are redirected to the login page.

---

### User Story 2 - View My Listings (Priority: P1)

A seller can navigate to "My Listings" and see all clubs they have listed, with status indicators (active, sold).

**Why this priority**: Sellers need to manage their inventory from the UI, not just the API.

**Independent Test**: Log in as a seller with active listings — "My Listings" page displays the correct items.

**Acceptance Scenarios**:

1. **Given** a seller has 3 active and 1 sold listing, **When** they open "My Listings", **Then** all 4 listings are shown with visual sold/active status.
2. **Given** a seller has no listings, **When** they open "My Listings", **Then** an empty-state message and a prompt to list their first club are shown.
3. **Given** a listing is sold, **When** it appears in "My Listings", **Then** it is visually distinct from active listings and the sold date is shown.

---

### User Story 3 - Token Wallet Widget in Header (Priority: P2)

Any logged-in user sees their current token balance displayed in the site header. Clicking it opens a transaction history panel.

**Why this priority**: Constant visibility of token balance encourages engagement with the token economy.

**Independent Test**: Log in — the header shows a token balance. Earn tokens by selling — the balance updates after a page refresh.

**Acceptance Scenarios**:

1. **Given** a logged-in user with 75 tokens, **When** any page loads, **Then** the header displays "75 tokens" (or equivalent icon + number).
2. **Given** a user clicks the token balance widget, **When** the panel opens, **Then** a list of recent token transactions is shown.
3. **Given** a logged-out user, **When** any page loads, **Then** no token balance widget is shown.

---

### User Story 4 - AI-Powered Club Discovery (Priority: P3)

A user types a natural language query (e.g., "forgiving iron for high handicappers") and receives semantically relevant club listings.

**Why this priority**: The AI semantic search infrastructure (pgvector + Ollama) is already built — this phase simply surfaces it in the UI with golf-appropriate copy.

**Independent Test**: Enable Ollama locally, enter a semantic query in the search bar, and verify results are more relevant than a keyword search would return.

**Acceptance Scenarios**:

1. **Given** Ollama is enabled and embeddings have been generated, **When** a user searches "beginner-friendly driver", **Then** results include clubs that are semantically relevant even if the words don't match exactly.
2. **Given** Ollama is disabled, **When** a user searches, **Then** the UI falls back to standard keyword/filter search without error.

---

### Edge Cases

- What does the token widget show if Token.API is temporarily unavailable?
- How does the listing form handle very long club descriptions?
- What happens on the "My Listings" page if the user has just listed a club and the catalog hasn't refreshed?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A "Sell My Club" page MUST be added to WebApp with a form covering: club name, type (dropdown), brand (dropdown), condition (dropdown), manufacture year, price, description, and tags.
- **FR-002**: The "Sell My Club" page MUST require authentication; unauthenticated users are redirected to login.
- **FR-003**: A "My Listings" page MUST be added to WebApp, showing the authenticated seller's listings fetched from `GET /api/catalog/items/my-listings`.
- **FR-004**: Each listing in "My Listings" MUST display its `TokenPotentialScore`, view count, favorite count, and average rating.
- **FR-005**: A token balance widget MUST be added to the site header for authenticated users, displaying the current balance fetched from `GET /api/tokens/balance`.
- **FR-006**: Clicking the token balance widget MUST open a slide-out or modal panel displaying recent token transactions from `GET /api/tokens/transactions`.
- **FR-007**: The basket/checkout flow MUST include a token redemption input allowing the buyer to specify how many tokens to apply, with a live-updated net price preview.
- **FR-008**: The semantic search input (already present) MUST display golf-appropriate placeholder text (e.g., "Search clubs by feel, flex, or style...").
- **FR-009**: All new UI components MUST be implemented as Razor components in `WebAppComponents` where reusable across WebApp and HybridApp.
- **FR-010**: If Token.API is unavailable, the token widget MUST degrade gracefully (show "—" or hide) without breaking page load.

### Key Entities

- **No new backend entities** — all data comes from existing API endpoints defined in Phases 2–5.
- **New Razor components**: `ListClubForm`, `MyListingsPage`, `TokenWalletWidget`, `TokenTransactionPanel`, `TokenRedemptionInput`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A seller can complete the "List a Club" form and submit in under 90 seconds from a cold start.
- **SC-002**: "My Listings" page loads in under 2 seconds under normal conditions.
- **SC-003**: Token balance in the header is current as of the last page load (stale-while-revalidate acceptable).
- **SC-004**: The checkout token redemption input correctly prevents applying more tokens than the club price or the user's balance (client-side validation).
- **SC-005**: All new pages pass Playwright e2e tests covering the happy path for listing and purchasing a club.

## Assumptions

- This phase depends on Phases 2 (seller listings), 3 (scoring), 4 (token wallet), and 5 (token checkout) being complete.
- AI semantic search requires Ollama to be running locally; the UI gracefully degrades to keyword search if unavailable.
- HybridApp (MAUI Hybrid) gets the new Razor components for free via `WebAppComponents`; native ClientApp (MAUI) UI updates are out of scope for this phase.
- Real-time token balance updates (e.g., via SignalR) are out of scope; the balance refreshes on page navigation.
- Image upload for club listings is a placeholder upload flow; CDN/storage integration is out of scope for this phase.
