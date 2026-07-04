namespace OneCup.Application.Dtos.System;

/// <summary>工序列表项（表格行）。</summary>
public class ProcessListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>工序详情（Drawer 只读）。</summary>
public class ProcessDto : ProcessListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建工序请求。Code 不在此处——由系统在事务内生成。</summary>
public record CreateProcessRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public int SortOrder { get; init; } = 0;
    public string? Remark { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>编辑工序请求（字段同 Create，独立类以便 FluentValidation 区分规则）。</summary>
public record UpdateProcessRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public int SortOrder { get; init; } = 0;
    public string? Remark { get; init; }
    public bool IsActive { get; init; } = true;
}
