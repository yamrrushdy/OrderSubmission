using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence.Configurations;

internal sealed class OrderNotificationConfiguration : IEntityTypeConfiguration<OrderNotification>
{
    public const string TableName = "OrderNotifications";

    public void Configure(EntityTypeBuilder<OrderNotification> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_OrderNotifications_Attempts", "[Attempts] >= 0");
            table.HasCheckConstraint("CK_OrderNotifications_Status", "[Status] IN ('Pending', 'Failed', 'Delivered')");
            table.HasCheckConstraint(
                "CK_OrderNotifications_Delivery",
                "([Status] = 'Delivered' AND [DeliveredAt] IS NOT NULL) OR ([Status] <> 'Delivered' AND [DeliveredAt] IS NULL AND [NextAttemptAt] IS NOT NULL)");
        });

        builder.HasKey(notification => notification.Id).IsClustered(false);

        builder.Property(notification => notification.Id)
            .HasConversion(id => id.Value, value => new NotificationId(value))
            .ValueGeneratedNever();

        builder.Property(notification => notification.OrderId)
            .HasConversion(id => id.Value, value => new OrderId(value));

        builder.Property(notification => notification.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false);

        builder.Property(notification => notification.LastError).HasMaxLength(OrderNotification.MaxErrorLength);

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<OrderNotification>(notification => notification.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(notification => notification.CreatedAt).IsClustered();

        builder.HasIndex(notification => notification.NextAttemptAt)
            .HasDatabaseName("IX_OrderNotifications_Due")
            .HasFilter("[Status] IN ('Pending', 'Failed')");

        builder.Ignore(notification => notification.IsDelivered);
    }
}
