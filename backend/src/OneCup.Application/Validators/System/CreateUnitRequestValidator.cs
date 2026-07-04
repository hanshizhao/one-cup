using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建计量单位请求校验（仅格式，业务规则在 Service 层）。</summary>
public class CreateUnitRequestValidator : AbstractValidator<CreateUnitRequest>
{
    public CreateUnitRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().Length(1, 32)
            .Matches("^[a-z][a-z0-9_]*$").WithMessage("Code 必须为小写英文标识符（字母开头，仅含小写字母/数字/下划线）");
        RuleFor(x => x.NameZh).NotEmpty().Length(1, 64);
        RuleFor(x => x.NameEn).NotEmpty().Length(1, 64);
        RuleFor(x => x.Symbol).NotEmpty().Length(1, 16);
        RuleFor(x => x.Category)
            .NotEmpty().Length(1, 32)
            .Matches("^[A-Z][A-Z0-9_]*$").WithMessage("Category 必须为大写枚举式（字母开头，仅含大写字母/数字/下划线）");
        RuleFor(x => x.Factor).GreaterThan(0m);
        RuleFor(x => x.Precision).InclusiveBetween(0, 6);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
