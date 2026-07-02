using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class UpdateRoleRequestValidatorTests
{
    private readonly UpdateRoleRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(new UpdateRoleRequest { Name = "测试" }).IsValid);
    }

    [Fact]
    public void Empty_name_fails()
    {
        Assert.False(_validator.Validate(new UpdateRoleRequest { Name = "" }).IsValid);
    }

    [Fact]
    public void Name_over_50_fails()
    {
        Assert.False(_validator.Validate(new UpdateRoleRequest { Name = new string('a', 51) }).IsValid);
    }
}
