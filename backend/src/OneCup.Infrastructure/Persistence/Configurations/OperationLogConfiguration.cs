using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        builder.ToTable("operation_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(32).IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(64);
        builder.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(64);
        builder.Property(x => x.TargetName).HasColumnName("target_name").HasMaxLength(128);
        builder.Property(x => x.Result).HasColumnName("result").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(8).IsRequired();
        builder.Property(x => x.RequestPath).HasColumnName("request_path").HasMaxLength(256).IsRequired();
        builder.Property(x => x.StatusCode).HasColumnName("status_code").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(256);
        // RequestPayload 脱敏后的入参 JSON，用 jsonb 列存储便于查询
        builder.Property(x => x.RequestPayload).HasColumnName("request_payload").HasColumnType("jsonb");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.StackTrace).HasColumnName("stack_trace");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        // OperationLog 不更新，UpdatedAt 无意义，Ignore 避免建无用列
        builder.Ignore(x => x.UpdatedAt);

        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_op_logs_created_at");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_op_logs_user_id");
        builder.HasIndex(x => new { x.Module, x.Action }).HasDatabaseName("ix_op_logs_module_action");
    }
}
