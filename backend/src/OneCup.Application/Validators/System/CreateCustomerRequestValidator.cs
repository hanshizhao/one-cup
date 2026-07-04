using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建客户请求校验。联系电话用宽松正则（允许座机/分机/手机）。</summary>
public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    // 数字、+、-、空格、括号；座机/手机/分机通用
    private static readonly string PhonePattern = @"^[\d+\-()\s]+$";

    public CreateCustomerRequestValidator()
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
