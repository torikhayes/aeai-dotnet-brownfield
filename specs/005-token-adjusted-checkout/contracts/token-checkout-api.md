# Contract: Token Checkout API and Messaging

## Scope

Contracts required for token-based checkout across Basket.API, Ordering.API, and PaymentProcessor.

## 1) Basket gRPC Contract Extension

### Proto Changes (`basket.proto`)

```protobuf
enum PaymentMethod {
  PAYMENT_METHOD_UNSPECIFIED = 0;
  PAYMENT_METHOD_CASH = 1;
  PAYMENT_METHOD_TOKENS = 2;
}

message CustomerBasketResponse {
  repeated BasketItem items = 1;
  PaymentMethod payment_method = 2;
}

message UpdateBasketRequest {
  repeated BasketItem items = 2;
  PaymentMethod payment_method = 3;
}
```

### Behavioral Contract

- If `payment_method` omitted/unspecified, server treats as `CASH`.
- Server persists method with basket payload in Redis.
- Token method may be rejected when token payment prerequisites are not met.

## 2) Ordering API Request Contract

### Endpoint

`POST /api/orders`

### Request Additions

```json
{
  "paymentMethod": "Cash | Tokens"
}
```

### Behavioral Contract

- `Cash`: existing behavior, card data required and validated.
- `Tokens`: card data ignored for gateway execution path; token spend precondition enforced.
- Missing value defaults to `Cash` for backward compatibility.

### Error Cases

- `400 insufficient_balance`: token balance below required amount.
- `400 token_price_unavailable`: one or more basket items cannot be priced in tokens.
- `503 token_service_unavailable`: spend retries exhausted/downstream unavailable.

## 3) PaymentProcessor Spend Endpoint Contract

### Endpoint

`POST /api/tokens/spend`

### Request

```json
{
  "userId": "<identity-guid>",
  "amount": 90,
  "orderId": "<order-id-or-command-id>"
}
```

### Success Response

`200 OK`

```json
{
  "newBalance": 30
}
```

### Error Responses

- `400 validation_error`
- `400 insufficient_balance`
- `409 already_processed`
- `503 service_unavailable`

### Behavioral Contract

- Authorization required.
- Endpoint intended for internal Ordering.API flow.
- `orderId` acts as idempotency key.

## 4) Compensation Event Contract

### Event

`OrderCreationFailedIntegrationEvent`

### Payload

```json
{
  "userId": "<identity-guid>",
  "orderId": "<order-id-or-command-id>",
  "amount": 90,
  "reason": "order_persistence_failed"
}
```

### Behavioral Contract

- Emitted only when token spend succeeded but order creation failed afterward.
- PaymentProcessor consumes and applies exactly one compensating credit.
- Duplicate events are treated idempotently.

## 5) Existing Event Flow Compatibility

- `PaymentMethod = Cash` remains on existing stock-confirmed -> payment succeeded/failed event flow.
- `PaymentMethod = Tokens` bypasses external gateway charge step but still emits/order-processes downstream order lifecycle events needed by Catalog/Webhooks consumers.
