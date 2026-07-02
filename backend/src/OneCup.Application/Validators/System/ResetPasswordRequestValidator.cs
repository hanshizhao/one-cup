using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>重置密码请求校验:新密码需达中等强度。</summary>
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().Must(PasswordRules.BeMediumStrength)
            .WithMessage("密码至少8位且含字母和数字");
    }
}
