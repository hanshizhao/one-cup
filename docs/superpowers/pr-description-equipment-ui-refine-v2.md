# 设备管理 UI 精装修 v2

> 本分支对设备管理模块（设备 / 设备类型 / 运行模板）的 UI 做了四轮迭代精装修，前后端联动。本 PR 描述同时作为**交接档案**——新会话/接手者读此 + 关联 spec 即可获得完整上下文。

## 总览

四个阶段，每阶段都走了「头脑风暴 → spec → plan → 子代理实施 → review」完整流程：

| 阶段 | 内容 | 关键产出 |
|---|---|---|
| ① 设备表单 Modal→Drawer | 设备新建/编辑从 560px Modal 改为 480px Drawer，单列左标签，与详情抽屉同形态 | EquipmentFormDrawer |
| ② 设备类型表单标准化 | 修复返回 bug（Tab URL 持久化）+ 按官方范式重排（双 Card + sticky 操作栏） | index.tsx Tab query、TypeFormPage 重排 |
| ③ 设备类型表单布局重构 | 左右分栏 35:65 + 删除参数编辑器预览面板 + 响应式 | ParameterEditor 精简、TypeFormPage 分栏 |
| ④ 运行模板独立 Tab | 模板从抽屉段落提升为第 3 Tab，前后端联动（跨类型查询 + 表单页选类型） | 新端点、TemplateTab、表单页改造 |

## 改动规模

- 23 个 commit，31 文件，+4070 / -846
- 前端：设备/设备类型/模板三模块的容器、列表、表单、详情抽屉、参数编辑器
- 后端：模板跨类型分页查询端点 + 顶层详情端点 + DTO 补字段

## 各阶段详情（含设计决策，供接手者参考）

### 阶段 ① 设备表单 Modal→Drawer
- **设计稿依据**：`docs/superpowers/specs/2026-07-07-equipment-form-drawer-design.md`
- **关键决策**：12 个字段在 Modal 里过高，Drawer 对纵向滚动更友好；与详情抽屉同形态（480px）统一视觉
- **偏离设计稿**：设计稿要求设备编辑用 Modal，实际改用 Drawer（理论与体感偏差的修正，spec 有说明）

### 阶段 ② 设备类型表单标准化
- **设计稿依据**：`docs/superpowers/specs/2026-07-07-equipment-type-form-standardize-design.md`
- **bug 修复**：返回按钮 `navigate(-1)` 跳错 Tab —— 根因是 Tab 状态在 useState 里不随 URL 持久化。改为 `?tab=` query 持久化 + 显式跳转
- **标准化**：参照 Arco Pro `form/group` 官方范式（页头 + 双 Card + 底部 sticky 操作栏）

### 阶段 ③ 设备类型表单布局重构
- **设计稿依据**：`docs/superpowers/specs/2026-07-07-equipment-type-form-layout-design.md`
- **关键决策**：宽屏下控件过宽 → 改左右分栏（基础信息左 35% + 参数定义右 65%）；删除参数编辑器的「在模板里长什么样」预览面板（价值低、占宽度）

### 阶段 ④ 运行模板独立 Tab
- **设计稿依据**：`docs/superpowers/specs/2026-07-07-equipment-template-tab-design.md`
- **前后端联动**：
  - 后端新增 `GET /api/equipment-templates`（跨类型分页）+ `GET /api/equipment-templates/{id}`（顶层详情）
  - DTO 补 `equipmentTypeId` / `equipmentTypeName`
  - 模板表单路由去掉 typeId，类型在表单内选（新建可选 / 编辑锁定，切换类型清空值）
- **详情抽屉降级**：模板段落从可编辑列表改为只读概览 + 跳转链接

## review 抓出并修复的关键 bug（重要交接信息）

- **后端 status 计算失效**：`EquipmentTemplatePagedSpec` 漏了 `ApplyInclude(Values)`，导致跨类型列表里所有模板状态显示「有效」。已修（commit c36cd5a）
- **详情 Status 硬编码**：详情 DTO 顶层 `Status` 硬编码 `"valid"`。已改为 `WorstStatus` 派生
- **窄屏 align-items**：左右分栏的 `@media` 没重置 `align-items`，导致窄屏堆叠时卡片变窄。已加 `align-items: stretch`
- **`?typeId=` 未消费**：模板 Tab 读了 `searchParams` 但没用，导致从抽屉跳转后不自动定位类型。已修（commit c7627ee）

## 已知遗留 / 后续工作

- 设备相关页面的 **UI 视觉细节**会由「另一个会话（高手）」接手打磨——布局骨架已搭好，但像素级润色仍需人工审美判断
- `ParameterEditor.tsx` 有个预存的 `Space` orphan import（review 提过，未动以控范围）
- 后端 `GetProcessNames` 全表加载（预存，性能优化点，非阻塞）
- `getEquipmentTemplateById`（嵌套版）前端已无引用，可清理（保留无害）

## 关联文档（交接必读）

设计稿（spec，含完整决策依据）：
- `docs/superpowers/specs/2026-07-07-equipment-form-drawer-design.md`
- `docs/superpowers/specs/2026-07-07-equipment-type-form-standardize-design.md`
- `docs/superpowers/specs/2026-07-07-equipment-type-form-layout-design.md`
- `docs/superpowers/specs/2026-07-07-equipment-template-tab-design.md`

实施计划（plan，含任务拆分与代码）：
- `docs/superpowers/plans/2026-07-07-equipment-form-drawer.md`
- `docs/superpowers/plans/2026-07-07-equipment-type-form-standardize.md`
- `docs/superpowers/plans/2026-07-07-equipment-type-form-layout.md`
- `docs/superpowers/plans/2026-07-07-equipment-template-tab.md`

## 验收

- 前端 `npm run build` 通过
- 后端 `dotnet build` + 模板单测 6/6 + 设备单测 29/29 通过
- 人工验收：设备 Drawer、设备类型表单（含返回 bug）、模板 Tab 全流程已逐条核对通过

---

> 接手者：先读本 PR 描述 + 上述 4 份 spec，即可获得完整设计上下文。代码层面的演进脉络见 git log（每个阶段的 spec→plan→实施→review fix commit 都成对存在）。
