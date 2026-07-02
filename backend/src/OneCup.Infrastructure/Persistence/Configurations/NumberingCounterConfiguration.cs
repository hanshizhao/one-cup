using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingCounterConfiguration : IEntityTypeConfiguration<NumberingCounter>
{
    public void Configure(EntityTypeBuilder<NumberingCounter> builder)
    {
        builder.ToTable("numbering_counters");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RuleId).HasColumnName("rule_id").IsRequired();
        // 空串代替 NULL（参与唯一索引，避免 PG NULL 歧义）
        builder.Property(c => c.CategoryCode).HasColumnName("category_code").HasMaxLength(32).HasDefaultValue("").IsRequired();
        builder.Property(c => c.PeriodKey).HasColumnName("period_key").HasMaxLength(16).HasDefaultValue("").IsRequired();
        builder.Property(c => c.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.Rule)
            .WithMany()
            .HasForeignKey(c => c.RuleId);

        // 桶唯一标识：(rule_id, category_code, period_key)
        builder.HasIndex(c => new { c.RuleId, c.CategoryCode, c.PeriodKey })
            .HasDatabaseName("ux_numbering_counters_bucket")
            .IsUnique();
    }
}
