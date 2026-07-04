namespace OneCup.Application.Dtos.System;

/// <summary>新建颜色请求。Code 不在此处——由系统在事务内经编号引擎生成。</summary>
public record CreateColorRequest
{
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Hex { get; init; } = string.Empty;
    public string ColorFamily { get; init; } = string.Empty;
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
}

public record UpdateColorRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Hex { get; init; }
    public string? ColorFamily { get; init; }
    public string? Remark { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateColorStatusRequest
{
    public bool IsActive { get; init; }
}

public class ColorDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public string ColorFamily { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
