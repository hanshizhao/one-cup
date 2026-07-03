# AGENTS.md — OneCup 项目指令

> 本文件是 ZCode 的**项目级指令文件**（workspace instruction file）。
> 每个会话启动时自动注入到上下文，用于约定本项目所有 Agent 必须遵守的规范。
> 依据：ZCode 官方配置指南——`<repo>/AGENTS.md` 适用于"repository-specific rules
> that should be shared with the project, such as architecture boundaries ... "。

---

## 前端：导航架构决策规则（Menu vs Tabs）

源自 Arco Design 官方组件规范（arco.design/docs/spec/menu、/docs/spec/tabs）。

**核心原则：侧边栏 Menu 保持扁平，功能模块内部导航用 Tabs。**

- **侧边栏 Menu 是系统级功能导航**，用于不同功能模块之间切换。保持扁平层级——
  避免不必要的多级 SubMenu 嵌套。
- **不要用侧边栏 Menu 组织子模块内容**。一个功能模块内部的子页面 / 子视图
  （例如"规则配置 / 业务字典 / 生成日志"同属"编号管理"），应使用页面内 Tabs，
  而不是拆成侧边栏的 SubMenu 子项。
- **Tabs 用于同层级、不同类别的内容**。当多个视图属于同一功能模块、层级相同、
  只是类别不同时，用 Tabs 组织。

| 判断问题 | 答案 | 用什么 |
| --- | --- | --- |
| 这些视图属于不同系统级模块吗？ | 是 → | 侧边栏菜单项（平级） |
| 这些视图属于同一个功能模块吗？ | 是 → | 页面内 Tabs |

**反模式**：把同模块的子视图拆成侧边栏多级 SubMenu——菜单层级变深、信息触达
变慢，且与 Arco 设计规范冲突。

> 当需要查阅某个具体 Arco 组件的 API / 用法时，加载 `arco-design` 技能
> （`$arco-design` 或按 description 自动触发）。
