using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Orders;

public sealed class OrderLine
{
    public const int MaxQuantity = 10_000;
    public const decimal MaxUnitPrice = 1_000_000m;
    public const int PriceScale = 2;

    private OrderLine()
    {
        ProductCode = null!;
    }

    private OrderLine(int lineNumber, ProductCode productCode, int quantity, decimal unitPrice)
    {
        LineNumber = lineNumber;
        ProductCode = productCode;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public int LineNumber { get; private set; }

    public ProductCode ProductCode { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    internal static Result<OrderLine> Create(int lineNumber, OrderLineDraft draft)
    {
        var validation = new ValidationCollector();

        var productCode = validation.Collect("productCode", ProductCode.Create(draft.ProductCode));

        if (draft.Quantity <= 0)
        {
            validation.Add("quantity", OrderErrors.QuantityNotPositive);
        }
        else if (draft.Quantity > MaxQuantity)
        {
            validation.Add("quantity", OrderErrors.QuantityTooLarge);
        }

        if (draft.UnitPrice <= 0)
        {
            validation.Add("unitPrice", OrderErrors.UnitPriceNotPositive);
        }
        else if (draft.UnitPrice > MaxUnitPrice)
        {
            validation.Add("unitPrice", OrderErrors.UnitPriceTooLarge);
        }
        else if (decimal.Round(draft.UnitPrice, PriceScale) != draft.UnitPrice)
        {
            validation.Add("unitPrice", OrderErrors.UnitPriceTooPrecise);
        }

        if (validation.HasFailures)
        {
            return validation.ToError();
        }

        return new OrderLine(lineNumber, productCode!, draft.Quantity, decimal.Round(draft.UnitPrice, PriceScale));
    }
}

public sealed record OrderLineDraft(string? ProductCode, int Quantity, decimal UnitPrice);
