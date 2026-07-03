using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using OneCup.Application.Common;
using OneCup.Application.Interfaces;
using OneCup.Api.Services;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;

namespace OneCup.Api.Filters;

/// <summary>
/// 全局操作日志捕获 Filter。
/// - 未贴 [Audit] 的 GET：跳过（避免查询接口海量日志）。
/// - 贴 [Audit] 的任意方法：用特性语义记录。
/// - 未贴 [Audit] 的非 GET：启发式（Module←路由前缀, Action←HTTP方法）记录。
/// 异常也记：DomainException 记 Failed+ErrorMessage（不记堆栈），其他异常记 Failed+堆栈。
/// fire-and-forget：通过 IAuditLogWriter 入队即返回，不影响响应。
/// </summary>
public sealed class OperationLogActionFilter : IAsyncActionFilter
{
    private readonly IAuditLogWriter _writer;
    private readonly CurrentUserService _current;

    public OperationLogActionFilter(IAuditLogWriter writer, CurrentUserService current)
    {
        _writer = writer;
        _current = current;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var method = http.Request.Method;
        var auditAttr = GetAuditAttribute(context);

        // 未标注的 GET 不记
        if (auditAttr is null && IsGet(method))
        {
            await next();
            return;
        }

        var sw = Stopwatch.StartNew();
        var executedContext = await next();
        sw.Stop();

        // 组装日志
        var (module, action, targetType) = ResolveSemantics(auditAttr, http.Request.Path.Value ?? "", method);
        var payload = await ReadPayloadAsync(context);
        var status = executedContext.HttpContext.Response.StatusCode;
        var traceId = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier;
        var ip = http.Connection.RemoteIpAddress?.ToString();
        var ua = http.Request.Headers.UserAgent.ToString();
        if (ua.Length > 256) ua = ua[..256];

        var log = new OperationLog
        {
            UserId = _current.UserId,
            Username = _current.Username ?? "",
            Module = module,
            Action = action,
            TargetType = targetType,
            HttpMethod = method,
            RequestPath = http.Request.Path.Value ?? "",
            StatusCode = status,
            IpAddress = ip,
            UserAgent = ua,
            RequestPayload = payload,
            DurationMs = (int)sw.ElapsedMilliseconds,
            TraceId = traceId,
        };

        // 结果与异常
        if (executedContext.Exception is not null)
        {
            log.Result = OperationResult.Failed;
            // DomainException 是业务校验失败（400），不记堆栈；其他（500）记堆栈
            if (executedContext.Exception is OneCup.Domain.Exceptions.DomainException de)
            {
                log.ErrorMessage = de.Message;
            }
            else
            {
                log.ErrorMessage = executedContext.Exception.Message;
                log.StackTrace = executedContext.Exception.ToString();
            }
            // 异常已被全局异常处理器接管，这里不设 handled
        }
        else
        {
            log.Result = status >= 400 ? OperationResult.Failed : OperationResult.Success;
        }

        // 捕获目标 Id：优先路由 {id}，其次 CreatedAtAction 的路由值
        if (context.RouteData.Values.TryGetValue("id", out var idVal) && idVal is not null)
            log.TargetId = idVal.ToString();
        else if (executedContext.Result is CreatedAtActionResult created)
            log.TargetId = created.RouteValues?.TryGetValue("id", out var cid) == true ? cid?.ToString() : null;

        _writer.Enqueue(log);
    }

    private static AuditAttribute? GetAuditAttribute(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad && cad.MethodInfo is not null)
        {
            return cad.MethodInfo.GetCustomAttributes(typeof(AuditAttribute), inherit: true)
                .OfType<AuditAttribute>().FirstOrDefault();
        }
        return null;
    }

    private static bool IsGet(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase);

    /// <summary>解析业务语义：有特性用特性，否则启发式。</summary>
    private static (string module, string action, string? targetType) ResolveSemantics(
        AuditAttribute? attr, string path, string method)
    {
        if (attr is not null)
            return (attr.Module, attr.Action, attr.TargetType ?? attr.Module);

        // 启发式：/api/users/{id} → module "Users"（首段）；action 由 HTTP 方法推断
        var seg = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var module = seg.Length >= 2 && seg[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            ? Capitalize(seg[1])
            : (seg.Length >= 1 ? Capitalize(seg[0]) : "Unknown");

        var action = method.ToUpperInvariant() switch
        {
            "POST" => "Create",
            "PUT" => "Update",
            "DELETE" => "Delete",
            "PATCH" => "Update",
            _ => "Action",
        };
        return (module, action, module);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>读取请求体并脱敏。仅 [FromBody] 的 JSON 入参有意义。</summary>
    private static async Task<string?> ReadPayloadAsync(ActionExecutingContext context)
    {
        // 取 action 参数中标记 [FromBody] 的值
        object? body = null;
        foreach (var p in context.ActionDescriptor.Parameters)
        {
            if (context.ActionArguments.TryGetValue(p.Name, out var v) && v is not null
                && p.BindingInfo?.BindingSource?.Id == "Body")
            {
                body = v;
                break;
            }
        }
        if (body is null) return null;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(body);
            return PayloadSanitizer.Sanitize(json);
        }
        catch
        {
            return "[binary]";
        }
    }
}
