using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OrderSubmission.Application.Orders.Submit;

namespace OrderSubmission.Application.Idempotency;

public static class RequestFingerprint
{
    public const int Length = 32;

    public static byte[] Compute(SubmitOrderCommand command)
    {
        var canonical = new CanonicalOrder(
            command.CustomerReference?.Trim() ?? string.Empty,
            (command.Lines ?? [])
                .Select(line => line is null
                    ? null
                    : new CanonicalLine(
                        line.ProductCode?.Trim().ToUpperInvariant() ?? string.Empty,
                        line.Quantity,
                        line.UnitPrice.ToString("0.############################", CultureInfo.InvariantCulture)))
                .ToArray());

        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical));
    }

    private sealed record CanonicalOrder(string CustomerReference, CanonicalLine?[] Lines);

    private sealed record CanonicalLine(string ProductCode, int Quantity, string UnitPrice);
}
