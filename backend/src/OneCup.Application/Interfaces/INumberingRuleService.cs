using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号规则管理服务（系统管理用，业务模块不需要）。
/// </summary>
public interface INumberingRuleService
{
    Task<PagedResult<NumberingRuleListItemDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? targetType, bool? isActive,
        CancellationToken ct = default);

    Task<NumberingRuleDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<NumberingRuleDto> CreateAsync(CreateNumberingRuleRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateNumberingRuleRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task<PagedResult<NumberingLogListItemDto>> GetLogsAsync(
        int page, int pageSize, string? targetType, string? categoryCode,
        Guid? ruleId, string? code, DateTime? startDate, DateTime? endDate,
        CancellationToken ct = default);
}
