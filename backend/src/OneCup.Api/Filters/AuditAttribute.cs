using Microsoft.AspNetCore.Mvc.Filters;

namespace OneCup.Api.Filters;

/// <summary>
/// 标注 Controller Action 的审计语义。
/// 贴在 Action 上：无论 GET/POST 都记录，且 Module/Action/TargetType 取特性值。
/// 未贴的 Action：全局 Filter 仅对非 GET 启发式记录。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditAttribute : Attribute, IFilterMetadata
{
    /// <summary>业务模块，如 User/Role/Numbering。</summary>
    public required string Module { get; init; }

    /// <summary>动作，如 Create/Update/Delete。</summary>
    public required string Action { get; init; }

    /// <summary>目标资源类型（可选，默认与 Module 同）。</summary>
    public string? TargetType { get; init; }
}
