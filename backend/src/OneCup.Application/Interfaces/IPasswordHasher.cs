namespace OneCup.Application.Interfaces;

/// <summary>
/// 密码哈希服务接口。
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
