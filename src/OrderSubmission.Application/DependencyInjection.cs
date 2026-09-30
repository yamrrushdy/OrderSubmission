using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderSubmission.Application.Notifications;
using OrderSubmission.Application.Orders.Get;
using OrderSubmission.Application.Orders.Submit;

namespace OrderSubmission.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SubmitOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<NotificationDispatcher>();

        return services;
    }
}
