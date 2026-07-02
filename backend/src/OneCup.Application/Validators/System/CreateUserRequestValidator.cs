using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建用户请求校验。</summary>
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.DisplayName).NotEmpty().Length(1, 50);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password).NotEmpty().Must(PasswordRules.BeMediumStrength)
            .WithMessage("密码至少8位且含字母和数字");
        RuleFor(x => x.RoleIds).NotEmpty();
    }
}
