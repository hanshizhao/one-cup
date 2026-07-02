using FluentValidation;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Common;

/// <summary>FluentValidation 手动校验扩展:失败抛 DomainException(→400)。</summary>
public static class ValidationExtensions
{
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        }
    }
}
