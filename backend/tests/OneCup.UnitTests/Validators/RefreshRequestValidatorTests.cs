using OneCup.Application.Dtos.Auth;
using OneCup.Application.Validators.Auth;

namespace OneCup.UnitTests.Validators;

public class RefreshRequestValidatorTests
{
    private readonly RefreshRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(new RefreshRequest { RefreshToken = "some-token" }).IsValid);
    }

    [Fact]
    public void Empty_token_fails()
    {
        Assert.False(_validator.Validate(new RefreshRequest { RefreshToken = "" }).IsValid);
    }
}
