using OrderSubmission.Application.Orders.Submit;

namespace OrderSubmission.Api.Contracts;

public sealed record SubmitOrderRequest(string? CustomerReference, IReadOnlyList<SubmitOrderLineRequest?>? Lines)
{
    public SubmitOrderCommand ToCommand(string? idempotencyKey) => new(
        idempotencyKey,
        CustomerReference,
        Lines?
            .Select(line => line is null ? null : new SubmitOrderLine(line.ProductCode, line.Quantity, line.UnitPrice))
            .ToList());
}

public sealed record SubmitOrderLineRequest(string? ProductCode, int Quantity, decimal UnitPrice);
