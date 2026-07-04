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
