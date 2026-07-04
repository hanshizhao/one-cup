using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 客户管理服务实现。
/// CreateAsync 在事务内取号+落库（B+ 不跳号：计数器增量与客户记录同生共死）。
/// 名称唯一性预检用 AnyIgnoringFiltersAsync，识别已软删除占用的名称。
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customers;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;

    public CustomerService(
        IRepository<Customer> customers,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator)
    {
        _customers = customers;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<CustomerListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _customers.CountAsync(new CustomerFilterSpec(keyword, code, isActive), ct);
        var customers = await _customers.ListAsync(
            new CustomerPagedSpec(keyword, code, isActive, page, pageSize), ct);

        return new PagedResult<CustomerListItemDto>
        {
            Items = customers.Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                ShortName = c.ShortName,
                ContactPerson = c.ContactPerson,
                ContactPhone = c.ContactPhone,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct);
        if (c is null) return null;

        return new CustomerDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ShortName = c.ShortName,
            ContactPerson = c.ContactPerson,
            ContactPhone = c.ContactPhone,
            Remark = c.Remark,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        };
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 名称唯一性预检（绕过软删除过滤器）
        if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name), ct))
        {
            throw new DomainException($"客户名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内取号（行锁），计数器增量与客户记录一起提交
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Customer, null, ct);
            var customer = new Customer
            {
                Code = code,
                Name = request.Name,
                ShortName = request.ShortName,
                ContactPerson = request.ContactPerson,
                ContactPhone = request.ContactPhone,
                Remark = request.Remark,
                IsActive = request.IsActive,
            };
            await _customers.AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = customer.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("客户创建失败");
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var customer = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct)
            ?? throw new DomainException("客户不存在");

        // 改名查重（排除自身）
        if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"客户名称「{request.Name}」已存在");
        }

        customer.Name = request.Name;
        customer.ShortName = request.ShortName;
        customer.ContactPerson = request.ContactPerson;
        customer.ContactPhone = request.ContactPhone;
        customer.Remark = request.Remark;
        customer.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);  // 无编号操作，不需事务
        return await GetByIdAsync(id, ct) ?? throw new DomainException("客户更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走 FindAsync（绕过 QueryFilter），已软删客户仍可找到 → 幂等重删返回 204（与 UserService 一致）。
        // 不能用 FirstOrDefaultAsync(CustomerByIdSpec)：它走 Set<T>().AsQueryable() 会应用全局软删除过滤器，
        // 已软删客户被隐藏 → 返回 null → 抛 404，破坏幂等契约。
        var customer = await _customers.GetByIdAsync(id, ct)
            ?? throw new DomainException("客户不存在");

        customer.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }
}
