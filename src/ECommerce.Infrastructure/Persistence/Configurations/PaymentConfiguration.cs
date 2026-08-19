using ECommerce.Domain.Entities.Ordering;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", p =>
        {
            p.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderId)
            .IsRequired();

        // Convert Enum to String in Database
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PaymentStatus.Pending);

        builder.Property(p => p.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.TransactionReference)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(p => p.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(p => p.PaidAtUtc)
            .IsRequired(false);

        // 1:1 Unique Constraint with Order
        builder.HasIndex(p => p.OrderId)
            .IsUnique();

        builder.HasIndex(p => p.Status);
    }
}