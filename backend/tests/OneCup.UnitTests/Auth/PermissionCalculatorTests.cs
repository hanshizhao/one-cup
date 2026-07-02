using OneCup.Application.Services;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.Auth;

public class PermissionCalculatorTests
{
    private readonly PermissionCalculator _calc = new();

    [Fact]
    public void IsWildcard_true_when_contains_star()
    {
        Assert.True(_calc.IsWildcard(new[] { "fabric:read", "*" }));
    }

    [Fact]
    public void IsWildcard_false_without_star()
    {
        Assert.False(_calc.IsWildcard(new[] { "fabric:read" }));
    }

    [Fact]
    public void GetEffective_admin_role_returns_wildcard()
    {
        var adminRole = new Role { Code = "admin", Permissions = new List<Permission>() };
        var user = new User { Roles = new List<Role> { adminRole } };
        var result = _calc.GetEffective(user);
        Assert.Equal(new[] { "*" }, result);
    }

    [Fact]
    public void GetEffective_non_admin_aggregates_and_dedupes()
    {
        var dev = new Role
        {
            Code = "developer",
            Permissions = new List<Permission>
            {
                new() { Code = "fabric:read" },
                new() { Code = "material:read" },
            }
        };
        var other = new Role
        {
            Code = "viewer",
            Permissions = new List<Permission>
            {
                new() { Code = "fabric:read" },          // 重复
                new() { Code = "system:user:manage" },
            }
        };
        var user = new User { Roles = new List<Role> { dev, other } };
        var result = _calc.GetEffective(user);
        Assert.Equal(new[] { "fabric:read", "material:read", "system:user:manage" }, result.OrderBy(x => x));
    }
}
