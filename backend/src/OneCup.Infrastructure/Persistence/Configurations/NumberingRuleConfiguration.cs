using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingRuleConfiguration : IEntityTypeConfiguration<NumberingRule>
{
    public void Configure(EntityTypeBuilder<NumberingRule> builder)
    {
        builder.ToTable("numbering_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TargetType).HasColumnName("target_type").HasMaxLength(32).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Prefix).HasColumnName("prefix").HasMaxLength(16).IsRequired();
        builder.Property(r => r.IncludeCategory).HasColumnName("include_category").IsRequired();
        builder.Property(r => r.DateSegment).HasColumnName("date_segment").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(r => r.SeqLength).HasColumnName("seq_length").IsRequired();
        builder.Property(r => r.Separator).HasColumnName("separator").HasMaxLength(8).IsRequired();
        builder.Property(r => r.ResetPeriod).HasColumnName("reset_period").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(r => r.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // 部分唯一索引：同一业务类型同时只能有一条启用规则
        builder.HasIndex(r => new { r.TargetType, r.IsActive })
            .HasDatabaseName("ux_numbering_rules_target_type_active")
            .HasFilter("\"is_active\" = true")
            .IsUnique();

        builder.HasIndex(r => r.TargetType).HasDatabaseName("ix_numbering_rules_target_type");
    }
}
