namespace OneCup.Application.Common;

/// <summary>
/// 分页查询结果,用于列表类 API 响应。
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
