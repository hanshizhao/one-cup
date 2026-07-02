using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;

namespace OneCup.Application.Services;

public class PermissionCalculator : IPermissionCalculator
{
    private const string AdminRoleCode = "admin";
    private const string Wildcard = "*";

    public bool IsWildcard(IReadOnlyCollection<string> permCodes) => permCodes.Contains(Wildcard);

    public IReadOnlyList<string> GetEffective(User user)
    {
        if (user.Roles.Any(r => r.Code == AdminRoleCode))
        {
            return new List<string> { Wildcard };
        }

        return user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToList();
    }
}
