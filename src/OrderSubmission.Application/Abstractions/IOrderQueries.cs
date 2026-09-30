using OrderSubmission.Application.Orders;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Abstractions;

public interface IOrderQueries
{
    Task<OrderDetails?> GetAsync(OrderId id, CancellationToken cancellationToken);
}
