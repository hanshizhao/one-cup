namespace OneCup.Application.Validators;

/// <summary>密码强度共享规则。中等强度:长度≥8 且含字母且含数字。</summary>
public static class PasswordRules
{
    public static bool BeMediumStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        return hasLetter && hasDigit;
    }
}
