using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>更新用户请求校验:不含 Username(不可改)。</summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().Length(1, 50);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.RoleIds).NotEmpty();
    }
}
