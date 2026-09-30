using OrderSubmission.Application.Abstractions;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(OrderingDbContext context) : IOrderRepository
{
    public void Add(Order order) => context.Orders.Add(order);
}
