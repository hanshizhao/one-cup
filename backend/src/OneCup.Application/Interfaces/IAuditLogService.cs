using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 审计日志查询服务（只读）。
/// </summary>
public interface IAuditLogService
{
    Task<PagedResult<OperationLogListItemDto>> SearchOperationsAsync(OperationLogQuery query, CancellationToken ct = default);
    Task<OperationLogDto?> GetOperationAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<LoginLogDto>> SearchLoginsAsync(LoginLogQuery query, CancellationToken ct = default);
    Task<LoginLogDto?> GetLoginAsync(Guid id, CancellationToken ct = default);
}
