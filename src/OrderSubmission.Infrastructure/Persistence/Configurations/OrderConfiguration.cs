using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table => table.HasCheckConstraint("CK_Orders_Total_Positive", "[Total] > 0"));

        builder.HasKey(order => order.Id).IsClustered(false);

        builder.Property(order => order.Id)
            .HasConversion(id => id.Value, value => new OrderId(value))
            .ValueGeneratedNever();

        builder.Property(order => order.CustomerReference)
            .HasConversion(reference => reference.Value, value => CustomerReference.Create(value).Value)
            .HasMaxLength(CustomerReference.MaxLength)
            .IsRequired();

        builder.Property(order => order.Total).HasPrecision(19, 2);

        builder.Property(order => order.PlacedAt).IsRequired();

        builder.HasIndex(order => order.PlacedAt).IsClustered();

        builder.HasIndex(order => order.CustomerReference);

        builder.OwnsMany(order => order.Lines, lines =>
        {
            lines.ToTable("OrderLines", table =>
            {
                table.HasCheckConstraint("CK_OrderLines_Quantity_Positive", "[Quantity] > 0");
                table.HasCheckConstraint("CK_OrderLines_UnitPrice_Positive", "[UnitPrice] > 0");
            });

            lines.WithOwner().HasForeignKey("OrderId");

            lines.HasKey("OrderId", nameof(OrderLine.LineNumber));

            lines.Property(line => line.LineNumber).ValueGeneratedNever();

            lines.Property(line => line.ProductCode)
                .HasConversion(code => code.Value, value => ProductCode.Create(value).Value)
                .HasMaxLength(ProductCode.MaxLength)
                .IsUnicode(false)
                .IsRequired();

            lines.Property(line => line.UnitPrice).HasPrecision(18, 2);

            lines.Ignore(line => line.LineTotal);
        });

        builder.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
