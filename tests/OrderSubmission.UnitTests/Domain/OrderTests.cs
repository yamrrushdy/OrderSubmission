using OrderSubmission.Domain.Common;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_calculates_total_from_line_items()
    {
        var result = Order.Place(
            "CUST-001",
            [new OrderLineDraft("SKU-1", 3, 10.50m), new OrderLineDraft("SKU-2", 2, 4.25m)],
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(40.00m, result.Value.Total);
        Assert.Equal(new[] { 1, 2 }, result.Value.Lines.Select(line => line.LineNumber));
    }

    [Fact]
    public void Place_normalizes_customer_reference_and_product_codes()
    {
        var order = Order.Place("  CUST-001 ", [new OrderLineDraft(" sku-1 ", 1, 1m)], Now).Value;

        Assert.Equal("CUST-001", order.CustomerReference.Value);
        Assert.Equal("SKU-1", order.Lines[0].ProductCode.Value);
    }

    [Fact]
    public void Place_requires_at_least_one_line()
    {
        var result = Order.Place("CUST-001", [], Now);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains("lines", error.Failures.Keys);
    }

    [Theory]
    [InlineData(0, 1, "lines[0].quantity")]
    [InlineData(-2, 1, "lines[0].quantity")]
    [InlineData(1, 0, "lines[0].unitPrice")]
    [InlineData(1, -5, "lines[0].unitPrice")]
    public void Place_rejects_non_positive_quantity_and_price(int quantity, decimal unitPrice, string field)
    {
        var result = Order.Place("CUST-001", [new OrderLineDraft("SKU-1", quantity, unitPrice)], Now);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(field, error.Failures.Keys);
    }

    [Fact]
    public void Place_rejects_prices_with_more_than_two_decimals()
    {
        var result = Order.Place("CUST-001", [new OrderLineDraft("SKU-1", 1, 9.999m)], Now);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains("lines[0].unitPrice", error.Failures.Keys);
    }

    [Fact]
    public void Place_reports_every_invalid_field_at_once()
    {
        var result = Order.Place(
            " ",
            [new OrderLineDraft("SKU-1", 1, 1m), new OrderLineDraft("bad code!", 0, 1m), null],
            Now);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(
            new[] { "customerReference", "lines[1].productCode", "lines[1].quantity", "lines[2]" },
            error.Failures.Keys.Order(StringComparer.Ordinal));
    }
}
