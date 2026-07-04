using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);

    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
