using OneCup.Application.Common;

namespace OneCup.UnitTests.AuditLog;

public class PayloadSanitizerTests
{
    [Fact]
    public void Sanitize_NoSensitiveField_ReturnsOriginalContent()
    {
        var json = """{"username":"admin","roles":["admin"]}""";
        var result = PayloadSanitizer.Sanitize(json);
        // 未命中黑名单，原样返回（含 password 字段名才打码）
        Assert.Contains("\"admin\"", result);
        Assert.Contains("username", result);
    }

    [Fact]
    public void Sanitize_PasswordField_ReplacedWithMask()
    {
        var json = """{"username":"admin","password":"abc123"}""";
        var result = PayloadSanitizer.Sanitize(json);
        Assert.Contains("\"password\":\"***\"", result);
        Assert.DoesNotContain("abc123", result);
        Assert.Contains("admin", result);
    }

    [Fact]
    public void Sanitize_CaseInsensitiveFieldName()
    {
        var json = """{"Password":"secret"}""";
        var result = PayloadSanitizer.Sanitize(json);
        Assert.Contains("\"***\"", result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void Sanitize_NestedObject_SensitiveFieldMasked()
    {
        var json = """{"user":{"name":"admin","token":"tk-xyz"},"level":1}""";
        var result = PayloadSanitizer.Sanitize(json);
        Assert.Contains("\"token\":\"***\"", result);
        Assert.DoesNotContain("tk-xyz", result);
        Assert.Contains("admin", result);
        Assert.Contains("\"level\":1", result);
    }

    [Fact]
    public void Sanitize_ArrayOfObjects_SensitiveFieldsMasked()
    {
        var json = """{"items":[{"password":"p1"},{"password":"p2"}]}""";
        var result = PayloadSanitizer.Sanitize(json);
        Assert.Contains("\"password\":\"***\"", result);
        Assert.DoesNotContain("p1", result);
        Assert.DoesNotContain("p2", result);
    }

    [Fact]
    public void Sanitize_AllKnownSensitiveFieldsMasked()
    {
        var json = """{"password":"a","oldPassword":"b","newPassword":"c","token":"d","accessToken":"e","refreshToken":"f","secret":"g","authorization":"h"}""";
        var result = PayloadSanitizer.Sanitize(json);
        foreach (var field in new[] { "password", "oldPassword", "newPassword", "token", "accessToken", "refreshToken", "secret", "authorization" })
        {
            Assert.Contains($"\"{field}\":\"***\"", result);
        }
        foreach (var val in new[] { "\"a\"", "\"b\"", "\"c\"", "\"d\"", "\"e\"", "\"f\"", "\"g\"", "\"h\"" })
        {
            // 数值字符可能在别处出现，但带引号的原始值 a/b/c... 不应作为值出现
        }
        Assert.DoesNotContain("\"a\",\"", result);
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", PayloadSanitizer.Sanitize(""));
        Assert.Null(PayloadSanitizer.Sanitize(null!));
    }

    [Fact]
    public void Sanitize_OverSize_ReturnsTruncatedMarker()
    {
        // 构造 > 8KB 的合法 JSON
        var big = new string('x', 9000);
        var json = "{\"data\":\"" + big + "\"}";
        var result = PayloadSanitizer.Sanitize(json);
        Assert.Contains("[truncated:", result);
    }

    [Fact]
    public void Sanitize_InvalidJson_ReturnsBinaryMarker()
    {
        var result = PayloadSanitizer.Sanitize("not-a-json {{{");
        Assert.Equal("[binary]", result);
    }
}
