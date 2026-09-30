using OrderSubmission.Application.Abstractions;
using OrderSubmission.Domain.Common;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Orders.Get;

public sealed class GetOrderHandler(IOrderQueries queries)
{
    public async Task<Result<OrderDetails>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(id);
        var order = await queries.GetAsync(orderId, cancellationToken);

        return order is null ? OrderErrors.NotFound(orderId) : order;
    }
}
