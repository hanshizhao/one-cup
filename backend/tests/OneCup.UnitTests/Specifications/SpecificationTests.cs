using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.Specifications;

public class SpecificationTests
{
    [Fact]
    public void Empty_specification_has_no_criteria_or_paging()
    {
        ISpecification<User> spec = new TestSpec();
        Assert.Null(spec.Criteria);
        Assert.Empty(spec.Includes);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    [Fact]
    public void Apply_criteria_sets_criteria()
    {
        var spec = new TestSpec();
        spec.ApplyCriteria(u => u.Username == "admin");
        Assert.NotNull(spec.Criteria);
    }

    [Fact]
    public void ApplyInclude_adds_string_path()
    {
        var spec = new TestSpec();
        spec.ApplyInclude("Roles");
        spec.ApplyInclude("Roles.Permissions");
        Assert.Equal(2, spec.Includes.Count);
        Assert.Contains("Roles.Permissions", spec.Includes);
    }

    [Fact]
    public void ApplyPaging_sets_skip_take()
    {
        var spec = new TestSpec();
        spec.ApplyPaging(page: 2, pageSize: 10);
        Assert.Equal(10, spec.Skip);
        Assert.Equal(10, spec.Take);
    }

    // 测试用具体类(基类是抽象的)
    internal class TestSpec : Specification<User> { }
}
