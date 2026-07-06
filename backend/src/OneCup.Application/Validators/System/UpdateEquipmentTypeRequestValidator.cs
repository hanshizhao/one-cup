using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑设备类型请求校验。</summary>
public class UpdateEquipmentTypeRequestValidator : AbstractValidator<UpdateEquipmentTypeRequest>
{
    public UpdateEquipmentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Parameters).SetValidator(new ParameterDefinitionDtoValidator());
    }
}
