# ADR-0005: Monolith over microservices

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

系统是印染厂内部应用,用户规模几十到几百人。需要决定后端部署形态:单体还是微服务。这决定了运维复杂度、开发节奏和团队规模要求。

## Decision

采用单体应用部署,一个后端进程承载所有业务逻辑,一个数据库。

## Alternatives Considered

### Alternative 1: 微服务(按业务域拆分)
- **Pros**: 独立扩展,技术栈可异构
- **Cons**: 服务发现、网关、分布式事务、链路追踪等运维复杂度极高
- **Why not**: 几十到几百人的内部系统不需要微服务的扩展能力,过度设计

### Alternative 2: 模块化单体(折中)
- **Pros**: 单体内部按模块清晰分层,未来可拆
- **Cons**: 与本方案实际差异不大,只是命名不同
- **Why not**: 已通过 Clean Architecture 分层实现模块化,无需额外概念

## Consequences

### Positive
- 部署简单:一个进程 + 一个数据库
- 开发调试方便,无分布式问题
- 运维成本最低,不需要 K8s/服务网格

### Negative
- 无法独立扩展单个模块(如产品检索量大时)
- 所有模块共享一个进程,一个模块崩溃影响全局

### Risks
- 随业务增长,单进程可能成为瓶颈 — 通过 Clean Architecture 保持模块边界清晰,未来可按模块拆出服务
