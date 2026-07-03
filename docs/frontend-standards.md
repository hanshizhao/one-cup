# 前端标准手册（Frontend Standards）

> 本文件是 OneCup 前端的**项目级一致性规范**。
> 与 `AGENTS.md` 并列——AGENTS.md 是每次会话注入的精简指令，本文件是各规范的完整版。
>
> 组件级用法（`Row`/`Col`、`Form.Item field`、`Select.Option` 等）请查阅 `arco-design` 技能；
> 本文件只管**项目级一致性**——"我们这个项目里，XX 必须怎么写"。

---

## 列表查询页标准

> 完整设计文档：`docs/specs/2026-07-03-query-table-standard-design.md`
> 代码模板：`docs/specs/templates/query-table-page.template.tsx` + `query-table-page.module.less.template`
> 依据：Arco Design Pro 官方 `search-table` 最佳实践页源码

### 决策表（速查）

所有列表查询页（含表格 + 查询筛选的页面）**必须**遵守：

| 问题 | 标准答案 |
|---|---|
| 整页容器？ | 单个 `<Card>`，**禁止**裸 div / 再加 padding / 第二个 Card |
| 页面标题？ | `<Typography.Title heading={6}>`，作为 Card 第一个子元素 |
| 查询字段布局？ | `Form` + `Row gutter={24}` + `Col span={8}`（一行 3 字段） |
| 字段绑定属性？ | `Form.Item` 用 `field`（非 antd 的 `name`） |
| 查询/重置按钮位置？ | 表单**外侧**兄弟 flex div，竖直 border 分隔，**不**放最后一个 Col |
| 查询触发方式？ | 仅按钮触发（`getFieldsValue`），**禁止**字段 onChange 自动查询 |
| 重置行为？ | `resetFields()` 后用 `{}` 重查 |
| 分页与筛选？ | 筛选保留到分页请求；新查询重置到第 1 页 |
| 表格工具栏？ | flex `space-between` + 左右两个 `<Space>` |
| 表格状态？ | `loading`/`pagination` 外部受控；`rowKey` 必填 |
| 折叠/展开？ | **不做**（官方未实现，当前最大页面 ≤5 字段） |

### 反模式（禁止）

- 用 `<Space wrap>` + 硬编码宽度排查询字段（现 7 页通病）。
- 查询/重置按钮塞在最后一个 `<Col>` 里"右对齐"（官方做法是表单外侧兄弟 div）。
- 字段 `onChange` 自动触发查询（应仅按钮触发）。
- 重置按钮叫"刷新"、或用错误图标（统一叫"重置" + `IconRefresh`）。
- 新建按钮用 `<span/>` 占位 hack 撑到右边（应用 flex `space-between` 工具栏）。
- 页面自己加 `<div style={{padding:16}}>`（layout 已有 padding，会双重 padding）。

### 标准骨架（最小可用）

```tsx
<Card>
  <Title heading={6}>标题</Title>
  <SearchForm onSearch={handleSearch} />   {/* Form + Row gutter=24 + Col span=8 + 外侧按钮 div */}
  <div className={styles['button-group']}>{/* flex space-between + 两个 Space */}</div>
  <Table rowKey loading pagination onChange columns data />
</Card>
```

查询区三件套（固定结构，照抄）：

```tsx
<div className={styles['search-form-wrapper']}>     {/* display:flex */}
  <Form form={form} labelAlign="left" labelCol={{ span: 5 }} wrapperCol={{ span: 19 }}>
    <Row gutter={24}>
      <Col span={8}><Form.Item label="名称" field="name"><Input allowClear/></Form.Item></Col>
      ...
    </Row>
  </Form>
  <div className={styles['right-button']}>           {/* flex-direction:column; justify-content:space-between */}
    <Button type="primary" icon={<IconSearch/>} onClick={handleSubmit}>查询</Button>
    <Button icon={<IconRefresh/>} onClick={handleReset}>重置</Button>
  </div>
</div>
```

**新建列表页时**：复制 `docs/specs/templates/query-table-page.template.tsx` 改名，按 `【替换点】` 注释改字段/列/接口即可，**不要从零手写布局**。

---

<!-- 后续前端标准（如表单弹窗标准、详情页标准等）按相同结构追加到本文件 -->
