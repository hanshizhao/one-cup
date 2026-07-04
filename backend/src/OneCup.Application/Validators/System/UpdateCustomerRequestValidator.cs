using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑客户请求校验（字段约束同 Create）。</summary>
public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    private static readonly string PhonePattern = @"^[\d+\-()\s]+$";

    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ShortName));
        RuleFor(x => x.ContactPerson).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ContactPerson));
        RuleFor(x => x.ContactPhone)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrEmpty(x.ContactPhone));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
