using Microsoft.AspNetCore.Mvc;
using OrderSubmission.Api.Contracts;
using OrderSubmission.Api.Http;
using OrderSubmission.Application.Orders;
using OrderSubmission.Application.Orders.Get;
using OrderSubmission.Application.Orders.Submit;

namespace OrderSubmission.Api.Endpoints;

public static class OrderEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    private const string GetOrderRoute = "GetOrder";

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("/", SubmitAsync)
            .Produces<OrderDetails>(StatusCodes.Status201Created)
            .Produces<OrderDetails>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapGet("/{id:guid}", GetAsync)
            .WithName(GetOrderRoute)
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SubmitAsync(
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        SubmitOrderRequest request,
        SubmitOrderHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(idempotencyKey), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var (order, isReplay) = result.Value;

        if (isReplay)
        {
            response.Headers[ReplayedHeader] = "true";
            return Results.Ok(order);
        }

        return Results.CreatedAtRoute(GetOrderRoute, new { id = order.Id }, order);
    }

    private static async Task<IResult> GetAsync(Guid id, GetOrderHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.Match<IResult>(order => TypedResults.Ok(order), error => error.ToProblem());
    }
}
