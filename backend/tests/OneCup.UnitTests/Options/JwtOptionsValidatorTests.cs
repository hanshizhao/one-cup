using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Application.Validators;

namespace OneCup.UnitTests.Options;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_null_or_empty_secret_fails()
    {
        var options = new JwtOptions { SecretKey = "", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_placeholder_secret_fails()
    {
        var options = new JwtOptions { SecretKey = JwtOptions.PlaceholderSecret, Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_short_secret_under_32_bytes_fails()
    {
        var options = new JwtOptions { SecretKey = "short-key-only-20-chars", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_secret_at_least_32_bytes_succeeds()
    {
        var options = new JwtOptions { SecretKey = "this-is-a-valid-secret-key-32+bytes!", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Succeeded);
    }
}
