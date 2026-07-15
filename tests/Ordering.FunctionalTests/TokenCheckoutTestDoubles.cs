using eShop.Ordering.API.Application.IntegrationEvents;
using eShop.Ordering.API.Application.Services;
using eShop.EventBus.Events;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.Domain.Seedwork;
using System.Threading;

namespace eShop.Ordering.FunctionalTests;

public enum FakeTokenSpendMode
{
    Success,
    InsufficientBalance,
    ServiceUnavailable,
}

public sealed class FakeTokenSpendClient : ITokenSpendClient
{
    public FakeTokenSpendMode Mode { get; set; } = FakeTokenSpendMode.Success;
    public int SpendCallCount { get; private set; }

    public void Reset(FakeTokenSpendMode mode = FakeTokenSpendMode.Success)
    {
        Mode = mode;
        SpendCallCount = 0;
    }

    public Task<(TokenSpendResult Result, int NewBalance)> SpendAsync(string userId, int amount, string orderId, CancellationToken cancellationToken = default)
    {
        SpendCallCount++;
        return Mode switch
        {
            FakeTokenSpendMode.Success => Task.FromResult((TokenSpendResult.Success, 100 - amount)),
            FakeTokenSpendMode.InsufficientBalance => Task.FromResult((TokenSpendResult.InsufficientBalance, 0)),
            FakeTokenSpendMode.ServiceUnavailable => Task.FromResult((TokenSpendResult.ServiceUnavailable, 0)),
            _ => Task.FromResult((TokenSpendResult.UnknownFailure, 0)),
        };
    }
}

public sealed class CapturingOrderingIntegrationEventService : IOrderingIntegrationEventService
{
    public List<IntegrationEvent> Events { get; } = [];

    public Task PublishEventsThroughEventBusAsync(Guid transactionId)
    {
        return Task.CompletedTask;
    }

    public Task AddAndSaveEventAsync(IntegrationEvent evt)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}

    public sealed class NoOpOrderingIntegrationEventService : IOrderingIntegrationEventService
    {
        public Task PublishEventsThroughEventBusAsync(Guid transactionId)
        {
            return Task.CompletedTask;
        }

        public Task AddAndSaveEventAsync(IntegrationEvent evt)
        {
            return Task.CompletedTask;
        }
    }

public sealed class FailingOrderRepository : IOrderRepository
{
    public IUnitOfWork UnitOfWork { get; } = new FailingUnitOfWork();

    public Order Add(Order order) => order;

    public Task<Order> GetAsync(int orderId) => Task.FromResult<Order>(null!);

    public void Update(Order order)
    {
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public void Dispose()
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
