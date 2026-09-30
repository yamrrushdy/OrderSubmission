using Microsoft.EntityFrameworkCore;
using OrderSubmission.Application.Idempotency;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderNotification> Notifications => Set<OrderNotification>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(3);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
