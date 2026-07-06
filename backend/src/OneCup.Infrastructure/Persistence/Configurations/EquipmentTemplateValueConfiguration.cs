using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTemplateValueConfiguration : IEntityTypeConfiguration<EquipmentTemplateValue>
{
    public void Configure(EntityTypeBuilder<EquipmentTemplateValue> builder)
    {
        builder.ToTable("equipment_template_values");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTemplateId).HasColumnName("equipment_template_id");
        builder.Property(e => e.ParameterId).HasColumnName("parameter_id");
        builder.Property(e => e.Value).HasColumnName("value").HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // 一个模板对同一参数只有一个值
        builder.HasIndex(e => new { e.EquipmentTemplateId, e.ParameterId })
            .HasDatabaseName("ux_equipment_template_values_template_parameter")
            .IsUnique();

        // 参数定义 FK：不配置导航关系约束（无导航属性）。
        // 注意：参数定义删除时，引用它的模板值应保留为孤儿（由读时校验标记 orphan），
        // 而非被数据库连带删除。但参数定义属于 EquipmentType 子集合，删除通过
        // 类型更新时的整表替换 diff 发生（见 EquipmentTypeService.SyncParameters）。
        // 由于此 FK 无导航属性配置，EF 不会自动管理此关系的级联，
        // 删除参数定义不会触发数据库层面的值删除——孤儿值保留。
        // 不配 HasOne<>().WithMany() 避免 EF 在同聚合多级级联（Type→Parameter→Value）产生冲突。
    }
}
