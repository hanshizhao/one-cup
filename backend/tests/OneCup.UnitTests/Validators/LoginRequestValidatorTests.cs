using OneCup.Application.Dtos.Auth;
using OneCup.Application.Validators.Auth;

namespace OneCup.UnitTests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    private static LoginRequest Valid() => new() { Username = "alice", Password = "Password1" };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Empty_username_fails()
    {
        var req = Valid(); req.Username = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Empty_password_fails()
    {
        var req = Valid(); req.Password = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Username_over_50_fails()
    {
        var req = Valid(); req.Username = new string('a', 51);
        Assert.False(_validator.Validate(req).IsValid);
    }
}
