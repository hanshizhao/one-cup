using System.Linq.Expressions;

namespace OneCup.Application.Specifications;

/// <summary>
/// 规范基类:子类在构造函数中用 Apply* 方法链式构造查询。
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public List<string> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }

    IReadOnlyList<string> ISpecification<T>.Includes => Includes;

    public void ApplyCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;
    public void ApplyInclude(string navigationPath) => Includes.Add(navigationPath);
    public void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;
    public void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) =>
        OrderByDescending = orderByDescending;
    public void ApplyPaging(int page, int pageSize)
    {
        Skip = (page - 1) * pageSize;
        Take = pageSize;
    }
}
