namespace OneCup.Application.Dtos.System;

// ── 业务类型 ──

public record CreateTargetTypeRequest
{
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record UpdateTargetTypeRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateDictStatusRequest
{
    public bool IsActive { get; init; }
}

public class TargetTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ── 分类 ──

public record CreateCategoryRequest
{
    public string TargetTypeCode { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record UpdateCategoryRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public int? SortOrder { get; init; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string TargetTypeCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
