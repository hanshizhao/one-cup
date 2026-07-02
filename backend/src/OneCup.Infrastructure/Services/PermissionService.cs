using Microsoft.EntityFrameworkCore;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 权限查询服务实现（只读）。
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly OneCupDbContext _db;

    public PermissionService(OneCupDbContext db)
    {
        _db = db;
    }

    public async Task<List<PermissionDto>> GetListAsync(CancellationToken ct = default)
    {
        return await _db.Permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
            })
            .ToListAsync(ct);
    }
}
