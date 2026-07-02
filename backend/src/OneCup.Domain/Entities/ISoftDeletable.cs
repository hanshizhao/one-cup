namespace OneCup.Domain.Entities;

/// <summary>软删除标记接口。实现的实体加 IsDeleted 字段 + EF 全局查询过滤器。</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
