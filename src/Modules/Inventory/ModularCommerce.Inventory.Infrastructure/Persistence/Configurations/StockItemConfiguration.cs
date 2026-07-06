using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Inventory.Domain.Stock;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.ProductId).IsUnique();

        // Postgres'in her satırda tuttuğu xmin sistem kolonu concurrency token olarak map'lenir.
        builder.Property<uint>("xmin").IsRowVersion();

        // Available türetilmiş değerdir, kolon olarak tutulmaz.
        builder.Ignore(s => s.Available);

        builder.Ignore(s => s.DomainEvents);
    }
}
