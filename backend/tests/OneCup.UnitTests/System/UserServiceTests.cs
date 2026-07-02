using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
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

        var svc = new UserService(
            new Repository<User>(db), new Repository<Role>(db), new Repository<RefreshToken>(db),
            new UnitOfWork(db), new PasswordHasher(),
            new CreateUserRequestValidator(), new UpdateUserRequestValidator(), new ResetPasswordRequestValidator());
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
            svc.CreateAsync(new CreateUserRequest
            {
                Username = "dup",
                DisplayName = "重复用户",
                Password = "Password1",
                RoleIds = [SeedData.DeveloperRoleId],
            }));
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
                RoleIds = [SeedData.AdminRoleId],
            }));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesUser()
    {
        var user = MakeUser("del", "待删除");
        var (db, svc) = Setup(user);

        await svc.DeleteAsync(user.Id);

        // 权威断言：直接查 DB(绕过 QueryFilter)确认 IsDeleted 已置 true。
        var raw = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.True(raw.IsDeleted);

        // InMemory 在 LINQ 查询上确实执行 HasQueryFilter(经探测验证)，
        // 故经仓储/规范过滤后的 GetByIdAsync 应返回 null。
        var gone = await svc.GetByIdAsync(user.Id);
        Assert.Null(gone);
    }

    [Fact]
    public async Task DeleteAsync_AdminUser_Throws()
    {
        var adminUser = MakeUser("admin", "管理员");
        adminUser.Id = SeedData.AdminUserId;
        var (db, svc) = Setup(adminUser);

        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(SeedData.AdminUserId));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentUser_Throws()
    {
        var (db, svc) = Setup();

        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_RevokesActiveRefreshTokens()
    {
        var user = MakeUser("tk", "令牌用户");
        var (db, svc) = Setup(user);

        // 2 个未吊销 + 1 个已吊销令牌
        db.RefreshTokens.AddRange(
            new RefreshToken { Token = "t1", UserId = user.Id, IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new RefreshToken { Token = "t2", UserId = user.Id, IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new RefreshToken { Token = "t3", UserId = user.Id, IsRevoked = true, ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.SaveChanges();
        // 清理变更跟踪器,避免上面 attach 的实体干扰后续断言。
        db.ChangeTracker.Clear();

        await svc.DeleteAsync(user.Id);

        var tokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == user.Id).ToListAsync();
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }
}
