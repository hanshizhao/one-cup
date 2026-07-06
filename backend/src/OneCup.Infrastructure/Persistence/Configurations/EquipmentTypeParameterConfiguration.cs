using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTypeParameterConfiguration : IEntityTypeConfiguration<EquipmentTypeParameter>
{
    public void Configure(EntityTypeBuilder<EquipmentTypeParameter> builder)
    {
        builder.ToTable("equipment_type_parameters");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ValueType).HasColumnName("value_type").HasConversion<int>();
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.MinValue).HasColumnName("min_value").HasMaxLength(50);
        builder.Property(e => e.MaxValue).HasColumnName("max_value").HasMaxLength(50);
        builder.Property(e => e.Precision).HasColumnName("precision");
        builder.Property(e => e.Options).HasColumnName("options");
        builder.Property(e => e.Required).HasColumnName("required").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // 同类型内参数名唯一
        builder.HasIndex(e => new { e.EquipmentTypeId, e.Name })
            .HasDatabaseName("ux_equipment_type_parameters_type_name")
            .IsUnique();

        // 单位 FK：Restrict，删单位不连带删参数
        builder.HasOne<MeasurementUnit>()
            .WithMany()
            .HasForeignKey(e => e.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
