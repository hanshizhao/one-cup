using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 审计日志写入端口（fire-and-forget）。
/// 实现负责把日志实体入队，由后台消费者异步批量写库，永不阻塞调用方。
/// void 返回值明确告知调用方：不要 await、不要依赖写入完成。
/// </summary>
public interface IAuditLogWriter
{
    void Enqueue(OperationLog log);
    void Enqueue(LoginLog log);
}
