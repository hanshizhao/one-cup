using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建设备请求校验。</summary>
public class CreateEquipmentRequestValidator : AbstractValidator<CreateEquipmentRequest>
{
    public CreateEquipmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.Specification).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Specification));
        RuleFor(x => x.Supplier).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Supplier));
        RuleFor(x => x.Location).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Location));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
