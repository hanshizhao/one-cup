using System.Threading.Channels;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 审计日志的有界 Channel（单例）。
/// 多生产者（多个 HTTP 请求）单消费者（AuditLogQueueConsumer）。
/// 满时 DropOldest：丢最老的条目，保最近（追责优先近期）。
/// 容量由 AuditLogOptions.QueueCapacity 配置（默认 10000）。
/// </summary>
public sealed class AuditLogChannel : IDisposable
{
    private readonly Channel<AuditLogEntry> _channel;

    public AuditLogChannel(int capacity)
    {
        _channel = Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelWriter<AuditLogEntry> Writer => _channel.Writer;
    public ChannelReader<AuditLogEntry> Reader => _channel.Reader;

    public void Dispose() => _channel.Writer.TryComplete();
}

/// <summary>
/// 队列条目：包装操作日志或登录日志（两者结构不同，统一入口）。
/// 两个可空字段恰好一个非空。
/// </summary>
public sealed record AuditLogEntry
{
    public OperationLog? Operation { get; init; }
    public LoginLog? Login { get; init; }
}
