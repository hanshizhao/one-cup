using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class MeasurementUnitConfiguration : IEntityTypeConfiguration<MeasurementUnit>
{
    public void Configure(EntityTypeBuilder<MeasurementUnit> builder)
    {
        builder.ToTable("measurement_units");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(u => u.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(u => u.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(u => u.Symbol).HasColumnName("symbol").HasMaxLength(16).IsRequired();
        builder.Property(u => u.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(u => u.IsBase).HasColumnName("is_base").IsRequired();
        builder.Property(u => u.Factor).HasColumnName("factor").HasPrecision(18, 8).IsRequired();
        builder.Property(u => u.Precision).HasColumnName("precision").IsRequired();
        builder.Property(u => u.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Code)
            .HasDatabaseName("ux_measurement_units_code")
            .IsUnique();

        builder.HasIndex(u => u.Category)
            .HasDatabaseName("ix_measurement_units_category");
    }
}
