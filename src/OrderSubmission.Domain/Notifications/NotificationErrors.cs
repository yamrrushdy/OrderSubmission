using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error RetryInPast =
        Error.Validation("notification.retry_in_past", "A retry cannot be scheduled before the failed attempt.");

    public static Error AlreadyDelivered(NotificationId id) =>
        Error.Conflict("notification.already_delivered", $"Notification '{id}' has already been delivered.");
}
