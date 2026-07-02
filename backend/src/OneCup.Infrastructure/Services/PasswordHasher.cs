using OneCup.Application.Interfaces;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// BCrypt 密码哈希实现。
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
