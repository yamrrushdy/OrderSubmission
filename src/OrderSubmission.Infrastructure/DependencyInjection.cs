using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Application.Notifications;
using OrderSubmission.Infrastructure.Notifications;
using OrderSubmission.Infrastructure.Persistence;
using OrderSubmission.Infrastructure.Persistence.Queries;
using OrderSubmission.Infrastructure.Persistence.Repositories;

namespace OrderSubmission.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Orders";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderingDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        services.AddOptions<NotificationDispatchOptions>()
            .Bind(configuration.GetSection(NotificationDispatchOptions.SectionName))
            .Validate(o => o.BatchSize is > 0 and <= 500, "BatchSize must be between 1 and 500.")
            .Validate(o => o.PollingInterval > TimeSpan.Zero, "PollingInterval must be positive.")
            .Validate(o => o.LeaseDuration > TimeSpan.Zero, "LeaseDuration must be positive.")
            .Validate(o => o.InitialRetryDelay > TimeSpan.Zero && o.MaxRetryDelay >= o.InitialRetryDelay,
                "Retry delays must be positive and MaxRetryDelay must not be less than InitialRetryDelay.")
            .ValidateOnStart();

        services.AddOptions<FakeNotificationOptions>()
            .Bind(configuration.GetSection(FakeNotificationOptions.SectionName));

        services.AddSingleton<NotificationFaultSimulator>();
        services.AddScoped<INotificationSender, FakeNotificationSender>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
