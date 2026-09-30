using OrderSubmission.Domain.Common;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Domain.Notifications;

public sealed class OrderNotification : AggregateRoot<NotificationId>
{
    public const int MaxErrorLength = 512;

    private OrderNotification()
    {
    }

    private OrderNotification(NotificationId id, OrderId orderId, DateTimeOffset createdAt)
        : base(id)
    {
        OrderId = orderId;
        Status = NotificationStatus.Pending;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public OrderId OrderId { get; private set; }

    public NotificationStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public string? LastError { get; private set; }

    public bool IsDelivered => Status == NotificationStatus.Delivered;

    public static OrderNotification ForPlacedOrder(Order order, DateTimeOffset createdAt) =>
        new(NotificationId.New(), order.Id, createdAt);

    public Result MarkDelivered(DateTimeOffset deliveredAt)
    {
        if (IsDelivered)
        {
            return NotificationErrors.AlreadyDelivered(Id);
        }

        Attempts++;
        Status = NotificationStatus.Delivered;
        LastAttemptAt = deliveredAt;
        DeliveredAt = deliveredAt;
        NextAttemptAt = null;
        LastError = null;

        return Result.Success();
    }

    public Result MarkFailed(string reason, DateTimeOffset attemptedAt, DateTimeOffset retryAt)
    {
        if (IsDelivered)
        {
            return NotificationErrors.AlreadyDelivered(Id);
        }

        if (retryAt < attemptedAt)
        {
            return NotificationErrors.RetryInPast;
        }

        Attempts++;
        Status = NotificationStatus.Failed;
        LastAttemptAt = attemptedAt;
        NextAttemptAt = retryAt;
        LastError = Truncate(string.IsNullOrWhiteSpace(reason) ? "Delivery failed." : reason.Trim());

        return Result.Success();
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
}
