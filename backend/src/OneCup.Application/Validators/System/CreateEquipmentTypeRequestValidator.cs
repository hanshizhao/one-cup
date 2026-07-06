using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建设备类型请求校验。</summary>
public class CreateEquipmentTypeRequestValidator : AbstractValidator<CreateEquipmentTypeRequest>
{
    public CreateEquipmentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Parameters).SetValidator(new ParameterDefinitionDtoValidator());
    }
}

/// <summary>参数定义项校验（Create/Update 共用）。</summary>
public class ParameterDefinitionDtoValidator : AbstractValidator<ParameterDefinitionDto>
{
    public ParameterDefinitionDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MinValue).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.MinValue));
        RuleFor(x => x.MaxValue).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.MaxValue));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
