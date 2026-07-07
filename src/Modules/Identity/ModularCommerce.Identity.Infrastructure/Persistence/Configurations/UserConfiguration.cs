using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Identity.Domain.Users;

namespace ModularCommerce.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        // Email value object tek kolona açılır; Rehydrate doğrulamayı atlar
        // çünkü DB'deki değer Create'ten geçmiştir.
        builder.Property(u => u.Email)
            .HasConversion(e => e.Value, v => Email.Rehydrate(v))
            .HasColumnName("email")
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
      
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Ignore(u => u.DomainEvents);
    }
}
