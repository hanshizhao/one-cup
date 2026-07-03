using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using OneCup.Application.Interfaces;
using OneCup.Api.Filters;
using OneCup.Api.Services;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.AuditLog;

/// <summary>
/// 捕获写入的测试替身 IAuditLogWriter：记录所有入队的 OperationLog 供断言。
/// </summary>
internal sealed class FakeAuditLogWriter : IAuditLogWriter
{
    public List<OperationLog> Operations { get; } = new();
    public List<LoginLog> Logins { get; } = new();

    public void Enqueue(OperationLog log) => Operations.Add(log);
    public void Enqueue(LoginLog log) => Logins.Add(log);
}

public class OperationLogActionFilterTests
{
    private static DefaultHttpContext MakeContext(string method, string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = path;
        return http;
    }

    private static CurrentUserService NoUser() =>
        new(new HttpContextAccessor()); // 无登录态，UserId/Username 为 null

    [Fact]
    public async Task Filter_NonAuditGetRequest_Skips()
    {
        var writer = new FakeAuditLogWriter();
        var filter = new OperationLogActionFilter(writer, NoUser());
        var ctx = new ActionExecutingContext(
            new ActionContext(MakeContext("GET", "/api/users"), new RouteData(),
                new ControllerActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());

        bool nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), ctx.Controller));
        });

        Assert.True(nextCalled);
        Assert.Empty(writer.Operations); // GET 未标注不记录
    }

    [Fact]
    public async Task Filter_NonGetRequest_RecordsWithHeuristic()
    {
        var writer = new FakeAuditLogWriter();
        var filter = new OperationLogActionFilter(writer, NoUser());
        var http = MakeContext("POST", "/api/users");
        http.Response.StatusCode = 200;
        var ctx = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ControllerActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        await filter.OnActionExecutionAsync(ctx, () =>
            Task.FromResult<ActionExecutedContext>(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), ctx.Controller)));

        Assert.Single(writer.Operations);
        var log = writer.Operations[0];
        Assert.Equal("Users", log.Module);      // 启发式：/api/users → Users
        Assert.Equal("Create", log.Action);     // POST → Create
        Assert.Equal("POST", log.HttpMethod);
    }

    [Fact]
    public async Task Filter_DeleteRequest_ActionIsDelete()
    {
        var writer = new FakeAuditLogWriter();
        var filter = new OperationLogActionFilter(writer, NoUser());
        var http = MakeContext("DELETE", "/api/users/123");
        http.Response.StatusCode = 204;
        var routeData = new RouteData();
        routeData.Values["id"] = "123";
        var ctx = new ActionExecutingContext(
            new ActionContext(http, routeData, new ControllerActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        await filter.OnActionExecutionAsync(ctx, () =>
            Task.FromResult<ActionExecutedContext>(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), ctx.Controller)));

        var log = writer.Operations[0];
        Assert.Equal("Delete", log.Action);
        Assert.Equal("123", log.TargetId); // 从路由 {id} 取
    }

    [Fact]
    public async Task Filter_Exception_RecordsFailedWithStackTrace()
    {
        var writer = new FakeAuditLogWriter();
        var filter = new OperationLogActionFilter(writer, NoUser());
        var http = MakeContext("POST", "/api/users");
        var ctx = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ControllerActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var boom = new InvalidOperationException("boom");
        await filter.OnActionExecutionAsync(ctx, () =>
            Task.FromResult<ActionExecutedContext>(
                new ActionExecutedContext(ctx, new List<IFilterMetadata>(), ctx.Controller) { Exception = boom }));

        var log = writer.Operations[0];
        Assert.Equal(OneCup.Domain.Enums.OperationResult.Failed, log.Result);
        Assert.NotNull(log.StackTrace);
    }
}
