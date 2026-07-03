# 列表查询页标准（Query Table Page Standard）

> 状态：已确认，待实现
> 日期：2026-07-03
> 依据：Arco Design Pro 官方 `search-table` 最佳实践页源码
> （`arco-design-pro-next/src/pages/list/search-table/`）

---

## 1. 背景与动机

项目前端目前有 **7 个列表查询页**，查询区的写法**毫无标准、各写各的**：

| 维度 | 现存乱象 |
|---|---|
| 容器 | 裸 `<div>`、`<div style={{padding:16}}>`（操作/登录日志**双重 padding** bug）、`<Tabs>` |
| 字段布局 | **全部**手写 `<Space>` + 硬编码宽度（240/150/130/200/260/300… 各页不一），无任何一页用 Grid |
| 查询按钮 | 仅「编号管理-日志」tab 有；其他页靠字段 `onChange` 自动触发 |
| 重置按钮 | 仅 1 处叫「重置」；操作/登录日志页叫「刷新」却用着重置图标 |
| 新建按钮位置 | `space-between` / `<span/>` 占位 hack / 独立 `marginBottom`，三种放法 |
| Card 包裹 | **7 页全无** |
| 复用组件 | **零**，每页复制粘贴自己的查询行 |

项目刚起步，现在是立标准成本最低的窗口。本文档把 **Arco Design Pro 官方最佳实践**提取为项目级标准，所有列表查询页必须遵守。

> 注：`arco-design-pro-vite` / `arco-design-pro-cra` 变体**不含** search-table 页面，该页仅存在于 `arco-design-pro-next` 变体；但其中代码是框架无关的 React + Arco 组件代码，可平移到本项目的 Vite + React 项目。

---

## 2. 标准模式（Standard Pattern）

下列模式提取自 Arco Pro `search-table` 源码，所有列表查询页**必须照此实现**。每个要点后标注源码出处。

### 2.1 单 Card 包整页

整页用**单个 `<Card>`** 包裹——表单、工具栏、表格全部在一个 Card 内。**禁止**裸 `<div>`、**禁止**再加外层 padding、**禁止**第二个 Card。

页面标题用 `<Typography.Title heading={6}>` 作为 Card 的第一个子元素（**不是** Card 的 `title` prop）。

```tsx
const { Title } = Typography;
// ...
return (
  <Card>
    <Title heading={6}>页面标题</Title>
    <SearchForm onSearch={handleSearch} />
    {/* 工具栏 */}
    {/* Table */}
  </Card>
);
```
*出处：`search-table/index.tsx:85-117`*

### 2.2 查询表单 = 受控 Form + Grid

查询表单是**页面级独立组件**（如 `SearchForm`），受控于 `Form.useForm()`：

```tsx
const [form] = Form.useForm();

<Form
  form={form}
  className={styles['search-form']}
  labelAlign="left"
  labelCol={{ span: 5 }}
  wrapperCol={{ span: 19 }}
>
  <Row gutter={24}>
    <Col span={8}>
      <Form.Item label="名称" field="name">
        <Input allowClear />
      </Form.Item>
    </Col>
    {/* 每个字段一个 <Col span={8}> */}
  </Row>
</Form>
```

**关键参数（固定值）：**

| 参数 | 值 | 说明 |
|---|---|---|
| `Form` `labelAlign` | `"left"` | 标签左对齐 |
| `Form` `labelCol` | `{ span: 5 }` | 标签占 5 栅 |
| `Form` `wrapperCol` | `{ span: 19 }` | 输入控件占 19 栅（5+19=24） |
| `Row` `gutter` | `24` | 列间距 |
| `Col` `span` | `8` | 每字段占 8 栅 → 一行 **3 个字段** |
| `Form.Item` 字段绑定 | `field="xxx"` | Arco 规范，**不是** antd 的 `name` |
| `Row`/`Col` 取法 | `const { Row, Col } = Grid;` | 从 Grid 解构 |

*出处：`search-table/form.tsx:18, 43-50`*

### 2.3 查询/重置按钮 = 表单外侧兄弟 flex div（核心视觉特征）

**查询/重置按钮不在 Form 内、不在最后一个 Col 里**，而是表单**外侧的兄弟 flex div**，由一道竖直 border 视觉分隔，按钮纵向排列（查询在上、重置在下）。

```tsx
<div className={styles['search-form-wrapper']}>
  <Form ...>
    <Row gutter={24}>...</Row>
  </Form>
  <div className={styles['right-button']}>
    <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>查询</Button>
    <Button icon={<IconRefresh />} onClick={handleReset}>重置</Button>
  </div>
</div>
```

配套 CSS（写进页面的 `style/index.module.less`）：

```less
.search-form-wrapper {
  display: flex;
  border-bottom: 1px solid var(--color-border-1);
  margin-bottom: 20px;

  .right-button {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    padding-left: 20px;
    margin-bottom: 20px;
    border-left: 1px solid var(--color-border-2);
    box-sizing: border-box;
  }
}
```

*出处：`search-table/form.tsx:42-131`、`search-table/style/index.module.less:20-34`*

### 2.4 查询/重置行为

```tsx
const handleSubmit = () => {
  const values = form.getFieldsValue();
  props.onSearch(values);
};

const handleReset = () => {
  form.resetFields();
  props.onSearch({});
};
```

**行为规范：**

- `查询` → `form.getFieldsValue()` 读值 → 回调父组件。**不**调用 `validate()`（查询不需要校验）。
- `重置` → `form.resetFields()` → 用 `{}` 重新查询（清空所有筛选）。
- **字段不随 `onChange` 自动触发查询**——只在点按钮时查，避免疯狂请求。
- 查询按钮 **`onClick`** 触发（不用原生 `htmlType="submit"`）。
- 按钮固定：查询 = `type="primary" icon={<IconSearch/>}`；重置 = 默认 type + `icon={<IconRefresh/>}`。

*出处：`search-table/form.tsx:29-37, 124-129`*

### 2.5 分页与筛选的状态联动

父页面持有 `formParams` 状态，用 `useEffect` 在分页或筛选变化时重新请求：

```tsx
const [formParams, setFormParams] = useState({});
const [pagination, setPagination] = useState({
  sizeCanChange: true,
  showTotal: true,
  pageSize: 10,
  current: 1,
  pageSizeChangeResetCurrent: true,
});

useEffect(() => {
  fetchData();
}, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

function fetchData() {
  const { current, pageSize } = pagination;
  setLoading(true);
  axios.get('/api/list', {
    params: { page: current, pageSize, ...formParams },
  }).then((res) => {
    setData(res.data.list);
    setPagination((p) => ({ ...p, total: res.data.total }));
    setLoading(false);
  });
}

function handleSearch(params) {
  setPagination((p) => ({ ...p, current: 1 }));   // 新查询回到第 1 页
  setFormParams(params);
}
```

**要点：**
- 筛选条件**保留**到分页请求里（`...formParams` 合并进每次请求）。
- 新查询把 `current` 重置为 1。
- `JSON.stringify(formParams)` 作为 effect 依赖（对象引用对比无效）。

*出处：`search-table/index.tsx:43-83`*

### 2.6 表格工具栏 = flex space-between + 两个 Space 组

```tsx
<div className={styles['button-group']}>
  <Space>
    <Button type="primary" icon={<IconPlus />}>新建</Button>
    {/* 左侧主操作组 */}
  </Space>
  <Space>
    <Button icon={<IconDownload />}>下载</Button>
    {/* 右侧次要操作组 */}
  </Space>
</div>
```

```less
.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}
```

**要点：**
- 左右**两个** `<Space>` 组（不是单个 Space），靠 flex `space-between` 撑开。
- 左侧放主操作（新建 primary），右侧放次要操作（下载等）。
- 需要权限控制的按钮，整个工具栏用 `<RequirePermission>` 包裹（本项目约定）。

*出处：`search-table/index.tsx:89-107`、`style/index.module.less:36-40`*

### 2.7 表格

```tsx
<Table
  rowKey="id"
  loading={loading}
  onChange={onChangeTable}     // 回写 current/pageSize 到 state
  pagination={pagination}      // 外部受控
  columns={columns}
  data={data}
/>
```

**要点：**
- `rowKey` 必填。
- `loading` / `pagination` 均由父组件 state 外部控制。
- `onChange` 回写分页状态（`{ current, pageSize }` → `setPagination`）。

*出处：`search-table/index.tsx:108-115`*

---

## 3. 决策表（Decision Table）

> 这是标准的速查版，也会写入 `AGENTS.md`。

| 问题 | 标准答案 |
|---|---|
| 整页容器？ | 单个 `<Card>`，**禁止**裸 div / 再加 padding |
| 页面标题？ | `<Typography.Title heading={6}>`，作为 Card 第一个子元素 |
| 查询字段布局？ | `Form` + `Row gutter={24}` + `Col span={8}`（一行 3 字段） |
| 字段绑定属性？ | `Form.Item` 用 `field`（非 antd 的 `name`） |
| 查询/重置按钮位置？ | 表单**外侧**兄弟 flex div，竖直 border 分隔，**不**放最后一个 Col |
| 查询触发方式？ | 仅按钮触发（`getFieldsValue`），**禁止**字段 onChange 自动查询 |
| 重置行为？ | `resetFields()` 后用 `{}` 重查 |
| 分页与筛选？ | 筛选保留到分页请求；新查询重置到第 1 页 |
| 表格工具栏？ | flex `space-between` + 左右两个 `<Space>` |
| 表格状态？ | `loading`/`pagination` 外部受控；`rowKey` 必填 |
| 折叠/展开？ | **不做**（官方源码未实现，当前最大页面 ≤5 字段，平铺放得下） |

---

## 4. 落地结构

### 4.1 文档与模板存放

| 交付物 | 位置 |
|---|---|
| 完整设计文档（本文档） | `docs/specs/2026-07-03-query-table-standard-design.md` |
| 前端标准手册（含本标准全文） | `docs/frontend-standards.md` |
| 整页代码模板 | `docs/specs/templates/query-table-page.template.tsx` |
| 样式模板 | `docs/specs/templates/query-table-page.module.less.template` |
| AGENTS.md 指针 | `AGENTS.md` 增「列表查询页标准」一节，指向本手册 |

### 4.2 代码模板说明

提供两个可直接复制改名的模板：

- `query-table-page.template.tsx`：整页骨架——Card + Title + SearchForm（含 useForm + handleSubmit/handleReset）+ 工具栏 + Table + 数据/分页状态逻辑。新建列表页时复制此文件，按实际字段改 `Form.Item`。
- `query-table-page.module.less.template`：三段样式——`.search-form-wrapper` / `.right-button` / `.button-group`。

模板内的字段、列、接口按注释标记为替换点。

---

## 5. 迁移范围（现有 7 页对齐到新标准）

| 批次 | 页面 | 现状 → 改成 |
|---|---|---|
| **先改 2 页做样板** | 编号管理-规则配置 tab | `Space space-between` + 无按钮 → 新标准（含查询/重置） |
| | 操作日志 | `Space wrap` + "刷新" → 新标准（统一叫"重置"） |
| **推广** | 登录日志 | 同上，修复双重 padding bug |
| | 编号管理-生成日志 tab | 已有查询/重置但布局乱 → 对齐 |
| | 用户管理 | 单字段 → 套标准骨架（字段少也用同样骨架） |
| **例外（无查询区）** | 角色管理 | 只套 Card + Title + 工具栏骨架 |
| | 权限管理 | 只套 Card + Title 骨架 |
| | 编号字典 | 只套 Card + Title + 工具栏骨架，移除 `<span/>` hack |

迁移策略：**先改 2 页做样板**，验证标准可行后再批量推广。执行顺序在实现计划阶段细化。

---

## 6. 与现有规范的关系

- 本标准与 `AGENTS.md` 现有的「Menu vs Tabs」规则**并列**，同属前端导航/布局规范。
- 组件级用法（`Row`/`Col`、`Form.Item field`、`Select.Option` 等）仍查阅 `arco-design` 技能；本标准只管**项目级一致性**。
- 本标准**不**引入共享组件（`QueryForm` / `PageCard` 等），采用纯文档 + 模板方式落地。原因：项目初期先用清晰规则在多页验证模式，等模式稳定后再考虑抽象为组件（避免过早抽象）。

---

## 7. 已确认的决策记录

| 决策点 | 选择 | 理由 |
|---|---|---|
| 标准范围 | 全套前端标准 | 一次立全，避免"查询区统一但工具栏各写各的" |
| 固化方式 | 纯文档规范 + 代码模板 | 项目初期避免过早抽象；模式稳定后再抽组件 |
| 布局来源 | 直接提取自 Arco Pro 源码 | 让证据定规则，不空想 |
| 网格密度 | 一行 3 字段（Col span=8） | 官方 zh-CN 默认值 |
| 折叠/展开 | 不做 | 官方源码未实现；当前最大页面 ≤5 字段，平铺放得下 |
| 按钮结构 | 表单外侧兄弟 flex div | 官方最佳实践的视觉精髓（竖直分隔线 + 查询上重置下） |
| 文档存放 | 独立 `docs/frontend-standards.md`，AGENTS.md 只放指针 | 避免 AGENTS.md 过长稀释重点 |
