# ADR-0002: PostgreSQL as primary database

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

面料开发管理系统的数据特征:强关系型(面料 ↔ 多种工艺 ↔ 设备 ↔ 原料的多表关联)、状态流转(开发中 → 待审核 → 已归档)、高一致性要求(归档数据不能错不能丢)。数据库选型必须匹配这些特征。

## Decision

采用 PostgreSQL 作为主数据库,通过 EF Core (Npgsql 驱动) 访问。

## Alternatives Considered

### Alternative 1: Microsoft SQL Server
- **Pros**: Windows 生态和企业内部系统使用广泛,DBA 熟悉
- **Cons**: 授权成本高,Linux 部署体验不如 PG 原生
- **Why not**: PostgreSQL 在 .NET 生态支持同样优秀且免费开源

### Alternative 2: MySQL
- **Pros**: 国内企业项目常见,运维成熟
- **Cons**: 与 .NET 的现代化集成不如 PostgreSQL 自然,功能集(PG 的 JSONB、窗口函数等)不如 PG
- **Why not**: PostgreSQL 功能更全面,与 EF Core 配合更好

## Consequences

### Positive
- 开源免费,无授权成本
- 功能全面:强 JSON 支持(未来存灵活的工艺参数可能用到)、丰富的索引类型
- Npgsql + EF Core 驱动成熟,社区活跃

### Negative
- 如果未来工厂有 DBA 团队更熟悉 SQL Server,可能需要培训适应

### Risks
- 无明显风险;PostgreSQL 是 .NET 社区公认的最佳搭配之一
