using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Orders;

public sealed record CustomerReference
{
    public const int MaxLength = 64;

    private CustomerReference(string value) => Value = value;

    public string Value { get; }

    public static Result<CustomerReference> Create(string? value)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            return OrderErrors.CustomerReferenceRequired;
        }

        if (normalized.Length > MaxLength)
        {
            return OrderErrors.CustomerReferenceTooLong;
        }

        return new CustomerReference(normalized);
    }

    public override string ToString() => Value;
}
