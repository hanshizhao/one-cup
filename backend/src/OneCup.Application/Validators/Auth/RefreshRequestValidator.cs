using FluentValidation;
using OneCup.Application.Dtos.Auth;

namespace OneCup.Application.Validators.Auth;

/// <summary>刷新令牌请求校验:RefreshToken 必填。</summary>
public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
