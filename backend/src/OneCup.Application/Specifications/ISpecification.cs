using System.Linq.Expressions;

namespace OneCup.Application.Specifications;

/// <summary>
/// 查询规范:把查询条件、关联加载、排序、分页封装为一个对象,
/// 由 Infrastructure 的 Repository 翻译为 EF Core LINQ。
/// Application 层通过它表达查询意图,不直接依赖 EF Core。
/// </summary>
public interface ISpecification<T>
{
    /// <summary>过滤条件(可空表示无条件)。</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>需要 Include 的导航属性(字符串路径,支持点分多级如 "Roles.Permissions")。</summary>
    IReadOnlyList<string> Includes { get; }

    /// <summary>升序排序键(可空)。</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>降序排序键(可空)。</summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>跳过条数(分页用,可空)。</summary>
    int? Skip { get; }

    /// <summary>取条数(分页用,可空)。</summary>
    int? Take { get; }
}
