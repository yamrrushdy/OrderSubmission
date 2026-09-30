using OrderSubmission.Domain.Common;

namespace OrderSubmission.Application.Idempotency;

public sealed record IdempotencyKey
{
    public const int MaxLength = 100;

    private IdempotencyKey(string value) => Value = value;

    public string Value { get; }

    public static Result<IdempotencyKey> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IdempotencyErrors.KeyMissing;
        }

        if (value.Length > MaxLength || value.Any(c => c is < '!' or > '~'))
        {
            return IdempotencyErrors.KeyInvalid;
        }

        return new IdempotencyKey(value);
    }

    public override string ToString() => Value;
}
