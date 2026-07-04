namespace OneCup.Application.Dtos.System;

public record CreateUnitRequest
{
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsBase { get; init; }
    public decimal Factor { get; init; } = 1m;
    public int Precision { get; init; } = 2;
    public int SortOrder { get; init; }
}

// code/category 不可改 → DTO 不含这两个字段
public record UpdateUnitRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Symbol { get; init; }
    public bool? IsBase { get; init; }
    public decimal? Factor { get; init; }
    public int? Precision { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateUnitStatusRequest
{
    public bool IsActive { get; init; }
}

public record ConvertUnitRequest
{
    public string FromCode { get; init; } = string.Empty;
    public string ToCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

public record ConvertUnitResult
{
    public decimal Quantity { get; init; }
    public string FromCode { get; init; } = string.Empty;
    public string ToCode { get; init; } = string.Empty;
    public int Precision { get; init; }
}

public class UnitDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsBase { get; set; }
    public decimal Factor { get; set; }
    public int Precision { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
