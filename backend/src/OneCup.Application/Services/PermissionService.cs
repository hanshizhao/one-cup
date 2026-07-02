using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.Application.Services;

/// <summary>
/// 权限查询服务实现(只读)。
/// 通过 IRepository + AllPermissionsSpec 查询,按 Code 排序后投影为 PermissionDto。
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IRepository<Permission> _permissions;

    public PermissionService(IRepository<Permission> permissions)
    {
        _permissions = permissions;
    }

    public async Task<List<PermissionDto>> GetListAsync(CancellationToken ct = default)
    {
        var list = await _permissions.ListAsync(new AllPermissionsSpec(), ct);
        return list.Select(p => new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
        }).ToList();
    }
}
