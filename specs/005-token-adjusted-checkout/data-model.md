# Data Model: Token-Based Checkout

**Feature**: 005-token-adjusted-checkout  
**Date**: 2026-07-15

## Entities

### BasketPaymentMethod (new enum)

Represents checkout choice persisted in basket state.

```csharp
public enum BasketPaymentMethod
{
    Cash = 0,
    Tokens = 1
}
```

**Used by**:
- `Basket.API` `CustomerBasket` model
- `basket.proto` request/response payloads
- WebApp checkout selection UX

**Rules**:
- Default is `Cash`
- `Tokens` selection is only valid when all basket items have a defined `TokenPrice` and wallet balance is sufficient

### CustomerBasket (extended)

Current model in Redis gains explicit payment selection.

```csharp
public class CustomerBasket
{
    public string BuyerId { get; set; } = default!;
    public List<BasketItem> Items { get; set; } = [];
    public BasketPaymentMethod PaymentMethod { get; set; } = BasketPaymentMethod.Cash;
}
```

**Persistence**: JSON in Redis key `/basket/{buyerId}`

---

### CreateOrderCommand (extended)

Ordering input model gains payment-mode intent.

```csharp
public class CreateOrderCommand : IRequest<bool>
{
    // existing fields omitted
    public OrderPaymentMethod PaymentMethod { get; private set; }
}
```

**Rules**:
- `PaymentMethod = Cash` preserves existing card validation path
- `PaymentMethod = Tokens` bypasses card gateway path and requires token spend precondition

---

### OrderPaymentMethod (new enum)

Order aggregate-level payment indicator.

```csharp
public enum OrderPaymentMethod
{
    Cash = 0,
    Tokens = 1
}
```

---

### Order (extended aggregate)

Order persistence gains payment-mode and token accounting fields.

```csharp
public class Order : Entity, IAggregateRoot
{
    // existing fields omitted
    public OrderPaymentMethod PaymentMethod { get; private set; } = OrderPaymentMethod.Cash;
    public int TokensApplied { get; private set; } = 0;
}
```

**Rules**:
- `TokensApplied > 0` only when `PaymentMethod = Tokens`
- `TokensApplied = 0` for cash orders
- `TokensApplied` is computed from basket/catalog token prices at order creation

**Database changes** (`orderdb.orders`):
- Add `payment_method` (int, non-null, default 0)
- Add `tokens_applied` (int, non-null, default 0)

---

### TokenSpendRequest (existing endpoint contract, usage clarified)

Represents internal token debit request to PaymentProcessor.

```json
{
  "userId": "string",
  "amount": 90,
  "orderId": "string"
}
```

**Rules**:
- `amount` must be positive integer
- `orderId` is idempotency key for spend processing
- request accepted only from authorized internal caller flow

---

### OrderCreationFailedIntegrationEvent (new)

Compensation event emitted when order create fails after successful token debit.

```csharp
public record OrderCreationFailedIntegrationEvent(
    string UserId,
    string OrderId,
    int Amount,
    string Reason
) : IntegrationEvent;
```

**Consumer**: PaymentProcessor (TokenLedger)  
**Outcome**: credit tokens back once (idempotent by event/order key)

## Relationships

```text
CustomerBasket (Redis)
  └── PaymentMethod (Cash|Tokens)
          │
          ▼
CreateOrderCommand
  └── PaymentMethod (Cash|Tokens)
          │
          ▼
Order (orderdb)
  ├── PaymentMethod
  └── TokensApplied

Ordering.API --HTTP--> PaymentProcessor /api/tokens/spend
Ordering.API --EventBus--> OrderCreationFailedIntegrationEvent --> PaymentProcessor
```

## State/Transition Notes

### Token checkout success path

1. Basket contains `PaymentMethod = Tokens`
2. Ordering computes token amount (`TokensApplied`)
3. Ordering calls `/api/tokens/spend`
4. Spend succeeds -> order persists with `PaymentMethod = Tokens`
5. Payment gateway is skipped
6. Order advances through existing stock/payment lifecycle with token-paid semantics

### Token checkout failure path

1. Spend fails (`insufficient_balance`/validation/retries exhausted)
2. Order is not created
3. Client receives validation error and can retry with cash

### Compensation path

1. Spend succeeds
2. Order persistence fails afterward
3. Ordering emits `OrderCreationFailedIntegrationEvent`
4. PaymentProcessor credits tokens back exactly once
