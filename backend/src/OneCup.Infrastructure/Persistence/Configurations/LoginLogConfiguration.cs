using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        builder.ToTable("login_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(x => x.Result).HasColumnName("result").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(128);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(256);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Ignore(x => x.UpdatedAt);

        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_login_logs_created_at");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_login_logs_user_id");
        builder.HasIndex(x => x.Username).HasDatabaseName("ix_login_logs_username");
    }
}
