using OrderSubmission.Domain.Common;

namespace OrderSubmission.Application.Abstractions;

public interface INotificationSender
{
    Task<Result> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public sealed record NotificationMessage(Guid NotificationId, Guid OrderId, int Attempt);
