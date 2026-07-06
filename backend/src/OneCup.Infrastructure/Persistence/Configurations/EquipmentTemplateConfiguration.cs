using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTemplateConfiguration : IEntityTypeConfiguration<EquipmentTemplate>
{
    public void Configure(EntityTypeBuilder<EquipmentTemplate> builder)
    {
        builder.ToTable("equipment_templates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.ProcessId).HasColumnName("process_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // (类型, 工序, 名称) 三元组唯一
        builder.HasIndex(e => new { e.EquipmentTypeId, e.ProcessId, e.Name })
            .HasDatabaseName("ux_equipment_templates_type_process_name")
            .IsUnique();

        // 工序 FK：Restrict（工序是软删除，此处不级联）
        builder.HasOne<Process>()
            .WithMany()
            .HasForeignKey(e => e.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        // 参数值子集合：级联删除（模板删则值删）
        builder.HasMany(e => e.Values)
            .WithOne()
            .HasForeignKey(v => v.EquipmentTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
