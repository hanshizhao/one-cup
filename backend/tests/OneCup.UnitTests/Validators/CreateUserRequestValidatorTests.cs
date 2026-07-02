using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() => new()
    {
        Username = "alice", DisplayName = "Alice", Password = "Password1", RoleIds = [Guid.NewGuid()]
    };

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Empty_username_fails()
    {
        var req = Valid(); req.Username = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Short_username_under_3_fails()
    {
        var req = Valid(); req.Username = "ab";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Weak_password_no_digit_fails()
    {
        var req = Valid(); req.Password = "password";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Empty_roleIds_fails()
    {
        var req = Valid(); req.RoleIds = [];
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var req = Valid(); req.Email = "not-an-email";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Valid_email_passes()
    {
        var req = Valid(); req.Email = "alice@example.com";
        Assert.True(_validator.Validate(req).IsValid);
    }
}
