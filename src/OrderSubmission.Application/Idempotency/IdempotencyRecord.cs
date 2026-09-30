using System.Security.Cryptography;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Application.Idempotency;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
        Key = null!;
        RequestHash = [];
    }

    public IdempotencyRecord(IdempotencyKey key, byte[] requestHash, OrderId orderId, DateTimeOffset createdAt)
    {
        Key = key;
        RequestHash = requestHash;
        OrderId = orderId;
        CreatedAt = createdAt;
    }

    public IdempotencyKey Key { get; private set; }

    public byte[] RequestHash { get; private set; }

    public OrderId OrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool Matches(byte[] requestHash) => CryptographicOperations.FixedTimeEquals(RequestHash, requestHash);
}
