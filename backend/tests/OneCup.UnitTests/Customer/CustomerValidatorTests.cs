using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Customer;

public class CustomerValidatorTests
{
    private readonly CreateCustomerRequestValidator _create = new();
    private readonly UpdateCustomerRequestValidator _update = new();

    private static CreateCustomerRequest ValidCreate() => new()
    {
        Name = "深圳市测试服饰有限公司",
        ShortName = "测试服饰",
        ContactPerson = "张三",
        ContactPhone = "0755-12345678",
        Remark = "VIP客户",
        IsActive = true,
    };

    [Fact]
    public void Create_Valid_passes() =>
        Assert.True(_create.Validate(ValidCreate()).IsValid);

    [Fact]
    public void Create_EmptyName_fails()
    {
        var req = ValidCreate(); req.Name = "";
        Assert.False(_create.Validate(req).IsValid);
    }

    [Theory]
    // 112 个字符（前缀 11 + X×101），超过 MaximumLength(100) 上限
    [InlineData("深圳市XX服饰有限公司XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX")]
    public void Create_NameOver100_fails(string name)
    {
        Assert.True(name.Length > 100); // 防御：确保测试数据确实超长
        var req = ValidCreate(); req.Name = name;
        Assert.False(_create.Validate(req).IsValid);
    }

    [Fact]
    public void Create_PhoneAcceptsLandline()
    {
        var req = ValidCreate(); req.ContactPhone = "0755-12345678-888";
        Assert.True(_create.Validate(req).IsValid);
    }

    [Fact]
    public void Create_PhoneAcceptsMobile()
    {
        var req = ValidCreate(); req.ContactPhone = "13800138000";
        Assert.True(_create.Validate(req).IsValid);
    }

    [Fact]
    public void Create_PhoneRejectsLetters()
    {
        var req = ValidCreate(); req.ContactPhone = "abcd1234";
        Assert.False(_create.Validate(req).IsValid);
    }

    [Fact]
    public void Update_Valid_passes() =>
        Assert.True(_update.Validate(new UpdateCustomerRequest
        {
            Name = "测试",
            IsActive = false,
        }).IsValid);
}
