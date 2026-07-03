using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;

namespace OneCup.Application.Specifications;

// ────────────────────────── 操作日志 ──────────────────────────

/// <summary>
/// 仅过滤（无分页/排序），专用于 CountAsync 统计总数。
/// 基类 ApplyCriteria 是覆盖语义，多条件必须组合为单一 predicate 一次调用。
/// </summary>
public class OperationLogFilterSpec : Specification<OperationLog>
{
    public OperationLogFilterSpec(OperationLogQuery q)
    {
        var kw = string.IsNullOrWhiteSpace(q.Keyword) ? null : q.Keyword.Trim();
        ApplyCriteria(x =>
            (q.StartTime == null || x.CreatedAt >= q.StartTime) &&
            (q.EndTime == null || x.CreatedAt <= q.EndTime) &&
            (q.UserId == null || x.UserId == q.UserId) &&
            (string.IsNullOrEmpty(q.Username) || x.Username.Contains(q.Username)) &&
            (string.IsNullOrEmpty(q.Module) || x.Module == q.Module) &&
            (string.IsNullOrEmpty(q.Action) || x.Action == q.Action) &&
            (q.Result == null || x.Result == q.Result) &&
            (kw == null || x.RequestPath.Contains(kw) || x.TargetName!.Contains(kw) || x.ErrorMessage!.Contains(kw)));
    }
}

/// <summary>分页查询（含过滤 + 按 CreatedAt 倒序）。</summary>
/// <remarks>
/// 注意：基类 Specification&lt;T&gt;.Criteria 是 private set，子类无法跨实例读取后再 ApplyCriteria。
/// 故 PagedSpec 直接内联条件（与 FilterSpec 的 Where 完全一致），遵循现有 NumberingRulePagedSpec 的写法。
/// </remarks>
public class OperationLogPagedSpec : Specification<OperationLog>
{
    public OperationLogPagedSpec(OperationLogQuery q)
    {
        var kw = string.IsNullOrWhiteSpace(q.Keyword) ? null : q.Keyword.Trim();
        ApplyCriteria(x =>
            (q.StartTime == null || x.CreatedAt >= q.StartTime) &&
            (q.EndTime == null || x.CreatedAt <= q.EndTime) &&
            (q.UserId == null || x.UserId == q.UserId) &&
            (string.IsNullOrEmpty(q.Username) || x.Username.Contains(q.Username)) &&
            (string.IsNullOrEmpty(q.Module) || x.Module == q.Module) &&
            (string.IsNullOrEmpty(q.Action) || x.Action == q.Action) &&
            (q.Result == null || x.Result == q.Result) &&
            (kw == null || x.RequestPath.Contains(kw) || x.TargetName!.Contains(kw) || x.ErrorMessage!.Contains(kw)));
        ApplyOrderByDescending(x => x.CreatedAt);
        ApplyPaging(q.Page, q.PageSize);
    }
}

public class OperationLogByIdSpec : Specification<OperationLog>
{
    public OperationLogByIdSpec(Guid id) => ApplyCriteria(x => x.Id == id);
}

// ────────────────────────── 登录日志 ──────────────────────────

public class LoginLogFilterSpec : Specification<LoginLog>
{
    public LoginLogFilterSpec(LoginLogQuery q)
    {
        ApplyCriteria(x =>
            (q.StartTime == null || x.CreatedAt >= q.StartTime) &&
            (q.EndTime == null || x.CreatedAt <= q.EndTime) &&
            (q.UserId == null || x.UserId == q.UserId) &&
            (string.IsNullOrEmpty(q.Username) || x.Username.Contains(q.Username)) &&
            (q.EventType == null || x.EventType == q.EventType) &&
            (q.Result == null || x.Result == q.Result) &&
            (string.IsNullOrEmpty(q.FailureReason) || x.FailureReason == q.FailureReason));
    }
}

public class LoginLogPagedSpec : Specification<LoginLog>
{
    public LoginLogPagedSpec(LoginLogQuery q)
    {
        ApplyCriteria(x =>
            (q.StartTime == null || x.CreatedAt >= q.StartTime) &&
            (q.EndTime == null || x.CreatedAt <= q.EndTime) &&
            (q.UserId == null || x.UserId == q.UserId) &&
            (string.IsNullOrEmpty(q.Username) || x.Username.Contains(q.Username)) &&
            (q.EventType == null || x.EventType == q.EventType) &&
            (q.Result == null || x.Result == q.Result) &&
            (string.IsNullOrEmpty(q.FailureReason) || x.FailureReason == q.FailureReason));
        ApplyOrderByDescending(x => x.CreatedAt);
        ApplyPaging(q.Page, q.PageSize);
    }
}

public class LoginLogByIdSpec : Specification<LoginLog>
{
    public LoginLogByIdSpec(Guid id) => ApplyCriteria(x => x.Id == id);
}
