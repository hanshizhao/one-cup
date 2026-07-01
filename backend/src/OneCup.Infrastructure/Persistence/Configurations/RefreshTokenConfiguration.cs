using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id).HasColumnName("id");
        builder.Property(rt => rt.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
        builder.Property(rt => rt.UserId).HasColumnName("user_id");
        builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at");
        builder.Property(rt => rt.IsRevoked).HasColumnName("is_revoked").IsRequired();
        builder.Property(rt => rt.CreatedAt).HasColumnName("created_at");
        builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(rt => rt.Token).IsUnique();
    }
}
