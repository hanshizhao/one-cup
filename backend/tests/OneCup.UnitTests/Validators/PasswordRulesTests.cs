using OneCup.Application.Validators;

namespace OneCup.UnitTests.Validators;

public class PasswordRulesTests
{
    [Theory]
    [InlineData("Admin@123", true)]      // 字母+数字+符号(中等要求字母+数字即可,符号额外)
    [InlineData("password1", true)]       // 字母+数字,刚好满足
    [InlineData("12345678", false)]       // 纯数字,无字母
    [InlineData("abcdefgh", false)]       // 纯字母,无数字
    [InlineData("Ab1", false)]            // 太短
    [InlineData("", false)]
    public void BeMediumStrength_validates(string pwd, bool expected)
    {
        Assert.Equal(expected, PasswordRules.BeMediumStrength(pwd));
    }
}
