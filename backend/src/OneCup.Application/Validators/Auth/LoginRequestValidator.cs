using FluentValidation;
using OneCup.Application.Dtos.Auth;

namespace OneCup.Application.Validators.Auth;

/// <summary>登录请求校验:Username 必填且 ≤50,Password 必填。</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty();
    }
}
