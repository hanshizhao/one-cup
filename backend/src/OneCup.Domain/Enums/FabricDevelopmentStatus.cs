namespace OneCup.Domain.Enums;

/// <summary>
/// 面料开发单的状态流转。
/// 归档 (Archived) 是关键流转点:状态变更后数据锁定,并生成产品记录。
/// </summary>
public enum FabricDevelopmentStatus
{
    /// <summary>开发中 — 可编辑工艺数据</summary>
    InDevelopment = 0,

    /// <summary>待审核 — 提交后等待归档审批</summary>
    PendingReview = 1,

    /// <summary>已归档 — 数据锁定只读,已生成产品记录</summary>
    Archived = 2,
}
