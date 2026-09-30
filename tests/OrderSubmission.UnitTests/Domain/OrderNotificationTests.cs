using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.UnitTests.Domain;

public sealed class OrderNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_notification_is_pending_and_due_immediately()
    {
        var notification = CreateNotification();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(0, notification.Attempts);
        Assert.Equal(Now, notification.NextAttemptAt);
    }

    [Fact]
    public void Failed_delivery_stays_retryable_with_the_reason_recorded()
    {
        var notification = CreateNotification();

        var result = notification.MarkFailed("provider down", Now.AddSeconds(1), Now.AddSeconds(6));

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.Equal("provider down", notification.LastError);
        Assert.Equal(Now.AddSeconds(6), notification.NextAttemptAt);
    }

    [Fact]
    public void Delivery_after_failure_clears_the_error_and_schedule()
    {
        var notification = CreateNotification();
        notification.MarkFailed("provider down", Now, Now.AddSeconds(5));

        notification.MarkDelivered(Now.AddSeconds(10));

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Equal(2, notification.Attempts);
        Assert.Null(notification.LastError);
        Assert.Null(notification.NextAttemptAt);
        Assert.Equal(Now.AddSeconds(10), notification.DeliveredAt);
    }

    [Fact]
    public void Delivered_notification_cannot_change_again()
    {
        var notification = CreateNotification();
        notification.MarkDelivered(Now);

        Assert.True(notification.MarkDelivered(Now.AddSeconds(1)).IsFailure);
        Assert.True(notification.MarkFailed("late failure", Now.AddSeconds(1), Now.AddSeconds(2)).IsFailure);
        Assert.Equal(1, notification.Attempts);
    }

    [Fact]
    public void Retry_cannot_be_scheduled_before_the_attempt()
    {
        var notification = CreateNotification();

        var result = notification.MarkFailed("provider down", Now, Now.AddSeconds(-1));

        Assert.Equal(NotificationErrors.RetryInPast, result.Error);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    private static OrderNotification CreateNotification()
    {
        var order = Order.Place("CUST-001", [new OrderLineDraft("SKU-1", 1, 5m)], Now).Value;
        return OrderNotification.ForPlacedOrder(order, Now);
    }
}
