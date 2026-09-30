using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Domain.Common;

namespace OrderSubmission.Infrastructure.Notifications;

internal sealed partial class FakeNotificationSender(
    NotificationFaultSimulator simulator,
    IOptions<FakeNotificationOptions> options,
    ILogger<FakeNotificationSender> logger) : INotificationSender
{
    public async Task<Result> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (options.Value.Latency > TimeSpan.Zero)
        {
            await Task.Delay(options.Value.Latency, cancellationToken);
        }

        var failure = simulator.FailureReasonFor(message.Attempt);
        if (failure is not null)
        {
            return Error.Failure("notification.delivery_failed", failure);
        }

        LogSent(message.OrderId, message.NotificationId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order confirmation for {OrderId} sent (notification {NotificationId})")]
    private partial void LogSent(Guid orderId, Guid notificationId);
}
