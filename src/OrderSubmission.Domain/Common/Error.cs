namespace OrderSubmission.Domain.Common;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict
}

public record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Failure(string code, string description) => new(code, description);
}
