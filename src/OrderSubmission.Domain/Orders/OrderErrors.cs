using OrderSubmission.Domain.Common;

namespace OrderSubmission.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error CustomerReferenceRequired =
        Error.Validation("order.customer_reference_required", "Customer reference is required.");

    public static readonly Error CustomerReferenceTooLong =
        Error.Validation("order.customer_reference_too_long", $"Customer reference cannot exceed {CustomerReference.MaxLength} characters.");

    public static readonly Error NoLines =
        Error.Validation("order.no_lines", "An order must contain at least one line item.");

    public static readonly Error TooManyLines =
        Error.Validation("order.too_many_lines", $"An order cannot contain more than {Order.MaxLines} line items.");

    public static readonly Error LineRequired =
        Error.Validation("order.line_required", "Line item cannot be empty.");

    public static readonly Error ProductCodeRequired =
        Error.Validation("order.product_code_required", "Product code is required.");

    public static readonly Error ProductCodeTooLong =
        Error.Validation("order.product_code_too_long", $"Product code cannot exceed {ProductCode.MaxLength} characters.");

    public static readonly Error ProductCodeInvalid =
        Error.Validation("order.product_code_invalid", "Product code may only contain letters, digits, '.', '-' and '_'.");

    public static readonly Error QuantityNotPositive =
        Error.Validation("order.quantity_not_positive", "Quantity must be greater than zero.");

    public static readonly Error QuantityTooLarge =
        Error.Validation("order.quantity_too_large", $"Quantity cannot exceed {OrderLine.MaxQuantity}.");

    public static readonly Error UnitPriceNotPositive =
        Error.Validation("order.unit_price_not_positive", "Unit price must be greater than zero.");

    public static readonly Error UnitPriceTooLarge =
        Error.Validation("order.unit_price_too_large", $"Unit price cannot exceed {OrderLine.MaxUnitPrice}.");

    public static readonly Error UnitPriceTooPrecise =
        Error.Validation("order.unit_price_too_precise", $"Unit price cannot have more than {OrderLine.PriceScale} decimal places.");

    public static Error NotFound(OrderId id) =>
        Error.NotFound("order.not_found", $"Order '{id}' was not found.");
}
