using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建角色请求校验:Code 仅允许小写字母/数字/下划线/冒号。</summary>
public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Code).NotEmpty()
            .Matches("^[a-z][a-z0-9_:]*$")
            .WithMessage("角色编码只能含小写字母/数字/下划线/冒号")
            .MaximumLength(50);
    }
}
