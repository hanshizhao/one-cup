namespace OneCup.Domain.Exceptions;

/// <summary>
/// 认证失败异常（凭证无效、令牌过期/吊销等）。
/// API 层会将其映射为 HTTP 401 响应。
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
