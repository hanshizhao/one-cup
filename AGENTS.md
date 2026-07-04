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

---

## 前端：列表查询页标准（Query Table Page）

源自 Arco Design Pro 官方 `search-table` 最佳实践页源码。所有"表格 + 查询筛选"
的列表页**必须**遵守。完整规范见 `docs/frontend-standards.md`，代码模板见
`docs/specs/templates/`。

**核心原则：单个 Card 包整页；查询区用 Form + Grid 三列；查询/重置按钮放表单
外侧兄弟 div；新建列表页必须从模板复制，不从零手写布局。**

速查决策表：

| 问题 | 标准答案 |
| --- | --- |
| 整页容器 | 单个 `<Card>`，禁止裸 div / 再加 padding / 第二个 Card |
| 查询字段布局 | `Form` + `Row gutter={24}` + `Col span={8}`（一行 3 字段） |
| 查询/重置按钮 | 表单**外侧**兄弟 flex div，竖直 border 分隔，不放最后一个 Col |
| 查询触发方式 | 仅按钮触发（`getFieldsValue`），禁止字段 onChange 自动查询 |
| 表格工具栏 | flex `space-between` + 左右两个 `<Space>` |

**反模式**：用 `<Space wrap>` + 硬编码宽度排字段；按钮塞最后一个 Col；字段
onChange 自动查询；重置按钮叫"刷新"；新建按钮用 `<span/>` hack；页面自己加
`padding`（layout 已有，会双重 padding）。

**新建列表页**：复制 `docs/specs/templates/query-table-page.template.tsx` 改名，
按 `【替换点】` 注释改字段/列/接口，**不要从零手写布局**。

> 当需要查阅某个具体 Arco 组件的 API / 用法时，加载 `arco-design` 技能
> （`$arco-design` 或按 description 自动触发）。

---

## 业务与交互模式标准（Business & Interaction Conventions）

**核心原则：遇到下表场景必须走对应固定路径，不能凭感觉选方案。**

这是项目级"业务与交互模式标准"机制。下表是索引；**实现任何涉及下列场景的
功能前，必须先读对应详情文档，再开始写代码。**改动已有功能时，若发现现状与
标准不符，顺手修正并向用户说明。

| ID | 触发场景 | 必须走的标准 | 详情 |
| --- | --- | --- | --- |
| C01 | 任何"删除"操作（行内删除 / 批量删除 / 详情页删除） | 按"可逆性 + 影响范围"选 Popconfirm 或 Modal | docs/conventions/c01-delete-confirm.md |
| C02 | 创建带编号的业务对象（颜色 / 客户 / 商品 / 计量单位等） | 走"取预览 → 只读展示 → null 则禁用表单 + 提示"流程 | docs/conventions/c02-numbered-object-create.md |

### 实施规则（不可绕过）

1. **动手前必查**：实现任何功能前，扫一遍上方主决策表的"触发场景"列。命中
   任何一条，必须先读对应详情文档，再开始写代码。
2. **改动时对齐**：修改已有功能时，检查该功能是否命中某条标准。若命中且
   现状与标准不符，顺手修正并向用户说明。
3. **新增标准**：当用户描述"期望的工作流"或指出不一致时，先把它沉淀成一条
   新的 Convention（加一行表 + 建详情文件），再实现。不要只停留在对话里。
4. **引用而非复述**：实现某功能时，在回复里引用命中的标准 ID（如"本次删除
   操作按 C01 走 Popconfirm"），方便用户核对。

> 新增标准的完整步骤见 `docs/conventions/README.md`。标准采用渐进式成长：
> 初版至少写"适用边界"和"决策树"，参考实现与反模式可后补。
