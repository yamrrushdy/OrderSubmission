using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Orders;

public sealed record OrderDetails(
    Guid Id,
    string CustomerReference,
    decimal Total,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderLineDetails> Lines,
    NotificationDetails Notification)
{
    public static OrderDetails From(Order order, OrderNotification notification) => new(
        order.Id.Value,
        order.CustomerReference.Value,
        order.Total,
        order.PlacedAt,
        order.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new OrderLineDetails(
                line.LineNumber,
                line.ProductCode.Value,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToList(),
        NotificationDetails.From(notification));
}

public sealed record OrderLineDetails(
    int LineNumber,
    string ProductCode,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record NotificationDetails(
    NotificationStatus Status,
    int Attempts,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? DeliveredAt,
    string? LastError)
{
    public static NotificationDetails From(OrderNotification notification) => new(
        notification.Status,
        notification.Attempts,
        notification.LastAttemptAt,
        notification.NextAttemptAt,
        notification.DeliveredAt,
        notification.LastError);
}
