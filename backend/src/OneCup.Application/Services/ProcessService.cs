using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 工序管理服务实现。
/// CreateAsync 在事务内取号+落库（计数器增量与工序记录同生共死，不跳号）。
/// 名称「分类内唯一」预检用 AnyIgnoringFiltersAsync，识别已软删除占用的名称。
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IRepository<Process> _processes;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateProcessRequest> _createValidator;
    private readonly IValidator<UpdateProcessRequest> _updateValidator;

    public ProcessService(
        IRepository<Process> processes,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateProcessRequest> createValidator,
        IValidator<UpdateProcessRequest> updateValidator)
    {
        _processes = processes;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<ProcessListItemDto>> GetListAsync(
        string? keyword, string? category, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _processes.CountAsync(new ProcessFilterSpec(keyword, category, isActive), ct);
        var items = await _processes.ListAsync(
            new ProcessPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<ProcessListItemDto>
        {
            Items = items.Select(p => new ProcessListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Category = p.Category,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ProcessDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _processes.FirstOrDefaultAsync(new ProcessByIdSpec(id), ct);
        if (p is null) return null;

        return new ProcessDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Category = p.Category,
            SortOrder = p.SortOrder,
            Remark = p.Remark,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
    }

    public async Task<ProcessDto> CreateAsync(CreateProcessRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 名称「分类内唯一」预检（绕过软删除过滤器）
        if (await _processes.AnyIgnoringFiltersAsync(
            new ProcessByNameSpec(request.Name, request.Category), ct))
        {
            throw new DomainException($"工序名称「{request.Name}」在该分类下已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内取号（行锁），计数器增量与工序记录一起提交
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Process, request.CategoryCode, ct);
            var process = new Process
            {
                Code = code,
                Name = request.Name,
                Category = request.Category,
                SortOrder = request.SortOrder,
                Remark = request.Remark,
                IsActive = request.IsActive,
            };
            await _processes.AddAsync(process, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = process.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("工序创建失败");
    }

    public async Task<ProcessDto> UpdateAsync(Guid id, UpdateProcessRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var process = await _processes.FirstOrDefaultAsync(new ProcessByIdSpec(id), ct)
            ?? throw new DomainException("工序不存在");

        // 改名/改分类查重（排除自身）
        if (await _processes.AnyIgnoringFiltersAsync(
            new ProcessByNameSpec(request.Name, request.Category, id), ct))
        {
            throw new DomainException($"工序名称「{request.Name}」在该分类下已存在");
        }

        process.Name = request.Name;
        process.Category = request.Category;
        process.SortOrder = request.SortOrder;
        process.Remark = request.Remark;
        process.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);  // 无编号操作，不需事务
        return await GetByIdAsync(id, ct) ?? throw new DomainException("工序更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走 FindAsync（绕过 QueryFilter），已软删工序仍可找到 → 幂等重删返回 204。
        var process = await _processes.GetByIdAsync(id, ct)
            ?? throw new DomainException("工序不存在");

        process.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }
}
