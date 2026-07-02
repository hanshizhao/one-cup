using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(new ResetPasswordRequest { NewPassword = "Password1" }).IsValid);
    }

    [Fact]
    public void Empty_password_fails()
    {
        Assert.False(_validator.Validate(new ResetPasswordRequest { NewPassword = "" }).IsValid);
    }

    [Fact]
    public void Weak_password_no_digit_fails()
    {
        Assert.False(_validator.Validate(new ResetPasswordRequest { NewPassword = "password" }).IsValid);
    }
}
