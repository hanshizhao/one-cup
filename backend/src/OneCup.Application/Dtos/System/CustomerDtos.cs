namespace OneCup.Application.Dtos.System;

/// <summary>客户列表项（表格行）。</summary>
public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>客户详情（Drawer 只读）。</summary>
public class CustomerDto : CustomerListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建客户请求。Code 不在此处——由系统在事务内生成。</summary>
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>编辑客户请求（字段同 Create，独立类以便 FluentValidation 区分规则）。</summary>
public class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}
