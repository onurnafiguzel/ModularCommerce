using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularCommerce.Cart.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<CartRecord>
{
    public void Configure(EntityTypeBuilder<CartRecord> builder)
    {
        builder.ToTable("carts");

        builder.HasKey(c => c.CustomerId);
        builder.Property(c => c.CustomerId).ValueGeneratedNever(); // müşteri kimliği = sepet kimliği

        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        // Kalemler tek bir jsonb kolonuna (OwnsMany.ToJson, EF10) — sepet bir bütün olarak okunur/yazılır.
        builder.OwnsMany(c => c.Items, items => items.ToJson());
    }
}
