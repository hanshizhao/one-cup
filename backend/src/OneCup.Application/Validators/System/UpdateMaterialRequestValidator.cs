using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑物料请求校验(字段约束同 Create,必填字段在 null 时跳过以支持部分更新语义)。</summary>
public class UpdateMaterialRequestValidator : AbstractValidator<UpdateMaterialRequest>
{
    public UpdateMaterialRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Spec).NotEmpty().MaximumLength(100).When(x => x.Spec is not null);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(32).When(x => x.Category is not null);
        RuleFor(x => x.Remark).MaximumLength(256).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder is not null);
    }
}
