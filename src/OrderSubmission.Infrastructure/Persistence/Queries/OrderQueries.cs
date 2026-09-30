using Microsoft.EntityFrameworkCore;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Application.Orders;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence.Queries;

internal sealed class OrderQueries(OrderingDbContext context) : IOrderQueries
{
    public async Task<OrderDetails?> GetAsync(OrderId id, CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var notification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(n => n.OrderId == id, cancellationToken);

        return OrderDetails.From(order, notification);
    }
}
