using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderSubmission.Application.Idempotency;
using OrderSubmission.Domain.Orders;

namespace OrderSubmission.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public const string PrimaryKeyName = "PK_IdempotencyKeys";

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyKeys");

        builder.HasKey(record => record.Key)
            .HasName(PrimaryKeyName)
            .IsClustered(false);

        builder.Property(record => record.Key)
            .HasConversion(key => key.Value, value => IdempotencyKey.Create(value).Value)
            .HasMaxLength(IdempotencyKey.MaxLength)
            .IsUnicode(false);

        builder.Property(record => record.RequestHash)
            .HasMaxLength(RequestFingerprint.Length)
            .IsFixedLength();

        builder.Property(record => record.OrderId)
            .HasConversion(id => id.Value, value => new OrderId(value));

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<IdempotencyRecord>(record => record.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(record => record.CreatedAt).IsClustered();
    }
}
