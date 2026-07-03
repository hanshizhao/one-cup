using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.Application.Services;

/// <summary>
/// 审计日志查询服务实现。只读，无写入。
/// 遵循"过滤 Spec（COUNT）与分页 Spec（取数）拆开"约定，避免 Count 误加分页。
/// RequestPayload 在返回前再过一次脱敏（纵深防御）。
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IRepository<OperationLog> _ops;
    private readonly IRepository<LoginLog> _logins;

    public AuditLogService(IRepository<OperationLog> ops, IRepository<LoginLog> logins)
    {
        _ops = ops;
        _logins = logins;
    }

    public async Task<PagedResult<OperationLogListItemDto>> SearchOperationsAsync(OperationLogQuery query, CancellationToken ct = default)
    {
        var total = await _ops.CountAsync(new OperationLogFilterSpec(query), ct);
        var rows = await _ops.ListAsync(new OperationLogPagedSpec(query), ct);

        return new PagedResult<OperationLogListItemDto>
        {
            Items = rows.Select(r => new OperationLogListItemDto
            {
                Id = r.Id,
                UserId = r.UserId,
                Username = r.Username,
                Module = r.Module,
                Action = r.Action,
                TargetType = r.TargetType,
                TargetId = r.TargetId,
                TargetName = r.TargetName,
                Result = r.Result,
                StatusCode = r.StatusCode,
                DurationMs = r.DurationMs,
                CreatedAt = r.CreatedAt,
            }).ToList(),
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }

    public async Task<OperationLogDto?> GetOperationAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _ops.FirstOrDefaultAsync(new OperationLogByIdSpec(id), ct);
        if (r is null) return null;
        return ToDto(r);
    }

    public async Task<PagedResult<LoginLogDto>> SearchLoginsAsync(LoginLogQuery query, CancellationToken ct = default)
    {
        var total = await _logins.CountAsync(new LoginLogFilterSpec(query), ct);
        var rows = await _logins.ListAsync(new LoginLogPagedSpec(query), ct);

        return new PagedResult<LoginLogDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }

    public async Task<LoginLogDto?> GetLoginAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _logins.FirstOrDefaultAsync(new LoginLogByIdSpec(id), ct);
        if (r is null) return null;
        return ToDto(r);
    }

    private static OperationLogDto ToDto(OperationLog r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        Username = r.Username,
        Module = r.Module,
        Action = r.Action,
        TargetType = r.TargetType,
        TargetId = r.TargetId,
        TargetName = r.TargetName,
        Result = r.Result,
        HttpMethod = r.HttpMethod,
        RequestPath = r.RequestPath,
        StatusCode = r.StatusCode,
        IpAddress = r.IpAddress,
        UserAgent = r.UserAgent,
        // 纵深防御：DB 中已脱敏，返回前再过一次（防历史脏数据）
        RequestPayload = PayloadSanitizer.Sanitize(r.RequestPayload),
        ErrorMessage = r.ErrorMessage,
        StackTrace = r.StackTrace,
        DurationMs = r.DurationMs,
        TraceId = r.TraceId,
        CreatedAt = r.CreatedAt,
    };

    private static LoginLogDto ToDto(LoginLog r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        Username = r.Username,
        EventType = r.EventType,
        Result = r.Result,
        IpAddress = r.IpAddress,
        UserAgent = r.UserAgent,
        FailureReason = r.FailureReason,
        Message = r.Message,
        CreatedAt = r.CreatedAt,
    };
}
