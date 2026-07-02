using Microsoft.EntityFrameworkCore;
using OneCup.Application.Services;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.System;

public class PermissionServiceTests
{
    private static async Task<PermissionService> SetupAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new OneCupDbContext(options);
        db.Permissions.AddRange(
            new Permission { Code = "system:role:manage", Name = "管理角色" },
            new Permission { Code = "fabric:read", Name = "查看面料" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new PermissionService(new Repository<Permission>(db));
    }

    [Fact]
    public async Task GetList_returns_permissions_ordered_by_code()
    {
        var svc = await SetupAsync(nameof(GetList_returns_permissions_ordered_by_code));
        var result = await svc.GetListAsync();
        Assert.Equal(2, result.Count);
        Assert.Equal("fabric:read", result[0].Code);          // f < s
        Assert.Equal("system:role:manage", result[1].Code);
    }
}
