namespace OneCup.Application.Interfaces;

/// <summary>
/// 编码生成服务。业务对象落库事务内调用 GenerateAsync。
/// </summary>
public interface INumberingService
{
    /// <summary>
    /// 生成编码。在调用方事务内执行行锁取号，调用方负责提交事务。
    /// </summary>
    /// <param name="targetType">业务对象类型，如 NumberTargetTypes.Fabric</param>
    /// <param name="categoryCode">品类码，规则开启分类码段时必填</param>
    Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);

    /// <summary>
    /// 预览下一个编码（只读，不消耗计数，仅供参考）。
    /// </summary>
    Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);
}
