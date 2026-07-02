using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IPermissionService
{
    Task<List<PermissionDto>> GetListAsync(CancellationToken ct = default);
}
