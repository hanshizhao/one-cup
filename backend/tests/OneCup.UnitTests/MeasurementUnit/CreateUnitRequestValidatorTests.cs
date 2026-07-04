using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.MeasurementUnit;

public class CreateUnitRequestValidatorTests
{
    private readonly CreateUnitRequestValidator _validator = new();

    private static CreateUnitRequest Valid() => new()
    {
        Code = "kg", NameZh = "千克", NameEn = "Kilogram",
        Symbol = "kg", Category = "WEIGHT", IsBase = false,
        Factor = 0.001m, Precision = 2, SortOrder = 1,
    };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Empty_code_fails()
    {
        var req = Valid() with { Code = "" };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Uppercase_code_fails()
    {
        var req = Valid() with { Code = "KG" };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Empty_category_fails()
    {
        var req = Valid() with { Category = "" };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Lowercase_category_fails()
    {
        var req = Valid() with { Category = "weight" };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Zero_factor_fails()
    {
        var req = Valid() with { Factor = 0m };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Precision_out_of_range_fails()
    {
        var req = Valid() with { Precision = 7 };
        Assert.False(_validator.Validate(req).IsValid);
    }
}
