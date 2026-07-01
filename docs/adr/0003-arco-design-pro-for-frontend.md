# ADR-0003: Arco Design Pro as frontend scaffold

**Date**: 2026-07-01
**Status**: accepted
**Deciders**: 项目开发者

## Context

需要搭建一个中后台管理系统前端,要求:完整的布局(侧边栏菜单/顶栏/面包屑)、权限路由、请求封装、国际化、主题切换。技术栈已确定为 React + TypeScript + Vite。关键问题是从零搭建这些基础设施,还是使用开箱即用的脚手架。

## Decision

采用 Arco Design Pro (Vite 版) 作为前端脚手架,保留其自带的 Redux 管理全局 UI 状态,业务数据通过 React Query 管理。

## Alternatives Considered

### Alternative 1: Vite 纯净模板 + 从零搭建
- **Pros**: 完全可控,版本最新
- **Cons**: 布局、权限路由、菜单、请求封装全部手写,大量样板代码,与 Arco Pro 背道而驰
- **Why not**: Arco Pro 已提供这些开箱即用,重复劳动无意义

### Alternative 2: Arco Design Pro + 替换 Redux 为 Zustand
- **Pros**: Zustand 更轻量无样板代码
- **Cons**: 需要改写 Arco Pro 的 store 结构,增加额外工作量
- **Why not**: Arco Pro 自带的 Redux 只管全局 UI 状态(用户/主题/菜单),业务数据走 React Query,Redux 的开销可接受

## Consequences

### Positive
- 中后台布局、权限路由、请求拦截器开箱即用,省去数天搭建工作
- 16+ 页面模板(表格/列表/表单/仪表盘)可直接参考或复用
- 组件库 Arco Design 与脚手架天然匹配

### Negative
- 锁定在 React 17 + react-router 5(Arco Pro Vite 版本较旧),后续可能需要升级
- 两套状态管理(Redux + React Query)需团队理解各自职责边界

### Risks
- Arco Pro Vite 版依赖版本偏旧(React 17, Vite 2) — 升级时需系统性更新,已记录为已知技术债
