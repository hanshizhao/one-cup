using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class CreateRoleRequestValidatorTests
{
    private readonly CreateRoleRequestValidator _validator = new();

    [Fact]
    public void Valid_code_passes() => Assert.True(_validator.Validate(new CreateRoleRequest { Name = "测试", Code = "test_role" }).IsValid);

    [Theory]
    [InlineData("Admin")]      // 大写
    [InlineData("test role")]  // 空格
    [InlineData("")]           // 空
    public void Invalid_code_fails(string code) =>
        Assert.False(_validator.Validate(new CreateRoleRequest { Name = "测试", Code = code }).IsValid);
}
