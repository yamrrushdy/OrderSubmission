using OrderSubmission.Application.Idempotency;
using OrderSubmission.Application.Orders.Submit;

namespace OrderSubmission.UnitTests.Application;

public sealed class RequestFingerprintTests
{
    [Fact]
    public void Equivalent_payloads_produce_the_same_fingerprint()
    {
        var original = Command("CUST-001", new SubmitOrderLine("SKU-1", 2, 10m));
        var reformatted = Command("  CUST-001 ", new SubmitOrderLine(" sku-1", 2, 10.00m));

        Assert.Equal(RequestFingerprint.Compute(original), RequestFingerprint.Compute(reformatted));
    }

    [Theory]
    [InlineData("CUST-002", "SKU-1", 2, 10)]
    [InlineData("CUST-001", "SKU-2", 2, 10)]
    [InlineData("CUST-001", "SKU-1", 3, 10)]
    [InlineData("CUST-001", "SKU-1", 2, 10.01)]
    public void Any_business_change_produces_a_different_fingerprint(string customer, string sku, int quantity, decimal price)
    {
        var original = Command("CUST-001", new SubmitOrderLine("SKU-1", 2, 10m));
        var changed = Command(customer, new SubmitOrderLine(sku, quantity, price));

        Assert.NotEqual(RequestFingerprint.Compute(original), RequestFingerprint.Compute(changed));
    }

    [Fact]
    public void Line_order_is_part_of_the_fingerprint()
    {
        var first = Command("CUST-001", new SubmitOrderLine("A", 1, 1m), new SubmitOrderLine("B", 1, 1m));
        var second = Command("CUST-001", new SubmitOrderLine("B", 1, 1m), new SubmitOrderLine("A", 1, 1m));

        Assert.NotEqual(RequestFingerprint.Compute(first), RequestFingerprint.Compute(second));
    }

    [Fact]
    public void Idempotency_key_is_not_part_of_the_fingerprint()
    {
        var line = new SubmitOrderLine("SKU-1", 1, 1m);

        Assert.Equal(
            RequestFingerprint.Compute(new SubmitOrderCommand("key-a", "CUST-001", [line])),
            RequestFingerprint.Compute(new SubmitOrderCommand("key-b", "CUST-001", [line])));
    }

    private static SubmitOrderCommand Command(string customer, params SubmitOrderLine[] lines) =>
        new("key", customer, lines);
}
