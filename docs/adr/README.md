# Architecture Decision Records

本目录记录项目中所有架构决策的背景、理由和取舍。每个 ADR 回答"当时为什么这么选"。

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-dotnet-for-backend.md) | .NET 10 as backend framework | accepted | 2026-07-01 |
| [0002](0002-postgresql-as-database.md) | PostgreSQL as primary database | accepted | 2026-07-01 |
| [0003](0003-arco-design-pro-for-frontend.md) | Arco Design Pro as frontend scaffold | accepted | 2026-07-01 |
| [0004](0004-clean-architecture-layering.md) | Clean Architecture layering | accepted | 2026-07-01 |
| [0005](0005-monolith-over-microservices.md) | Monolith over microservices | accepted | 2026-07-01 |
| [0006](0006-jwt-authentication.md) | JWT for authentication | accepted | 2026-07-01 |

## 生命周期

```
proposed → accepted → [deprecated | superseded by ADR-NNNN]
```

新增 ADR 时:使用 `template.md` 作为模板,编号递增,并在本索引表中追加一行。
