using ECommerce.Domain.Entities.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", ci =>
        {
            ci.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] > 0");
            ci.HasCheckConstraint("CK_CartItems_UnitPriceSnapshot", "[UnitPriceSnapshot] >= 0");
        });

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.CartId)
            .IsRequired();

        builder.Property(ci => ci.ProductId)
            .IsRequired();

        builder.Property(ci => ci.UnitPriceSnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(ci => ci.Quantity)
            .IsRequired()
            .HasDefaultValue(1);

        // Indexes
        builder.HasIndex(ci => new { ci.CartId, ci.ProductId }).IsUnique();
        builder.HasIndex(ci => ci.ProductId);

        // Relationships
        builder.HasOne(ci => ci.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ci => ci.Product)
            .WithMany()
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}