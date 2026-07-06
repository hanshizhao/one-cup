using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建模板请求校验。参数值合法性由服务层强校验（依赖参数定义，跨实体）。</summary>
public class CreateEquipmentTemplateRequestValidator : AbstractValidator<CreateEquipmentTemplateRequest>
{
    public CreateEquipmentTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
