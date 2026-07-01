namespace OneCup.Domain.Exceptions;

/// <summary>
/// 领域层抛出的业务规则异常。
/// API 层会将其映射为 HTTP 400 响应。
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
