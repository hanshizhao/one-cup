using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingTargetTypeConfiguration : IEntityTypeConfiguration<NumberingTargetType>
{
    public void Configure(EntityTypeBuilder<NumberingTargetType> builder)
    {
        builder.ToTable("numbering_target_types");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(t => t.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(t => t.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(t => t.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.Code)
            .HasDatabaseName("ux_numbering_target_types_code")
            .IsUnique();
    }
}
