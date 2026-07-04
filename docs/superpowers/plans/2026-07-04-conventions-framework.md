# 业务与交互模式标准框架（Conventions Framework）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立项目级"业务与交互模式标准（Conventions）"机制，消除跨会话一致性漂移；落地 C01（删除确认）和 C02（编号对象创建流程）两条初始标准，并顺手对齐客户删除偏离点。

**Architecture:** 复用项目已验证的"列表查询页标准"三层范式——AGENTS.md 主决策表（自动注入，必然读到）+ docs/conventions/ 详情文档（决策树，必然照做）。四道闸门（自动注入 → 触发词比对 → 详情决策树 → 反模式自查）确保执行一致性。

**Tech Stack:** Markdown 文档；前端 Arco Design（Popconfirm / Modal.confirm / Form disabled / Alert）；TypeScript + React。

**关联设计文档：** `docs/specs/2026-07-04-conventions-framework-design.md`

## Global Constraints

- AGENTS.md 是会话启动自动注入的项目级指令文件，改它等于改所有未来会话的行为——措辞必须精确、强约束、无歧义。
- Conventions ID 用 `c` 小写 + 两位数字（c01, c02...），文件名 = `c0X-<name>.md`。
- 详情文档固定四节结构：适用边界 / 标准（决策树）/ 参考实现 / 反模式。
- 主决策表的"触发场景"列必须写**穷举式触发词**（具体场景名），不写抽象描述。
- "实质变更"采取轻量版：直接改详情文档，不新建条目，提交信息说明变更。
- 客户删除属软删除（可逆），按 C01 决策树应走 Popconfirm（当前 Modal.confirm 是偏离）。
- 参照 `frontend/src/pages/system/role/index.tsx:158-166` 的 Popconfirm 写法作为对齐范本。

---

## File Structure

| 文件 | 动作 | 职责 |
| --- | --- | --- |
| `AGENTS.md` | 修改 | 新增「业务与交互模式标准」章节：主决策表（C01/C02 两行）+ 四条实施规则 |
| `docs/conventions/README.md` | 新建 | 目录入口 + "如何新增一条标准"的操作说明 |
| `docs/conventions/c01-delete-confirm.md` | 新建 | C01 详情：删除确认形式的决策树 |
| `docs/conventions/c02-numbered-object-create.md` | 新建 | C02 详情：编号对象创建的固定流程 |
| `frontend/src/pages/business/customer/index.tsx` | 修改 | 客户删除偏离点对齐：Modal.confirm → Popconfirm |

**设计原则：** 框架文档（前 4 个文件）是机制的"基座"，必须先于代码对齐落地；客户删除对齐（第 5 个文件）是 C01 的第一次实战验证。任务按"先立标准 → 再用标准修正代码"的顺序，体现机制本身的工作流。

---

### Task 1: 创建 conventions 目录与 README

**Files:**
- Create: `docs/conventions/README.md`

**Interfaces:**
- Produces: `docs/conventions/` 目录存在；README 作为目录入口，被后续 C01/C02 文档的相对链接引用，也被 AGENTS.md 主决策表通过 `docs/conventions/<id>.md` 路径指向。

- [ ] **Step 1: 创建目录与 README 文件**

创建 `docs/conventions/README.md`，内容如下：

````markdown
# 业务与交互模式标准（Conventions）

主索引见 `AGENTS.md` 的「业务与交互模式标准」章节。本目录存放各条标准的详情文档。

## 这是什么

"遇到 X 场景必须走 Y 路径"的项目级标准。与 ADR（技术选型决策）、AGENTS.md 其他章节（导航/列表页布局）平行，专管**业务交互模式**这一层。

## 如何新增一条标准

1. **确认已获用户认可**——Agent 不可未经用户确认就单方面新增标准。
2. 在 `AGENTS.md`「业务与交互模式标准」主决策表加一行：
   - ID（下一个 c 序号）/ 触发场景（穷举式触发词）/ 一句话标准 / 详情链接
3. 在本目录建 `cXX-<name>.md`，至少填写「适用边界」和「标准（决策树）」两节。
4. 「参考实现」「反模式」可后续补全（渐进式 Lv1 → Lv2）。

## 修改已有标准

直接修改详情文档，不新建条目；在提交信息里说明变更内容。

## 标准列表

| ID | 标准 | 详情 |
| --- | --- | --- |
| c01 | 删除操作的确认形式 | c01-delete-confirm.md |
| c02 | 带"编号"的业务对象创建流程 | c02-numbered-object-create.md |
````

- [ ] **Step 2: 验证文件与目录**

Run: `ls docs/conventions/`
Expected: 输出含 `README.md`。

- [ ] **Step 3: Commit**

```bash
git add docs/conventions/README.md
git commit -m "docs(conventions): 新建 conventions 目录与 README 入口

业务与交互模式标准框架的目录结构。"
```

---

### Task 2: 创建 C01 详情文档（删除确认）

**Files:**
- Create: `docs/conventions/c01-delete-confirm.md`

**Interfaces:**
- Consumes: Task 1 的目录结构。
- Produces: C01 详情，被 AGENTS.md 主决策表第一行链接；其"参考实现"指向 `system/role/index.tsx`；其决策树被 Task 5（客户删除对齐）用作判定依据。

- [ ] **Step 1: 创建 C01 文档**

创建 `docs/conventions/c01-delete-confirm.md`，内容如下：

````markdown
# c01 — 删除操作的确认形式

> 一句话标准：删除确认形式由"影响范围 + 可逆性"决定，不是凭感觉选。

## 1. 什么时候适用 / 什么时候不适用

**适用：**
- 任何对单条或多条持久化数据的删除操作：
  - 行内删除（表格操作列的删除按钮）
  - 批量删除（勾选多行后的批量删除）
  - 详情页删除

**不适用：**
- 移除表单子项（如动态表单删一行、删除一个标签项）
- 取消已暂存的草稿项
- 启用/禁用状态切换（这类用 Popconfirm 即可，不属删除范畴）

## 2. 标准（决策树）

删除操作是否可逆（软删除 / 有回收站）？

- **是，且只删单条** → `Popconfirm`（行内气泡确认）
- **否（物理删除），但只删单条** → `Popconfirm`（气泡确认，提示文案强调"不可恢复"）
- **批量删除（≥2 条）** → `Modal.confirm`（弹窗，列出待删项 + 二次确认）
- **删除会级联影响其他数据**（删父项连带删子项）→ `Modal.confirm`（必须列出影响范围）

> 关键：**同类场景必须用同形式**。不能"这个模块删单条用 Popconfirm，那个模块删单条用 Modal"。

## 3. 参考实现

- **行内 Popconfirm（软删除/单条）**：`frontend/src/pages/system/role/index.tsx`

```tsx
<Popconfirm
  title={t['role.delete.confirm']}
  onOk={() => handleDelete(record.id)}
  disabled={record.code === 'admin'}
>
  <Button type="text" size="small" status="danger" disabled={record.code === 'admin'}>
    {t['role.delete']}
  </Button>
</Popconfirm>
```

- **批量 Modal.confirm**：待补（项目暂无批量删除场景；出现时在此补范例）。

## 4. 反模式（禁止这样做）

- ❌ 物理删除 + 批量，却只用 Popconfirm（影响大却用轻确认）
- ❌ 同一系统内同类删除场景形式不一致（如客户删除用 Modal、角色删除用 Popconfirm）
- ❌ 用 Modal 删单条可逆数据（确认成本超出风险）
- ❌ 删除按钮没有二次确认，直接执行删除
````

- [ ] **Step 2: 验证文件存在且链接路径正确**

Run: `ls docs/conventions/c01-delete-confirm.md`
Expected: 文件存在。

- [ ] **Step 3: Commit**

```bash
git add docs/conventions/c01-delete-confirm.md
git commit -m "docs(conventions): C01 删除操作确认形式标准

按可逆性+影响范围选 Popconfirm/Modal 的决策树。"
```

---

### Task 3: 创建 C02 详情文档（编号对象创建流程）

**Files:**
- Create: `docs/conventions/c02-numbered-object-create.md`

**Interfaces:**
- Consumes: Task 1 的目录结构；真实接口 `previewCode`（`frontend/src/api/numbering.ts`）和真实参考实现 `color/index.tsx`（已在设计阶段勘查确认）。
- Produces: C02 详情，被 AGENTS.md 主决策表第二行链接。

- [ ] **Step 1: 创建 C02 文档**

创建 `docs/conventions/c02-numbered-object-create.md`，内容如下：

````markdown
# c02 — 带"编号"的业务对象创建流程

> 一句话标准：用户永不手填编号；编号由系统预览后只读回填，无规则则禁用表单 + 提示。

## 1. 什么时候适用 / 什么时候不适用

**适用：**
- 任何业务对象，其"编号"字段由编号引擎生成：
  - 颜色（color）
  - 客户（customer）
  - 商品（product，未来）
  - 计量单位（measurement unit）
  - 其他任何带系统生成编号的对象

**不适用：**
- 用户自定义的自由字段（name、备注、描述等）
- 无编号概念的对象

## 2. 标准（固定流程）

打开"新建"表单时，按序执行：

1. 调用编号预览接口 `previewCode(targetType, categoryCode?)`（来自 `frontend/src/api/numbering.ts`，对应后端 `GET /api/numbering/preview`）。
2. 预览编号写入表单"编号"字段，设为 `readOnly`（不可编辑）。
3. 判断返回值：
   - **非空** → 编号字段只读展示预览值，表单其余字段正常可用，用户填写后提交。
   - **`null`** → 表单整体 `disabled`，顶部显示 `Alert`（`type="warning"`）提示：
     "未找到可用编号规则，请先配置编号规则后再新增"，提示中带跳转到编号规则配置的入口；
     同时确定按钮 `disabled`。
4. 提交成功后关闭表单、刷新列表。

## 3. 参考实现

- **主参考实例**：`frontend/src/pages/master-data/color/index.tsx`
  - 状态：`previewedCode` / `codeLoading` / `noRule`
  - `openCreate()`：调 `previewCode('color')`，`code` 为 null 或失败时 `setNoRule(true)`
  - 无规则时：`okButtonProps={{ disabled: noRule }}` + 顶部 `Alert` + `Form disabled={noRule}`
  - 编号字段：`Input readOnly value={previewedCode ?? undefined}`，占位符区分"预览中/无规则"

- **第二实例（同模式）**：`frontend/src/pages/business/customer/form.tsx`

- **预览接口签名**：

```ts
// frontend/src/api/numbering.ts
export function previewCode(targetType: string, categoryCode?: string)
  : Promise<{ code: string | null; note: string }>
```

## 4. 反模式（禁止这样做）

- ❌ 让用户手动输入编号
- ❌ 编号字段可编辑（即使用预览值，也不应允许修改）
- ❌ 预览失败时表单仍可用（用户填完才发现提交失败）
- ❌ 只灰掉表单，不提示原因（用户不知道为什么禁用）
- ❌ 不提供跳转到编号规则配置的入口（用户被卡住无处可去）
````

- [ ] **Step 2: 验证文件存在**

Run: `ls docs/conventions/c02-numbered-object-create.md`
Expected: 文件存在。

- [ ] **Step 3: Commit**

```bash
git add docs/conventions/c02-numbered-object-create.md
git commit -m "docs(conventions): C02 编号对象创建流程标准

取预览→只读展示→null则禁用+提示的固定流程。
参考实现指向 color/index.tsx。"
```

---

### Task 4: 在 AGENTS.md 新增「业务与交互模式标准」章节

**Files:**
- Modify: `AGENTS.md`（在文件末尾的 arco-design 技能提示之后追加新章节）

**Interfaces:**
- Consumes: Task 1-3 的文档（通过链接引用）。
- Produces: AGENTS.md 新章节，含主决策表 + 四条实施规则。这是整个机制的"发动机"——会话启动自动注入，保证必然读到。

- [ ] **Step 1: 读取 AGENTS.md 当前末尾，确认追加点**

Run: `tail -5 AGENTS.md`
Expected: 看到末尾是 arco-design 技能提示那段（`> 当需要查阅某个具体 Arco 组件 ...`）。新章节追加在它之后。

- [ ] **Step 2: 追加新章节**

在 `AGENTS.md` 末尾追加以下内容（注意：在最后一行 `> （$arco-design ...）` 之后空一行再开始）：

````markdown

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
````

- [ ] **Step 3: 验证追加成功且 Markdown 格式正确**

Run: `tail -30 AGENTS.md`
Expected: 看到新章节标题「业务与交互模式标准」、含两行的主决策表、四条实施规则。

- [ ] **Step 4: Commit**

```bash
git add AGENTS.md
git commit -m "docs(conventions): AGENTS.md 新增业务与交互模式标准章节

主决策表（C01/C02）+ 四条实施规则。会话启动自动注入，
保证 Agent 必然读到并对照执行。"
```

---

### Task 5: 对齐客户删除偏离点（C01 实战验证）

**Files:**
- Modify: `frontend/src/pages/business/customer/index.tsx:146-155`（handleDelete 函数）

**Interfaces:**
- Consumes: C01 决策树（Task 2）判定客户删除（软删除+单条）应走 Popconfirm；参照 `system/role/index.tsx:158-166` 的 Popconfirm 写法。
- Produces: 客户删除与其他模块（role 等）形式一致，消除现存漂移。

**判定依据（C01 决策树实战）：**
- 客户删除 = 软删除（`CustomerService.cs:149` 设 `IsDeleted=true`，非物理删除）
- 操作对象 = 单条（`handleDelete(record)` 传单个 record）
- 命中 C01 分支："可逆 + 单条 → Popconfirm"
- 当前用 Modal.confirm → 偏离 → 对齐为 Popconfirm

- [ ] **Step 1: 读取 customer/index.tsx 的 handleDelete 与列定义上下文**

Run: 读 `frontend/src/pages/business/customer/index.tsx` 第 146-230 行（含 handleDelete 与 operations 列 render）。
目的：确认 handleDelete 的调用位置（是在列 render 里用 `onClick={() => handleDelete(record)}`，还是其他形式），以便把 Modal.confirm 包裹改成 Popconfirm 包裹按钮。

预期看到（基于已读 146-155 行）：
- `handleDelete(record)` 内部调用 `Modal.confirm({...})`
- 某处操作列 render 里有删除按钮 `onClick={() => handleDelete(record)}`

- [ ] **Step 2: 重构 handleDelete —— 去掉 Modal.confirm，改为纯删除逻辑**

修改 `frontend/src/pages/business/customer/index.tsx`。

把 `handleDelete` 函数（当前 146-155 行）：

```tsx
  function handleDelete(record: CustomerListItem) {
    Modal.confirm({
      title: t['customer.message.deleteOk'],
      onOk: async () => {
        await deleteCustomer(record.id);
        Message.success(t['customer.message.deleteSuccess']);
        fetchData();
      },
    });
  }
```

改为（移除 Modal.confirm 包裹，只留删除执行逻辑）：

```tsx
  async function handleDelete(record: CustomerListItem) {
    await deleteCustomer(record.id);
    Message.success(t['customer.message.deleteSuccess']);
    fetchData();
  }
```

- [ ] **Step 3: 把删除按钮用 Popconfirm 包裹（参照 role/index.tsx）**

在 customer/index.tsx 的操作列（operations 列 render）里，找到当前的删除按钮（形如 `<Button status="danger" onClick={() => handleDelete(record)}>...`），改为用 `Popconfirm` 包裹。

参照 `system/role/index.tsx:158-166` 的写法：

```tsx
<Popconfirm
  title={t['customer.message.deleteOk']}
  onOk={() => handleDelete(record)}
>
  <Button type="text" size="small" status="danger">
    {t['customer.delete']}
  </Button>
</Popconfirm>
```

注意：
- 删除按钮文案的 i18n key 以文件中实际已有的为准（读代码确认 `t['customer.delete']` 或类似 key 是否存在；若删除文案在别处，沿用现有 key）。
- `title` 沿用 `t['customer.message.deleteOk']`（原 Modal.confirm 的 title 用的就是这个 key）。
- 若原删除按钮外层已在 `<Space>` 内，保持 Space 结构不变，只把 Button 换成 Popconfirm 包 Button。

- [ ] **Step 4: 确认 Popconfirm 已在 customer/index.tsx 的 import 中**

检查文件顶部 import 语句。若 `Popconfirm` 未从 `@arco-design/web-react` 导入，补上：

```tsx
import { Popconfirm } from '@arco-design/web-react';
```

（合并进已有的 `@arco-design/web-react` import，不要新增重复 import 行。）

若改完后 `Modal` 在该文件不再被使用（删掉 Modal.confirm 后无其他 Modal 用途），移除 `Modal` 的 import 以避免 lint 警告。**先用全文搜索 `Modal` 确认是否还有其他用途**，再决定是否移除。

- [ ] **Step 5: 类型检查/构建验证**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无类型错误（确认 handleDelete 改为 async 后，onOk 回调兼容；Popconfirm 类型正确）。

若 tsc 报错，按报错信息修正（常见：onOk 期望返回值类型、record 类型不匹配）。

- [ ] **Step 6: 运行前端 lint（若项目配置了）**

Run: `cd frontend && npm run lint`（若 package.json 有 lint script）
Expected: 无新增 lint 错误。

若没有 lint script，跳过此步（在 Step 5 已做类型检查）。

- [ ] **Step 7: Commit**

```bash
git add frontend/src/pages/business/customer/index.tsx
git commit -m "fix(customer): 删除确认对齐 C01（Modal.confirm → Popconfirm）

客户删除属软删除（可逆）+ 单条，按 c01-delete-confirm 决策树
应走 Popconfirm。原 Modal.confirm 是全项目唯一偏离点。
参照 system/role/index.tsx 的 Popconfirm 写法。"
```

---

### Task 6: 整体验证与收尾

**Files:**
- 无新增/修改，仅验证。

- [ ] **Step 1: 验证 conventions 目录结构完整**

Run: `ls docs/conventions/`
Expected: `README.md`、`c01-delete-confirm.md`、`c02-numbered-object-create.md` 三个文件齐全。

- [ ] **Step 2: 验证 AGENTS.md 链接的路径与文件一一对应**

确认 AGENTS.md 主决策表里的两个链接路径真实存在：
- `docs/conventions/c01-delete-confirm.md` ✓
- `docs/conventions/c02-numbered-object-create.md` ✓

- [ ] **Step 3: 对照设计文档验收标准自检**

逐条核对 `docs/specs/2026-07-04-conventions-framework-design.md` 的 §9 验收标准：
1. AGENTS.md 新增章节含主决策表（C01/C02 两行）+ 四条实施规则 ✓
2. docs/conventions/ 含 README + c01 + c02 ✓
3. 两条详情文档均含四节 ✓
4. C02 参考实现指向真实接口和文件 ✓
5. 客户删除偏离点已对齐 ✓

- [ ] **Step 4: 最终提交（若有遗漏的小修正）**

若 Step 1-3 发现任何问题，修正后提交；若全部通过，无需额外提交，本 Task 仅作验证记录。

---

## 自检（writing-plans self-review）

**1. Spec coverage（设计文档覆盖）：**
- §4 整体架构 → Task 1（目录）+ Task 4（AGENTS.md 章节）✓
- §5 详情文档模板 → Task 2（C01）+ Task 3（C02），均四节结构 ✓
- §6 四道闸门 / 实施规则 → Task 4 Step 2（写入 AGENTS.md）✓
- §7.1 三种触发、§7.3 轻量修订、§7.2 渐进式 → Task 1 README 落地操作说明 ✓
- §8 目录结构命名 → Task 1-3 文件名 ✓
- §9 验收标准 → Task 6 逐条核对 ✓
- §9.5 客户删除对齐 → Task 5 ✓
- §10 排除项（hook/全项目审计/其他标准/大规模整改）→ 计划未涉及，正确排除 ✓

**2. Placeholder scan：** 无 TBD/TODO；Task 5 Step 3 的删除按钮文案 key 明确要求"以文件中实际已有的为准"并给出判定方法，非占位。

**3. Type consistency：** C01 文档引用的 `t['role.delete.confirm']`、`t['customer.message.deleteOk']` 均来自已读真实代码；`previewCode` 签名与 `frontend/src/api/numbering.ts` 一致；`handleDelete` 改 async 后 onOk 兼容（返回 Promise 即可，Arco Popconfirm onOk 支持 Promise 返回值）。
