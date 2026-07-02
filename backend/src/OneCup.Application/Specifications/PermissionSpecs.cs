using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>查询全部权限(按 Code 排序)。</summary>
public class AllPermissionsSpec : Specification<Permission>
{
    public AllPermissionsSpec() { ApplyOrderBy(p => p.Code); }
}
