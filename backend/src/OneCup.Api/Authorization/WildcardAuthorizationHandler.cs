using Microsoft.AspNetCore.Authorization;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Authorization;

/// <summary>
/// 通配授权:用户 perm_codes 含 "*" 时放行所有策略。
/// 通配语义委托 IPermissionCalculator,保持单一来源。
/// </summary>
public class WildcardAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    private readonly IPermissionCalculator _permCalc;

    public WildcardAuthorizationHandler(IPermissionCalculator permCalc)
    {
        _permCalc = permCalc;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
    {
        var permClaims = context.User.FindAll("perm_codes").Select(c => c.Value).ToList();
        if (_permCalc.IsWildcard(permClaims))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
