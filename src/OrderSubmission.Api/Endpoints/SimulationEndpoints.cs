using OrderSubmission.Api.Http;
using OrderSubmission.Infrastructure.Notifications;

namespace OrderSubmission.Api.Endpoints;

public static class SimulationEndpoints
{
    public static IEndpointRouteBuilder MapSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var simulation = app.MapGroup("/api/simulation/notifications").WithTags("Simulation");

        simulation.MapGet("/", (NotificationFaultSimulator simulator) => TypedResults.Ok(simulator.Current));

        simulation.MapPut("/", (FaultSettings settings, NotificationFaultSimulator simulator) =>
        {
            var result = simulator.Apply(settings);
            return result.IsSuccess ? Results.Ok(simulator.Current) : result.Error.ToProblem();
        });

        return app;
    }
}
