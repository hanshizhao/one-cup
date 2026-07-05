using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Process;

public class ProcessValidatorTests
{
    private static CreateProcessRequest ValidCreate() => new()
    {
        Name = "染色",
        Category = "前处理",
        SortOrder = 1,
    };

    [Fact]
    public void Create_EmptyName_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(ValidCreate() with { Name = "" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_NameTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Name = new string('x', 51) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_CategoryTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Category = new string('y', 51) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_RemarkTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Remark = new string('z', 501) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_NullCategoryAndRemark_Valid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Category = null, Remark = null });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_EmptyName_Invalid()
    {
        var result = new UpdateProcessRequestValidator().Validate(
            new UpdateProcessRequest { Name = "" });
        Assert.False(result.IsValid);
    }
}
