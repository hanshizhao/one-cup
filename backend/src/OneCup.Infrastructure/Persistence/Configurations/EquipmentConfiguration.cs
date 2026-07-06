using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.Specification).HasColumnName("specification").HasMaxLength(200);
        builder.Property(e => e.Supplier).HasColumnName("supplier").HasMaxLength(100);
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(100);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(e => e.WarrantyExpiry).HasColumnName("warranty_expiry");
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Code).HasDatabaseName("ux_equipments_code").IsUnique();
        builder.HasIndex(e => e.Name).HasDatabaseName("ux_equipments_name").IsUnique();

        // 设备类型 FK：Restrict，删类型前需校验无设备引用（应用层拦截）
        builder.HasOne<EquipmentType>()
            .WithMany()
            .HasForeignKey(e => e.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // 软删除全局过滤器
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
