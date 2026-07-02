using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.System;

public class UserServiceTests
{
    private static (OneCupDbContext db, UserService svc) Setup(params User[] seedUsers)
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"user-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        // 种子角色
        var adminRole = new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin" };
        var devRole = new Role { Id = SeedData.DeveloperRoleId, Name = "开发员", Code = "developer" };
        db.Roles.AddRange(adminRole, devRole);
        db.Users.AddRange(seedUsers);
        db.SaveChanges();

        var svc = new UserService(db, new PasswordHasher());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static User MakeUser(string username, string display) => new()
    {
        Username = username,
        DisplayName = display,
        PasswordHash = "$2a$12$placeholder",
        IsActive = true,
    };

    [Fact]
    public async Task GetListAsync_ReturnsPagedResults()
    {
        var (db, svc) = Setup(MakeUser("user1", "用户一"), MakeUser("user2", "用户二"));
        var result = await svc.GetListAsync(1, 10, null);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_KeywordFiltersByUsername()
    {
        var (db, svc) = Setup(MakeUser("alice", "爱丽丝"), MakeUser("bob", "鲍勃"));
        var result = await svc.GetListAsync(1, 10, "alice");
        Assert.Single(result.Items);
        Assert.Equal("alice", result.Items[0].Username);
    }

    [Fact]
    public async Task CreateAsync_CreatesUserWithRoles()
    {
        var (db, svc) = Setup();
        var result = await svc.CreateAsync(new CreateUserRequest
        {
            Username = "newuser",
            DisplayName = "新用户",
            Password = "Pass@123",
            RoleIds = [SeedData.DeveloperRoleId],
        });
        Assert.Equal("newuser", result.Username);
        Assert.Contains(SeedData.DeveloperRoleId, result.RoleIds);
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_Throws()
    {
        var (db, svc) = Setup(MakeUser("dup", "重复用户"));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateUserRequest { Username = "dup", DisplayName = "x", Password = "p" }));
    }

    [Fact]
    public async Task UpdateStatusAsync_DisableAdminUser_Throws()
    {
        var adminUser = MakeUser("admin", "管理员");
        adminUser.Id = SeedData.AdminUserId;
        var (db, svc) = Setup(adminUser);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateStatusAsync(SeedData.AdminUserId, new UpdateStatusRequest { IsActive = false }));
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesHash()
    {
        var user = MakeUser("u1", "用户一");
        var (db, svc) = Setup(user);
        await svc.ResetPasswordAsync(user.Id, new ResetPasswordRequest { NewPassword = "NewPass@456" });
        var updated = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.NotEqual("$2a$12$placeholder", updated.PasswordHash);
    }

    [Fact]
    public async Task UpdateAsync_DisableAdminUser_Throws()
    {
        var adminUser = MakeUser("admin", "管理员");
        adminUser.Id = SeedData.AdminUserId;
        var (db, svc) = Setup(adminUser);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(SeedData.AdminUserId, new UpdateUserRequest
            {
                DisplayName = "管理员",
                IsActive = false,
                RoleIds = [],
            }));
    }
}
