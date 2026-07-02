using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _validator = new();

    private static UpdateUserRequest Valid() => new()
    {
        DisplayName = "Alice", IsActive = true, RoleIds = [Guid.NewGuid()]
    };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Empty_displayname_fails()
    {
        var req = Valid(); req.DisplayName = "";
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
}
