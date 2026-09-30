using System.Net;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Infrastructure.Notifications;
using OrderSubmission.IntegrationTests.Infrastructure;

namespace OrderSubmission.IntegrationTests;

public sealed class NotificationDeliveryTests(OrderingApiFactory factory) : ApiTestBase(factory)
{
    private static readonly TimeSpan PastAnyBackoff = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task Worker_delivers_pending_notification()
    {
        var orderId = await PlaceOrderAsync();

        await Factory.DispatchNotificationsAsync();

        var notification = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.NotNull(notification.DeliveredAt);
    }

    [Fact]
    public async Task Failed_delivery_is_recorded_and_recovers_once_the_service_is_back()
    {
        var orderId = await PlaceOrderAsync();
        Factory.FaultSimulator.Apply(FaultSettings.None with { SimulateOutage = true });

        await Factory.DispatchNotificationsAsync();

        var failed = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Contains("outage", failed.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(failed.NextAttemptAt);

        Factory.FaultSimulator.Reset();
        Factory.Clock.Advance(PastAnyBackoff);
        await Factory.DispatchNotificationsAsync();

        var recovered = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Delivered, recovered.Status);
        Assert.Equal(2, recovered.Attempts);
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public async Task Transient_failures_are_retried_until_delivery_succeeds()
    {
        var orderId = await PlaceOrderAsync();
        Factory.FaultSimulator.Apply(FaultSettings.None with { FailFirstAttempts = 2 });

        for (var cycle = 0; cycle < 3; cycle++)
        {
            await Factory.DispatchNotificationsAsync();
            Factory.Clock.Advance(PastAnyBackoff);
        }

        var notification = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Equal(3, notification.Attempts);
    }

    [Fact]
    public async Task Failed_delivery_waits_for_its_backoff_before_retrying()
    {
        var orderId = await PlaceOrderAsync();
        Factory.FaultSimulator.Apply(FaultSettings.None with { SimulateOutage = true });
        await Factory.DispatchNotificationsAsync();

        Factory.FaultSimulator.Reset();
        await Factory.DispatchNotificationsAsync();

        var notification = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(1, notification.Attempts);
    }

    [Fact]
    public async Task Delivered_notification_is_never_sent_again()
    {
        var orderId = await PlaceOrderAsync();
        await Factory.DispatchNotificationsAsync();

        Factory.Clock.Advance(PastAnyBackoff);
        await Factory.DispatchNotificationsAsync();

        Assert.Equal(1, (await GetOrderAsync(orderId)).Notification.Attempts);
    }

    [Fact]
    public async Task Claimed_notifications_are_not_picked_up_twice_by_parallel_dispatchers()
    {
        var orderId = await PlaceOrderAsync();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Factory.DispatchNotificationsAsync()));

        var notification = (await GetOrderAsync(orderId)).Notification;
        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Equal(1, notification.Attempts);
    }

    private async Task<Guid> PlaceOrderAsync()
    {
        var response = await SubmitAsync(SampleOrder(), NewKey());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadOrderAsync(response)).Id;
    }
}
