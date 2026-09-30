using OrderSubmission.Domain.Common;

namespace OrderSubmission.Application.Idempotency;

public static class IdempotencyErrors
{
    public static readonly Error KeyMissing = Error.Validation(
        "idempotency.key_missing",
        "The Idempotency-Key header is required.");

    public static readonly Error KeyInvalid = Error.Validation(
        "idempotency.key_invalid",
        $"The Idempotency-Key header must be 1-{IdempotencyKey.MaxLength} printable ASCII characters without spaces.");

    public static readonly Error KeyReused = Error.Conflict(
        "idempotency.key_reused",
        "This Idempotency-Key was already used with a different request payload.");
}
