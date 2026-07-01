using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(100);
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                "user_roles",
                j => j.HasOne<Role>().WithMany().HasForeignKey("role_id"),
                j => j.HasOne<User>().WithMany().HasForeignKey("user_id"),
                j => j.HasKey("user_id", "role_id"));

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId);
    }
}
