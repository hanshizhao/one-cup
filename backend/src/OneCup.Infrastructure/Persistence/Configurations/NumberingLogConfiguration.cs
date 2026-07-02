using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingLogConfiguration : IEntityTypeConfiguration<NumberingLog>
{
    public void Configure(EntityTypeBuilder<NumberingLog> builder)
    {
        builder.ToTable("numbering_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.GeneratedCode).HasColumnName("generated_code").HasMaxLength(64).IsRequired();
        builder.Property(l => l.RuleId).HasColumnName("rule_id").IsRequired();
        builder.Property(l => l.TargetType).HasColumnName("target_type").HasMaxLength(32).IsRequired();
        builder.Property(l => l.CategoryCode).HasColumnName("category_code").HasMaxLength(32);
        builder.Property(l => l.PeriodKey).HasColumnName("period_key").HasMaxLength(16);
        builder.Property(l => l.SeqValue).HasColumnName("seq_value").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        builder.HasOne(l => l.Rule)
            .WithMany()
            .HasForeignKey(l => l.RuleId);

        builder.HasIndex(l => l.GeneratedCode).HasDatabaseName("ix_numbering_logs_code");
        builder.HasIndex(l => new { l.RuleId, l.CreatedAt }).HasDatabaseName("ix_numbering_logs_rule_id");
        builder.HasIndex(l => new { l.TargetType, l.CreatedAt }).HasDatabaseName("ix_numbering_logs_target_type");
    }
}
