using Microsoft.EntityFrameworkCore;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Domain.Notifications;

namespace OrderSubmission.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(OrderingDbContext context) : INotificationRepository
{
    public void Add(OrderNotification notification) => context.Notifications.Add(notification);

    public async Task<IReadOnlyList<OrderNotification>> ClaimDueAsync(
        int batchSize,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        return await context.Notifications
            .FromSql($"""
                WITH due AS (
                    SELECT TOP ({batchSize}) *
                    FROM [ordering].[OrderNotifications] WITH (ROWLOCK, UPDLOCK, READPAST)
                    WHERE [Status] IN ('Pending', 'Failed') AND [NextAttemptAt] <= {now}
                    ORDER BY [NextAttemptAt]
                )
                UPDATE due
                SET [NextAttemptAt] = {leaseUntil}
                OUTPUT inserted.*
                """)
            .ToListAsync(cancellationToken);
    }
}
