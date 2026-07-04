using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> builder)
    {
        builder.ToTable("colors");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Hex).HasColumnName("hex").HasMaxLength(7).IsFixedLength().IsRequired();
        builder.Property(c => c.ColorFamily).HasColumnName("color_family").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Code)
            .HasDatabaseName("ux_colors_code")
            .IsUnique();
    }
}
