namespace OneCup.Application.Dtos.System;

/// <summary>新建物料请求。Code 不在此处——由系统在事务内经编号引擎生成;IsActive 默认 true。</summary>
public record CreateMaterialRequest
{
    public string Name { get; init; } = string.Empty;
    public string Spec { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
    /// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
    public string? CategoryCode { get; init; }
}

/// <summary>更新物料请求。全可空,部分更新;Code 不可改(不在此处),IsActive 走状态接口。</summary>
public record UpdateMaterialRequest
{
    public string? Name { get; init; }
    public string? Spec { get; init; }
    public string? Category { get; init; }
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateMaterialStatusRequest
{
    public bool IsActive { get; init; }
}

public class MaterialDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Spec { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? UnitId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
