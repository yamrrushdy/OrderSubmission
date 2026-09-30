using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OrderSubmission.Application.Notifications;
using OrderSubmission.Infrastructure.Notifications;
using OrderSubmission.Infrastructure.Persistence;

namespace OrderSubmission.IntegrationTests.Infrastructure;

public sealed class OrderingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServerVariable = "ORDERS_TEST_SQLSERVER";
    private const string DefaultServer = "Server=.;Trusted_Connection=True;TrustServerCertificate=True";

    public OrderingApiFactory()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable) ?? DefaultServer;

        ConnectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"OrderSubmission_Tests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));

    public NotificationFaultSimulator FaultSimulator => Services.GetRequiredService<NotificationFaultSimulator>();

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    public async Task<int> DispatchNotificationsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchDueAsync(CancellationToken.None);
    }

    public async Task<T> QueryAsync<T>(Func<OrderingDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<OrderingDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Orders", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Notifications:Dispatch:Enabled", "false");
        builder.UseSetting("Notifications:Fake:Latency", "00:00:00");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<OrderingApiFactory>
{
    public const string Name = "Ordering API";
}
