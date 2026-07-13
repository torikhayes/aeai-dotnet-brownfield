# Order Lifecycle &amp; Integration Events

The order state machine is the clearest example of the event-driven architecture in this repo, and the most likely place a future feature will need to hook in. `OrderStatus` (`src/Ordering.Domain/AggregatesModel/OrderAggregate/OrderStatus.cs`):

```
Submitted -> AwaitingValidation -> StockConfirmed -> Paid -> Shipped
                                 \-> (rejected at any stock/payment step) -> Cancelled
```

## The outbox pattern (why events aren't published inline)

Ordering.API never calls `eventBus.PublishAsync(...)` directly from a command handler. Instead:

1. A command handler calls `OrderingIntegrationEventService.AddAndSaveEventAsync(evt)`, which writes the event to the `IntegrationEventLogEF` table **in the same DB transaction** as the business change (`Behaviors/TransactionBehavior.cs` wraps every MediatR command in a transaction).
2. After the transaction commits, `TransactionBehavior` calls `PublishEventsThroughEventBusAsync(transactionId)`, which reads back the pending log rows for that transaction and publishes each to RabbitMQ, marking them Published/Failed as it goes.

This guarantees "order saved" and "event will eventually be published" happen atomically — no dual-write race. **If you add a new order-related command that needs to notify other services, follow this same two-step pattern** rather than publishing directly.

## Step-by-step flow

| # | Trigger | Component | Action | Event published |
|---|---|---|---|---|
| 1 | User places order | Ordering.API `CreateOrderCommandHandler` | creates `Order` (status `Submitted`) | `OrderStartedIntegrationEvent` |
| 2 | consumes #1 | **Basket.API** `OrderStartedIntegrationEventHandler` | clears the user's basket | — |
| 3 | consumes #1 | **OrderProcessor** `GracePeriodManagerService` | waits a configured grace period (lets user cancel) | `GracePeriodConfirmedIntegrationEvent` |
| 4 | consumes #3 | Ordering.API `GracePeriodConfirmedIntegrationEventHandler` | sends `SetAwaitingValidationOrderStatusCommand` → status `AwaitingValidation` | `OrderStatusChangedToAwaitingValidationIntegrationEvent` |
| 5 | consumes #4 | **Catalog.API** `OrderStatusChangedToAwaitingValidationIntegrationEventHandler` | checks stock for each line item | `OrderStockConfirmedIntegrationEvent` **or** `OrderStockRejectedIntegrationEvent` |
| 6a | consumes #5 (confirmed) | Ordering.API `OrderStockConfirmedIntegrationEventHandler` | `SetStockConfirmedOrderStatusCommand` → status `StockConfirmed` | `OrderStatusChangedToStockConfirmedIntegrationEvent` |
| 6b | consumes #5 (rejected) | Ordering.API `OrderStockRejectedIntegrationEventHandler` | `SetStockRejectedOrderStatusCommand` → status `Cancelled` | `OrderStatusChangedToCancelledIntegrationEvent` |
| 7 | consumes #6a | **PaymentProcessor** | simulates charging the card (`PaymentOptions` controls pass/fail) | `OrderPaymentSucceededIntegrationEvent` **or** `OrderPaymentFailedIntegrationEvent` |
| 8a | consumes #7 (success) | Ordering.API `OrderPaymentSucceededIntegrationEventHandler` | `SetPaidOrderStatusCommand` → status `Paid` | `OrderStatusChangedToPaidIntegrationEvent` |
| 8b | consumes #7 (failure) | Ordering.API `OrderPaymentFailedIntegrationEventHandler` | `CancelOrderCommand` → status `Cancelled` | `OrderStatusChangedToCancelledIntegrationEvent` |
| 9 | consumes #8a | **Catalog.API** `OrderStatusChangedToPaidIntegrationEventHandler` | decrements confirmed stock | — |
| 9 | consumes #8a | **Webhooks.API** `OrderStatusChangedToPaidIntegrationEventHandler` | notifies registered webhook subscribers | webhook delivery (not a bus event) |
| 10 | (manual/admin) | Ordering.API `ShipOrderCommandHandler` | `ShipOrderCommand` → status `Shipped` | `OrderStatusChangedToShippedIntegrationEvent` |
| 11 | consumes #10 | **Webhooks.API** `OrderStatusChangedToShippedIntegrationEventHandler` | notifies subscribers | webhook delivery |

Throughout, **WebApp** subscribes to every `OrderStatusChangedTo*` event (`src/WebApp/Services/OrderStatus/IntegrationEvents/EventHandling/`) purely to push live status updates to the storefront UI — it has no domain authority, it just reflects state.

## Where to hook in a new status / side effect

- **New terminal or intermediate order status**: add to `OrderStatus` enum, add an `Order.SetXStatus()` domain method + domain event in `Ordering.Domain`, add a `SetXOrderStatusCommand`/Handler + integration event in `Ordering.API/Application`, publish via `AddAndSaveEventAsync` inside the command handler (never inline).
- **New service reacting to an existing status change**: register a subscription in that service's `Program.cs` (`builder.AddRabbitMqEventBus(...).AddSubscription<TEvent, THandler>()`), add the handler under `IntegrationEvents/EventHandling/`.
- **Surface a status in the storefront UI**: mirror the pattern in `WebApp/Services/OrderStatus/IntegrationEvents/EventHandling/`.
