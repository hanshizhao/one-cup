using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.AuditLog;

public class AuditLogServiceTests
{
    private static (OneCupDbContext db, AuditLogService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new AuditLogService(
            new Repository<OperationLog>(db),
            new Repository<LoginLog>(db));
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static OperationLog MakeOp(string module, string action, OperationResult result = OperationResult.Success, string username = "admin") => new()
    {
        UserId = Guid.NewGuid(),
        Username = username,
        Module = module,
        Action = action,
        Result = result,
        HttpMethod = "POST",
        RequestPath = $"/api/{module.ToLower()}",
        StatusCode = 200,
    };

    [Fact]
    public async Task SearchOperationsAsync_FiltersByModule()
    {
        var (db, svc) = Setup();
        db.OperationLogs.AddRange(MakeOp("User", "Create"), MakeOp("Role", "Create"));
        await db.SaveChangesAsync();

        var result = await svc.SearchOperationsAsync(new OperationLogQuery { Page = 1, PageSize = 10, Module = "User" });

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("User", result.Items.Single().Module);
    }

    [Fact]
    public async Task SearchOperationsAsync_FiltersByResult()
    {
        var (db, svc) = Setup();
        db.OperationLogs.AddRange(
            MakeOp("User", "Create", OperationResult.Success),
            MakeOp("User", "Update", OperationResult.Failed));
        await db.SaveChangesAsync();

        var result = await svc.SearchOperationsAsync(new OperationLogQuery { Page = 1, PageSize = 10, Result = OperationResult.Failed });

        Assert.Equal(1, result.Total);
        Assert.Equal(OperationResult.Failed, result.Items.Single().Result);
    }

    [Fact]
    public async Task SearchOperationsAsync_Pagination_TotalNotAffectedByPaging()
    {
        // 回归：Count 不能误加 Skip/Take（FilterSpec 与 PagedSpec 拆分的核心约束）
        var (db, svc) = Setup();
        for (var i = 0; i < 15; i++)
            db.OperationLogs.Add(MakeOp("User", "Create"));
        await db.SaveChangesAsync();

        var page1 = await svc.SearchOperationsAsync(new OperationLogQuery { Page = 1, PageSize = 10 });
        var page2 = await svc.SearchOperationsAsync(new OperationLogQuery { Page = 2, PageSize = 10 });

        Assert.Equal(15, page1.Total);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(15, page2.Total);
        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task SearchOperationsAsync_KeywordMatchesRequestPath()
    {
        var (db, svc) = Setup();
        db.OperationLogs.AddRange(
            MakeOp("User", "Create"),     // path /api/user
            MakeOp("Role", "Create"));    // path /api/role
        await db.SaveChangesAsync();

        var result = await svc.SearchOperationsAsync(new OperationLogQuery { Page = 1, PageSize = 10, Keyword = "role" });

        Assert.Equal(1, result.Total);
        Assert.Equal("Role", result.Items.Single().Module);
    }

    [Fact]
    public async Task GetOperationAsync_NotFound_ReturnsNull()
    {
        var (db, svc) = Setup();
        var result = await svc.GetOperationAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOperationAsync_ReturnsDetailWithStackTrace()
    {
        var (db, svc) = Setup();
        var op = MakeOp("User", "Create");
        op.StackTrace = "at Foo() in Bar.cs:line 1";
        op.RequestPayload = """{"name":"x"}""";
        db.OperationLogs.Add(op);
        await db.SaveChangesAsync();

        var detail = await svc.GetOperationAsync(op.Id);

        Assert.NotNull(detail);
        Assert.Equal("at Foo() in Bar.cs:line 1", detail!.StackTrace);
        Assert.Equal("""{"name":"x"}""", detail.RequestPayload);
    }

    [Fact]
    public async Task SearchLoginsAsync_FiltersByEventType()
    {
        var (db, svc) = Setup();
        db.LoginLogs.AddRange(
            new LoginLog { Username = "a", EventType = LoginEventType.Login, Result = OperationResult.Success },
            new LoginLog { Username = "b", EventType = LoginEventType.Logout, Result = OperationResult.Success });
        await db.SaveChangesAsync();

        var result = await svc.SearchLoginsAsync(new LoginLogQuery { Page = 1, PageSize = 10, EventType = LoginEventType.Logout });

        Assert.Equal(1, result.Total);
        Assert.Equal(LoginEventType.Logout, result.Items.Single().EventType);
    }

    [Fact]
    public async Task SearchLoginsAsync_FiltersByUsernameContains()
    {
        var (db, svc) = Setup();
        db.LoginLogs.AddRange(
            new LoginLog { Username = "administrator", EventType = LoginEventType.Login, Result = OperationResult.Success },
            new LoginLog { Username = "guest", EventType = LoginEventType.Login, Result = OperationResult.Failed });
        await db.SaveChangesAsync();

        var result = await svc.SearchLoginsAsync(new LoginLogQuery { Page = 1, PageSize = 10, Username = "admin" });

        Assert.Equal(1, result.Total);
        Assert.Equal("administrator", result.Items.Single().Username);
    }
}
