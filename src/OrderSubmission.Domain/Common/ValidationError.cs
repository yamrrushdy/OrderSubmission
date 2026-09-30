namespace OrderSubmission.Domain.Common;

public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Failures)
    : Error("validation_failed", "One or more validation errors occurred.", ErrorType.Validation)
{
    public static ValidationError For(string field, Error error) =>
        new(new Dictionary<string, string[]> { [field] = [error.Description] });
}
