using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 操作日志：记录用户对系统的写操作（及标注的敏感读操作）。
/// 只追加，不可修改。UpdatedAt 永远为 null。
/// </summary>
public class OperationLog : BaseEntity
{
    /// <summary>操作人 Id；匿名请求（如登录失败）为 null。</summary>
    public Guid? UserId { get; set; }

    /// <summary>操作人用户名快照（账号可能改名/删除，审计保留当时的名字）。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>业务模块：User/Role/Numbering/Auth 等。</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>动作：Create/Update/Delete/Login 等。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>目标资源类型，如 User。</summary>
    public string? TargetType { get; set; }

    /// <summary>目标资源 Id（Guid 转字符串，兼容复合键）。</summary>
    public string? TargetId { get; set; }

    /// <summary>目标资源名称（可读性）。</summary>
    public string? TargetName { get; set; }

    /// <summary>操作结果。</summary>
    public OperationResult Result { get; set; }

    /// <summary>HTTP 方法：POST/PUT/DELETE。</summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>请求路由模板，如 /api/users/{id}。</summary>
    public string RequestPath { get; set; } = string.Empty;

    /// <summary>HTTP 响应码。</summary>
    public int StatusCode { get; set; }

    /// <summary>客户端 IP。</summary>
    public string? IpAddress { get; set; }

    /// <summary>客户端 User-Agent（截断）。</summary>
    public string? UserAgent { get; set; }

    /// <summary>脱敏后的请求入参（JSON 字符串）。</summary>
    public string? RequestPayload { get; set; }

    /// <summary>失败时的错误消息（DomainException.Message）。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>仅 500 异常记录的堆栈（400 不记，避免噪音）。</summary>
    public string? StackTrace { get; set; }

    /// <summary>耗时毫秒。</summary>
    public int DurationMs { get; set; }

    /// <summary>W3C TraceContext 的 traceId，关联同一请求。</summary>
    public string? TraceId { get; set; }
}
