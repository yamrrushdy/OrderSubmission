using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Domain.Common;
using OrderSubmission.Domain.Notifications;

namespace OrderSubmission.Application.Notifications;

public sealed partial class NotificationDispatcher(
    INotificationRepository notifications,
    INotificationSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<NotificationDispatchOptions> options,
    ILogger<NotificationDispatcher> logger)
{
    private readonly NotificationDispatchOptions _options = options.Value;

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var batch = await notifications.ClaimDueAsync(
            _options.BatchSize,
            now,
            now + _options.LeaseDuration,
            cancellationToken);

        foreach (var notification in batch)
        {
            await DeliverAsync(notification, cancellationToken);
        }

        return batch.Count;
    }

    private async Task DeliverAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        var attempt = notification.Attempts + 1;
        var outcome = await SendAsync(notification, attempt, cancellationToken);
        var completedAt = clock.GetUtcNow();

        if (outcome.IsSuccess)
        {
            notification.MarkDelivered(completedAt);
            LogDelivered(notification.Id.Value, notification.OrderId.Value, attempt);
        }
        else
        {
            var retryAt = completedAt + RetryBackoff.For(attempt, _options.InitialRetryDelay, _options.MaxRetryDelay);
            notification.MarkFailed(outcome.Error.Description, completedAt, retryAt);
            LogDeliveryFailed(notification.Id.Value, attempt, outcome.Error.Description, retryAt);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result> SendAsync(OrderNotification notification, int attempt, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.SendAsync(
                new NotificationMessage(notification.Id.Value, notification.OrderId.Value, attempt),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error.Failure("notification.sender_exception", exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification {NotificationId} for order {OrderId} delivered on attempt {Attempt}")]
    private partial void LogDelivered(Guid notificationId, Guid orderId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification {NotificationId} failed on attempt {Attempt}: {Reason}. Next attempt at {RetryAt}")]
    private partial void LogDeliveryFailed(Guid notificationId, int attempt, string reason, DateTimeOffset retryAt);
}
