using OrderSubmission.Application.Idempotency;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);
}

public interface INotificationRepository
{
    void Add(OrderNotification notification);

    Task<IReadOnlyList<OrderNotification>> ClaimDueAsync(
        int batchSize,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken);
}

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> FindAsync(IdempotencyKey key, CancellationToken cancellationToken);

    void Add(IdempotencyRecord record);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class DuplicateKeyException(string message, Exception innerException)
    : Exception(message, innerException);
