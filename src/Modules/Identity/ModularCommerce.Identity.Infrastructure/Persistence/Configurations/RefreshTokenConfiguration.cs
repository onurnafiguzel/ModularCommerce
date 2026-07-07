using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Identity.Domain.Users;

namespace ModularCommerce.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        // SHA-256 hex özeti: sabit 64 karakter. Arama hep bu kolondan yapılır.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Ayrı aggregate ama referans bütünlüğü DB'de korunur:
        // kullanıcı silinirse token'ları da silinir.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(t => t.DomainEvents);
    }
}
