using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    public const int MaxLines = 100;

    private readonly List<OrderLine> _lines = [];

    private Order()
    {
        CustomerReference = null!;
    }

    private Order(OrderId id, CustomerReference customerReference, IEnumerable<OrderLine> lines, DateTimeOffset placedAt)
        : base(id)
    {
        CustomerReference = customerReference;
        PlacedAt = placedAt;
        _lines.AddRange(lines);
        Total = _lines.Sum(line => line.LineTotal);
    }

    public CustomerReference CustomerReference { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public decimal Total { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public static Result<Order> Place(string? customerReference, IReadOnlyList<OrderLineDraft?>? lines, DateTimeOffset placedAt)
    {
        var validation = new ValidationCollector();

        var customer = validation.Collect("customerReference", CustomerReference.Create(customerReference));

        var orderLines = new List<OrderLine>();

        if (lines is null || lines.Count == 0)
        {
            validation.Add("lines", OrderErrors.NoLines);
        }
        else if (lines.Count > MaxLines)
        {
            validation.Add("lines", OrderErrors.TooManyLines);
        }
        else
        {
            for (var index = 0; index < lines.Count; index++)
            {
                if (lines[index] is not { } draft)
                {
                    validation.Add($"lines[{index}]", OrderErrors.LineRequired);
                    continue;
                }

                var line = validation.Collect($"lines[{index}]", OrderLine.Create(index + 1, draft));

                if (line is not null)
                {
                    orderLines.Add(line);
                }
            }
        }

        if (validation.HasFailures)
        {
            return validation.ToError();
        }

        return new Order(OrderId.New(), customer!, orderLines, placedAt);
    }
}
