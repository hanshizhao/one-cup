using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ═══════════════════════════════════════════
// 设备类型（EquipmentType）
// ═══════════════════════════════════════════

/// <summary>设备类型列表项。</summary>
public class EquipmentTypeListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public int TemplateCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>设备类型详情（含参数定义 + 模板摘要）。</summary>
public class EquipmentTypeDto : EquipmentTypeListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<EquipmentTypeParameterDto> Parameters { get; set; } = new();
    public List<EquipmentTemplateSummaryDto> Templates { get; set; } = new();
}

/// <summary>参数定义（详情展示）。</summary>
public class EquipmentTypeParameterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public Guid? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int? Precision { get; set; }
    public List<string>? Options { get; set; }
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>模板摘要（类型详情内嵌展示）。</summary>
public class EquipmentTemplateSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? Status { get; set; }   // valid/invalid/orphan（读时校验）
    public int SortOrder { get; set; }
}

/// <summary>新建设备类型请求。参数定义整表提交。</summary>
public class CreateEquipmentTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    /// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
    public string? CategoryCode { get; set; }
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

/// <summary>编辑设备类型请求。</summary>
public class UpdateEquipmentTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

/// <summary>
/// 参数定义提交项。Id=null=新增，Id 有值=更新；未出现在数组里的存量 Id=删除。
/// </summary>
public class ParameterDefinitionDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public Guid? UnitId { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int? Precision { get; set; }
    public List<string>? Options { get; set; }
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

// ═══════════════════════════════════════════
// 运行模板（EquipmentTemplate）
// ═══════════════════════════════════════════

/// <summary>模板列表项。</summary>
public class EquipmentTemplateListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? Status { get; set; }   // valid/invalid/orphan
    public string? StatusMessage { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>模板详情（含参数值 + 校验状态）。</summary>
public class EquipmentTemplateDto : EquipmentTemplateListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<EquipmentTemplateValueDto> Values { get; set; } = new();
}

/// <summary>模板参数值（详情返回，带校验状态）。</summary>
public class EquipmentTemplateValueDto
{
    public Guid ParameterId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public string? UnitSymbol { get; set; }
    public string? Value { get; set; }
    /// <summary>valid / invalid / orphan</summary>
    public string Status { get; set; } = "valid";
    public string? StatusMessage { get; set; }
}

/// <summary>新建模板请求。值整表提交。</summary>
public class CreateEquipmentTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<TemplateValueDto> Values { get; set; } = new();
}

/// <summary>编辑模板请求。</summary>
public class UpdateEquipmentTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<TemplateValueDto> Values { get; set; } = new();
}

/// <summary>模板值提交项。</summary>
public class TemplateValueDto
{
    public Guid ParameterId { get; set; }
    public string? Value { get; set; }
}

// ═══════════════════════════════════════════
// 设备实例（Equipment）
// ═══════════════════════════════════════════

/// <summary>设备列表项（扁平投影，不含参数/模板）。</summary>
public class EquipmentListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>设备详情。</summary>
public class EquipmentDto : EquipmentListItemDto
{
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建设备请求。Code 不在此处——由系统在事务内生成。</summary>
public class CreateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    /// <summary>可选；编号规则要求分类码时必填。</summary>
    public string? CategoryCode { get; set; }
}

/// <summary>编辑设备请求。</summary>
public class UpdateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
