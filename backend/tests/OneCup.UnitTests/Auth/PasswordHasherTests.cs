using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsNonEmptyString_DifferentFromInput()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("mypassword");
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.NotEqual("mypassword", hash);
    }

    [Fact]
    public void Hash_GeneratesDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasher();
        var hash1 = hasher.Hash("samepass");
        var hash2 = hasher.Hash("samepass");
        Assert.NotEqual(hash1, hash2); // BCrypt salt 使每次结果不同
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correctpass");
        Assert.True(hasher.Verify("correctpass", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correctpass");
        Assert.False(hasher.Verify("wrongpass", hash));
    }
}
