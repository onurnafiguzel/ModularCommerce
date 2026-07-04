using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Catalog.Domain.Products;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(Product.DescriptionMaxLength);

        builder.Property(p => p.Sku)
            .HasMaxLength(Product.SkuMaxLength)
            .IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        // Money value object'i aynı tabloda iki kolona açılır (EF complex type).
        builder.ComplexProperty(p => p.Price, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("price_amount")
                .HasPrecision(18, 2);

            price.Property(m => m.Currency)
                .HasColumnName("price_currency")
                .HasMaxLength(3);
        });

        // Domain event'ler persistence'a yazılmaz; outbox Hafta 7'de gelecek.
        builder.Ignore(p => p.DomainEvents);
    }
}
