# Data Model: PaymentProcessor Token Ledger Extension

**Feature**: 004-paymentprocessor-token-ledger  
**Database**: `tokendb` (Postgres, hosted on existing `postgres` Aspire container)  
**EF Core Context**: `TokenDbContext` in `PaymentProcessor/TokenLedger/Infrastructure/`

---

## Entities

### TokenWallet

Stores the current token balance for a user. Created only on first token award — not on first balance read.

```csharp
public class TokenWallet
{
    public string UserId { get; set; } = default!;   // PK — Identity sub claim
    public int Balance { get; set; }
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];     // Optimistic concurrency token
}
```

**EF Core configuration**:
```csharp
entity.HasKey(w => w.UserId);
entity.Property(w => w.RowVersion).IsRowVersion();
entity.Property(w => w.Balance).HasDefaultValue(0);
```

**Rules**:
- Balance MUST NOT go below zero (enforced in `TokenLedgerService`, not DB constraint)
- Created atomically during first `CreditTokens()` call
- `GET /balance` returns `0` without reading or writing a row if no wallet exists

---

### TokenTransaction

Append-only ledger of all token earn and spend events. Never mutated after insert.

```csharp
public class TokenTransaction
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public int Amount { get; set; }                   // positive = earn, negative = spend
    public string Reason { get; set; } = default!;    // e.g. "Driver/Excellent listing verified", "purchase debit"
    public string RelatedEventId { get; set; } = default!; // EventId from integration event — idempotency key
    public string? LookupTableVersion { get; set; }   // populated on earn transactions only
    public string? CatalogItemId { get; set; }        // populated on earn transactions only (from ClubListingVerifiedIntegrationEvent)
    public DateTime CreatedAt { get; set; }
}
```

**EF Core configuration**:
```csharp
entity.HasKey(t => t.Id);
entity.HasIndex(t => new { t.UserId, t.CreatedAt });  // query by user, ordered by date
entity.HasIndex(t => t.RelatedEventId).IsUnique();    // idempotency: one transaction per EventId
entity.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
```

---

### TokenAwardLookupEntry

Immutable versioned table of token award amounts keyed on club category × condition grade. New versions insert new rows; old rows are never updated.

```csharp
public class TokenAwardLookupEntry
{
    public Guid Id { get; set; }
    public string ClubCategory { get; set; } = default!;    // e.g. "Driver", "Iron Set"
    public string ConditionGrade { get; set; } = default!;  // "New" | "Excellent" | "Good" | "Fair"
    public int TokenAmount { get; set; }
    public string TableVersion { get; set; } = default!;    // e.g. "1.0.0"
    public DateTime EffectiveFrom { get; set; }
}
```

**EF Core configuration**:
```csharp
entity.HasKey(e => e.Id);
entity.HasIndex(e => new { e.ClubCategory, e.ConditionGrade, e.TableVersion }).IsUnique();
```

**Active entry query**: The active entry for a (category, condition) pair is the row with the greatest `EffectiveFrom` that is ≤ `DateTime.UtcNow`:
```csharp
await db.TokenAwardLookupEntries
    .Where(e => e.ClubCategory == category
             && e.ConditionGrade == condition
             && e.EffectiveFrom <= DateTime.UtcNow)
    .OrderByDescending(e => e.EffectiveFrom)
    .FirstOrDefaultAsync();
```

**Seed data (v1.0.0)**:

| ClubCategory | ConditionGrade | TokenAmount |
|---|---|---|
| Driver | New | 100 |
| Driver | Excellent | 80 |
| Driver | Good | 60 |
| Driver | Fair | 40 |
| Fairway Wood | New | 80 |
| Fairway Wood | Excellent | 65 |
| Fairway Wood | Good | 50 |
| Fairway Wood | Fair | 30 |
| Hybrid | New | 70 |
| Hybrid | Excellent | 55 |
| Hybrid | Good | 40 |
| Hybrid | Fair | 25 |
| Iron Set | New | 120 |
| Iron Set | Excellent | 95 |
| Iron Set | Good | 70 |
| Iron Set | Fair | 45 |
| Wedge | New | 60 |
| Wedge | Excellent | 48 |
| Wedge | Good | 35 |
| Wedge | Fair | 20 |
| Putter | New | 90 |
| Putter | Excellent | 72 |
| Putter | Good | 54 |
| Putter | Fair | 35 |
| Other | New | 50 |
| Other | Excellent | 40 |
| Other | Good | 30 |
| Other | Fair | 15 |

---

### TokenAwardedListing

Deduplication table enforcing the one-award-per-listing rule (FR-008). A row is inserted atomically with the `TokenTransaction` when a listing is awarded. Subsequent `ClubListingVerifiedIntegrationEvent` for the same `CatalogItemId` fail the unique constraint check and are rejected without crediting.

```csharp
public class TokenAwardedListing
{
    public string CatalogItemId { get; set; } = default!;  // PK
    public Guid TransactionId { get; set; }                // FK to TokenTransaction.Id
    public DateTime AwardedAt { get; set; }
}
```

**EF Core configuration**:
```csharp
entity.HasKey(l => l.CatalogItemId);
entity.Property(l => l.AwardedAt).HasDefaultValueSql("now()");
```

---

## Relationships

```
TokenWallet (1) ──── (N) TokenTransaction
                          via UserId (string match, no EF navigation — avoids joining across large transaction log)

TokenAwardedListing (1) ──── (1) TokenTransaction
                              via TransactionId
```

---

## Integration Event (cross-service)

`ClubListingVerifiedIntegrationEvent` is published by `Catalog.API` and consumed by `PaymentProcessor`. It lives in the shared `EventBus` project or is duplicated per-service following the existing pattern (each service defines its own event class matching the payload shape).

```csharp
// PaymentProcessor/IntegrationEvents/ClubListingVerifiedIntegrationEvent.cs
public record ClubListingVerifiedIntegrationEvent(
    Guid Id,
    DateTime CreationDate,
    string SellerId,
    string CatalogItemId,
    string Category,
    string Condition
) : IntegrationEvent(Id, CreationDate);
```

---

## DbContext Registration

In `Program.cs` (following `AddMigration<TContext, TSeeder>` from `eShop.Shared`):
```csharp
builder.AddNpgsqlDbContext<TokenDbContext>("tokendb");
builder.Services.AddMigration<TokenDbContext, TokenDbSeeder>();
```
