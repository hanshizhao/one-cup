# ADR-0004: Clean Architecture layering

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

面料开发管理系统的业务领域复杂:面料开发流程 + 织造/流程/染色三种工艺归档 + 多种主数据(设备、原料、颜色等)。后端代码结构需要支撑这种复杂度,同时保持可测试性和可维护性。单体部署不等于代码要糊在一起。

## Decision

采用 Clean Architecture 四层结构:Api / Application / Domain / Infrastructure,依赖方向严格单向。

```
Api → Application → Domain
Api → Infrastructure → Application → Domain
Domain 零依赖(纯 C#,不引用 EF Core)
```

## Alternatives Considered

### Alternative 1: 精简单体(单项目 + 文件夹分层)
- **Pros**: 简单直接,无多项目认知负担
- **Cons**: 业务复杂后容易出现"大杂烩",领域逻辑与数据访问混杂,单元测试难隔离
- **Why not**: 明确说过"细节较多",工艺归档字段多,糊在一起会越做越臃肿

### Alternative 2: Monorepo + 前后端共享类型(C# DTO → TS 自动生成)
- **Pros**: 类型契约零漂移,改后端字段前端立即知道
- **Cons**: 需引入 OpenAPI Generator / NSwag 等代码生成工具,增加构建复杂度
- **Why not**: 当前阶段过度工程,跑起来后需要再加也不迟

## Consequences

### Positive
- Domain 层零依赖,工艺规则、面料状态机等可脱离数据库单元测试
- 关注点分离清晰,改织造的代码不会碰到染色的
- 未来要拆服务时,分层边界就是天然的拆分线

### Negative
- 初期有样板代码(接口 + 实现 + DI 注册)
- 层间数据映射(Entity → DTO)有重复

### Risks
- 过度分层风险 — 通过只对真正有领域逻辑的模块严格分层来缓解,简单 CRUD 模块可适当简化
