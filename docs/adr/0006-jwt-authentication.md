# ADR-0006: JWT for authentication

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

前后端分离架构下,需要确定认证方案。系统是企业内部应用,有用户/角色/权限管理需求。认证方案需要无状态、易扩展、与前后端分离天然适配。

## Decision

采用 JWT (JSON Web Token) 作为认证方案,前端在 localStorage 存储 token,请求时通过 Authorization header 携带。

## Alternatives Considered

### Alternative 1: Cookie 认证
- **Pros**: .NET Identity 原生支持,自动处理续期,防 CSRF 有成熟方案
- **Cons**: 跨域配置复杂,前后端分离下不如 JWT 灵活
- **Why not**: 前后端分离 + 不同端口开发,Cookie 的 SameSite/CORS 处理更麻烦

### Alternative 2: IdentityServer (OIDC)
- **Pros**: 完整的 OAuth2/OIDC 实现,支持单点登录
- **Cons**: 单系统使用过于重量级,运维复杂
- **Why not**: 当前只有一个系统,不需要 SSO 能力

## Consequences

### Positive
- 无状态,服务端不需要存 session,水平扩展简单
- 前后端分离标准方案,跨域天然友好
- 移动端/其他客户端未来可直接复用

### Negative
- Token 无法主动失效(除非引入黑名单机制)
- Token 存 localStorage 有 XSS 风险

### Risks
- Token 续期体验 — 通过短有效期 + Refresh Token 机制缓解
- XSS 窃取 token — 通过前端安全实践(输入转义、CSP)降低风险
