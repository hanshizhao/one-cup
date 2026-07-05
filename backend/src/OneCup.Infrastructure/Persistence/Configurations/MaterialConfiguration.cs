using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Spec).HasColumnName("spec").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(m => m.UnitId).HasColumnName("unit_id");
        builder.Property(m => m.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(m => m.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.Code)
            .HasDatabaseName("ux_materials_code")
            .IsUnique();

        // 仅 FK 无导航属性(设计 1.2 节);级联 Restrict,防止删单位连带删物料
        builder.HasOne<MeasurementUnit>()
            .WithMany()
            .HasForeignKey(m => m.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
