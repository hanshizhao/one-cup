using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingCategoryConfiguration : IEntityTypeConfiguration<NumberingCategory>
{
    public void Configure(EntityTypeBuilder<NumberingCategory> builder)
    {
        builder.ToTable("numbering_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TargetTypeCode).HasColumnName("target_type_code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.TargetTypeCode, c.Code })
            .HasDatabaseName("ux_numbering_categories_type_code")
            .IsUnique();

        builder.HasIndex(c => c.TargetTypeCode)
            .HasDatabaseName("ix_numbering_categories_target_type");
    }
}
