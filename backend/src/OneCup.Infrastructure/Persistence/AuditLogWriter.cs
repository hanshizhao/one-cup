using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// IAuditLogWriter 实现：把日志实体包成 AuditLogEntry 写进 Channel，fire-and-forget。
/// void 返回——入队即返回，永不阻塞调用方。
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly AuditLogChannel _channel;

    public AuditLogWriter(AuditLogChannel channel)
    {
        _channel = channel;
    }

    public void Enqueue(OperationLog log)
    {
        // TryWrite：非阻塞。DropOldest 模式下队列满会自动丢弃最老的，TryWrite 总返回 true。
        _channel.Writer.TryWrite(new AuditLogEntry { Operation = log });
    }

    public void Enqueue(LoginLog log)
    {
        _channel.Writer.TryWrite(new AuditLogEntry { Login = log });
    }
}
