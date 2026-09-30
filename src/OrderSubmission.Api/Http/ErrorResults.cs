using OrderSubmission.Domain.Common;

namespace OrderSubmission.Api.Http;

public static class ErrorResults
{
    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error is ValidationError validation)
        {
            return Results.ValidationProblem(
                validation.Failures.ToDictionary(pair => pair.Key, pair => pair.Value),
                title: validation.Description,
                extensions: extensions);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        return Results.Problem(
            statusCode: status,
            title: title,
            detail: error.Description,
            extensions: extensions);
    }
}
