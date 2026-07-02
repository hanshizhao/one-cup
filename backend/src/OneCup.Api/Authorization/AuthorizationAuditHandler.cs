// backend/src/OneCup.Api/Authorization/AuthorizationAuditHandler.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace OneCup.Api.Authorization;

/// <summary>
/// 在授权失败(403)时记录安全审计日志。
/// 通过 IAuthorizationMiddlewareResultHandler 装饰,不改变授权结果。
/// </summary>
public class AuthorizationAuditHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();
    private readonly ILogger<AuthorizationAuditHandler> _logger;

    public AuthorizationAuditHandler(ILogger<AuthorizationAuditHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult result)
    {
        if (!result.Succeeded)
        {
            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var endpoint = context.GetEndpoint()?.DisplayName;
            _logger.LogWarning("权限拒绝:UserId={UserId}, Endpoint={Endpoint}", userId, endpoint);
        }
        await _inner.HandleAsync(next, context, policy, result);
    }
}
