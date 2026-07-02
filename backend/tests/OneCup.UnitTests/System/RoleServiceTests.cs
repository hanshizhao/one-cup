using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.System;

public class RoleServiceTests
{
    private static (OneCupDbContext db, RoleService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"role-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        db.Roles.Add(new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin" });
        db.Permissions.Add(new Permission { Id = SeedData.PermFabricRead, Code = "fabric:read", Name = "查看面料" });
        db.SaveChanges();

        var svc = new RoleService(db);
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllRoles()
    {
        var (db, svc) = Setup();
        var result = await svc.GetListAsync();
        Assert.Single(result); // admin
    }

    [Fact]
    public async Task CreateAsync_CreatesRole()
    {
        var (db, svc) = Setup();
        var result = await svc.CreateAsync(new CreateRoleRequest { Name = "测试角色", Code = "tester" });
        Assert.Equal("tester", result.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateRoleRequest { Name = "管理员2", Code = "admin" }));
    }

    [Fact]
    public async Task UpdateAsync_AssignsPermissions()
    {
        var (db, svc) = Setup();
        var role = new Role { Name = "开发", Code = "dev" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(role.Id, new UpdateRoleRequest
        {
            Name = "开发",
            PermissionIds = [SeedData.PermFabricRead],
        });
        Assert.Contains(SeedData.PermFabricRead, result.PermissionIds);
    }

    [Fact]
    public async Task DeleteAsync_AdminRole_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(SeedData.AdminRoleId));
    }
}
