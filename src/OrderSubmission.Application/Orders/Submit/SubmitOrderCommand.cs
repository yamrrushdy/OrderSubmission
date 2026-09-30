namespace OrderSubmission.Application.Orders.Submit;

public sealed record SubmitOrderCommand(
    string? IdempotencyKey,
    string? CustomerReference,
    IReadOnlyList<SubmitOrderLine?>? Lines);

public sealed record SubmitOrderLine(string? ProductCode, int Quantity, decimal UnitPrice);

public sealed record SubmitOrderResult(OrderDetails Order, bool IsReplay);
