using Microsoft.Extensions.Logging;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Application.Idempotency;
using OrderSubmission.Domain.Common;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Orders.Submit;

public sealed partial class SubmitOrderHandler(
    IOrderRepository orders,
    INotificationRepository notifications,
    IIdempotencyRepository idempotency,
    IOrderQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<SubmitOrderHandler> logger)
{
    public async Task<Result<SubmitOrderResult>> HandleAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var keyResult = IdempotencyKey.Create(command.IdempotencyKey);
        if (keyResult.IsFailure)
        {
            return keyResult.Error;
        }

        var key = keyResult.Value;
        var fingerprint = RequestFingerprint.Compute(command);

        var existing = await idempotency.FindAsync(key, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, fingerprint, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var placed = Order.Place(command.CustomerReference, ToDrafts(command.Lines), now);
        if (placed.IsFailure)
        {
            return placed.Error;
        }

        var order = placed.Value;
        var notification = OrderNotification.ForPlacedOrder(order, now);

        orders.Add(order);
        notifications.Add(notification);
        idempotency.Add(new IdempotencyRecord(key, fingerprint, order.Id, now));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            LogConcurrentSubmission(key.Value);

            var winner = await idempotency.FindAsync(key, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return await ReplayAsync(winner, fingerprint, cancellationToken);
        }

        LogOrderPlaced(order.Id.Value, order.Total, key.Value);

        return new SubmitOrderResult(OrderDetails.From(order, notification), IsReplay: false);
    }

    private async Task<Result<SubmitOrderResult>> ReplayAsync(
        IdempotencyRecord record,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        if (!record.Matches(fingerprint))
        {
            return IdempotencyErrors.KeyReused;
        }

        var order = await queries.GetAsync(record.OrderId, cancellationToken);

        return order is null
            ? OrderErrors.NotFound(record.OrderId)
            : new SubmitOrderResult(order, IsReplay: true);
    }

    private static List<OrderLineDraft?>? ToDrafts(IReadOnlyList<SubmitOrderLine?>? lines) =>
        lines?
            .Select(line => line is null ? null : new OrderLineDraft(line.ProductCode, line.Quantity, line.UnitPrice))
            .ToList();

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} placed with total {Total} (key {IdempotencyKey})")]
    private partial void LogOrderPlaced(Guid orderId, decimal total, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Concurrent submission detected for key {IdempotencyKey}; returning the committed order")]
    private partial void LogConcurrentSubmission(string idempotencyKey);
}
