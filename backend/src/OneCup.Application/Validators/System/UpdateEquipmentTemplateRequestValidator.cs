using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑模板请求校验。</summary>
public class UpdateEquipmentTemplateRequestValidator : AbstractValidator<UpdateEquipmentTemplateRequest>
{
    public UpdateEquipmentTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
