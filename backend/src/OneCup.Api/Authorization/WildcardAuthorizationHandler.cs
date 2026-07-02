using Microsoft.AspNetCore.Authorization;

namespace OneCup.Api.Authorization;

/// <summary>
/// admin 角色的 perm_codes 包含通配 "*"，直接放行所有策略。
/// </summary>
public class WildcardAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
    {
        if (context.User.HasClaim("perm_codes", "*"))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
