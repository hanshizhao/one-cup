using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.Specifications;

public class SpecificationTests
{
    [Fact]
    public void Empty_specification_has_no_criteria_or_paging()
    {
        var spec = (ISpecification<User>)new EmptyUserSpec();
        Assert.Null(spec.Criteria);
        Assert.Empty(spec.Includes);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    [Fact]
    public void Criteria_applied_in_constructor_sets_criteria()
    {
        var spec = (ISpecification<User>)new UserByAdminSpec();
        Assert.NotNull(spec.Criteria);
    }

    [Fact]
    public void Includes_applied_in_constructor_add_string_paths()
    {
        var spec = (ISpecification<User>)new UserWithRolesSpec();
        Assert.Equal(2, spec.Includes.Count);
        Assert.Contains("Roles.Permissions", spec.Includes);
    }

    [Fact]
    public void Paging_applied_in_constructor_sets_skip_take()
    {
        var spec = (ISpecification<User>)new UserPagedSpec(2, 10);
        Assert.Equal(10, spec.Skip);
        Assert.Equal(10, spec.Take);
    }

    // Realistic concrete specs that apply in their constructors
    internal class EmptyUserSpec : Specification<User> { }
    internal class UserByAdminSpec : Specification<User>
    {
        public UserByAdminSpec() { ApplyCriteria(u => u.Username == "admin"); }
    }
    internal class UserWithRolesSpec : Specification<User>
    {
        public UserWithRolesSpec() { ApplyInclude("Roles"); ApplyInclude("Roles.Permissions"); }
    }
    internal class UserPagedSpec : Specification<User>
    {
        public UserPagedSpec(int page, int pageSize) { ApplyPaging(page, pageSize); }
    }
}
