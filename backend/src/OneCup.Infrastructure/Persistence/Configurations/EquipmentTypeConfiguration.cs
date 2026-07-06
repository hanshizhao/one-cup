using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    public void Configure(EntityTypeBuilder<EquipmentType> builder)
    {
        builder.ToTable("equipment_types");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Code).HasDatabaseName("ux_equipment_types_code").IsUnique();
        builder.HasIndex(e => e.Name).HasDatabaseName("ux_equipment_types_name").IsUnique();

        // 参数定义子集合：级联删除（类型删则参数删）
        builder.HasMany(e => e.Parameters)
            .WithOne()
            .HasForeignKey(p => p.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // 运行模板子集合：级联删除
        builder.HasMany(e => e.Templates)
            .WithOne()
            .HasForeignKey(t => t.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
