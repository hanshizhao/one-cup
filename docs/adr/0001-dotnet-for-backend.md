# ADR-0001: .NET 10 as backend framework

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

印染厂面料开发管理系统,前后端分离的企业内部应用,用户规模几十到几百人。需要处理复杂的业务领域(面料开发流程 + 多种工艺归档 + 产品管理),数据一致性要求高。后端技术栈的选择需要兼顾开发效率、团队熟悉度、长期维护性和部署成本。

## Decision

采用 .NET 10 (LTS) 作为后端框架,配合 EF Core 做 ORM。

## Alternatives Considered

### Alternative 1: Java (Spring Boot)
- **Pros**: 生态成熟,招人容易,企业内部管理系统经验丰富
- **Cons**: 启动较重,内存占用高
- **Why not**: 团队更倾向 .NET 生态,开发体验更现代

### Alternative 2: Node.js (NestJS)
- **Pros**: 前后端同语言,可共享类型
- **Cons**: 强类型业务逻辑处理不如 C# 顺手
- **Why not**: 复杂工艺归档领域的领域建模,C# 更合适

### Alternative 3: Python (FastAPI/Django)
- **Pros**: 开发效率高,语法简洁
- **Cons**: 类型安全不如 C#,企业系统生态相对弱
- **Why not**: 团队偏好 .NET

### Alternative 4: Go (Gin/Echo)
- **Pros**: 高并发,低资源
- **Cons**: 企业管理系统生态偏薄,领域建模工具链不丰富
- **Why not**: 不匹配业务场景

## Consequences

### Positive
- .NET 10 LTS 支持到 2028 年,长期项目无升级压力
- EF Core + LINQ 强类型查询,编译期挡住大量错误,适合字段多的工艺业务
- C# 类型系统强,领域建模(Domain 层)清晰
- 跨平台,可部署在 Linux + Docker

### Negative
- .NET 在某些前端工具链生态(如 OpenAPI 代码生成)不如 Node.js 顺滑
- 团队需保持对 .NET 版本更新的关注

### Risks
- .NET 10 是较新版本,部分第三方库可能尚未完全适配 — 通过选择成熟库(EF Core、Npgsql)缓解
