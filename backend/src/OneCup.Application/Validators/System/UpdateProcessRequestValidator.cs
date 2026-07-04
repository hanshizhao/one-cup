using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑工序请求校验（字段约束同 Create）。</summary>
public class UpdateProcessRequestValidator : AbstractValidator<UpdateProcessRequest>
{
    public UpdateProcessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
