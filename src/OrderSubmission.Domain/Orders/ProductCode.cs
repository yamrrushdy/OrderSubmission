using System.Text.RegularExpressions;
using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Orders;

public sealed partial record ProductCode
{
    public const int MaxLength = 32;

    private ProductCode(string value) => Value = value;

    public string Value { get; }

    public static Result<ProductCode> Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            return OrderErrors.ProductCodeRequired;
        }

        if (normalized.Length > MaxLength)
        {
            return OrderErrors.ProductCodeTooLong;
        }

        if (!AllowedFormat().IsMatch(normalized))
        {
            return OrderErrors.ProductCodeInvalid;
        }

        return new ProductCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedFormat();
}
