# 列表查询页标准迁移 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把现有 7 个列表查询页全部对齐到「列表查询页标准」（提取自 Arco Pro search-table 最佳实践），消除各页手写 `<Space>` + 硬编码宽度的乱象。

**Architecture:** 纯文档规范（不引入共享组件）。先改 2 页做样板验证标准可行，再批量推广。每页改造 = 把查询区从 `<Space>` 重写为 `Form + Grid + 外侧按钮 div`，套 `<Card>` 容器，统一工具栏。页面已有的数据拉取/分页/抽屉逻辑全部保留，只换布局层。

**Tech Stack:** React 18 + TypeScript + Arco Design Web React 2.32 + Vite 5；测试用 Vitest 4（jsdom + globals）+ @testing-library/react 16 + @testing-library/jest-dom 6。

## Global Constraints

- **测试命令**：`npm test`（即 `vitest run`）；类型检查 `npx tsc --noEmit`；构建 `npm run build`。所有命令在 `frontend/` 目录下执行。
- **路径别名**：`@/*` → `frontend/src/*`（vitest.config.ts 已配同样 alias）。
- **Arco 导入**：始终从 `@arco-design/web-react` 根导入，图标从 `@arco-design/web-react/icon`。禁止子路径导入。
- **Form.Item**：用 `field` 属性绑定（非 antd 的 `name`）。
- **不引入新依赖、不引入共享组件文件**。所有改造在现有页面文件内完成。
- **测试环境**：vitest globals 已开（`describe/it/expect` 全局可用，无需 import）；jest-dom matchers 需 Task 0 注册 setup 文件。
- **标准依据**：`docs/specs/2026-07-03-query-table-standard-design.md`；速查表见 `docs/frontend-standards.md`。
- **AGENTS.md 规则**：编号管理的规则/字典/日志是同模块子视图 → 必须用页面内 Tabs（已是这样，改造时保留 Tabs 结构不动）。
- **提交粒度**：每个 Task 结束提交一次，commit message 用 `refactor(fe):` 前缀。

---

## File Structure

改造涉及的文件（按 Phase 分组）。**不新建任何共享组件**——每页自带标准布局。

### Phase 1（样板，先做）
- **Task 0**：`frontend/vitest.config.ts`（改）+ `frontend/src/test-setup.ts`（建）— 注册 jest-dom matchers
- **Task 1**：`frontend/src/pages/system/operation-log/index.tsx`（改）+ `frontend/src/pages/system/operation-log/style/index.module.less`（建）+ `frontend/src/pages/system/operation-log/__tests__/index.test.tsx`（建）
- **Task 2**：`frontend/src/pages/system/numbering/index.tsx`（改，规则配置 tab + 生成日志 tab 两个查询区）+ `frontend/src/pages/system/numbering/style/index.module.less`（建）+ `frontend/src/pages/system/numbering/__tests__/index.test.tsx`（建）

### Phase 2（推广，Phase 1 验收通过后做）
- **Task 3**：`frontend/src/pages/system/login-log/index.tsx`（改）+ 样式 + 测试
- **Task 4**：`frontend/src/pages/system/user/index.tsx`（改）+ 样式 + 测试
- **Task 5**：`frontend/src/pages/system/role/index.tsx` + `permission/index.tsx` + `numbering/dict/index.tsx`（改，无查询区，只套 Card + Title + 工具栏，移除 `<span/>` hack）

每个改造页的目录结构约定（与 Arco Pro 一致）：
```
pages/system/xxx/
  index.tsx                  ← 页面（Card + Title + 查询区 + 工具栏 + Table）
  style/index.module.less    ← 三段标准样式
  __tests__/index.test.tsx   ← 结构断言测试
```

---

## Phase 0：测试基础设施

### Task 0: 注册 jest-dom matchers

现有 3 个测试都是测纯逻辑（store/reducer），不渲染组件，所以 `@testing-library/jest-dom` 装了却没注册。本任务注册它，为后续组件渲染测试铺路。

**Files:**
- Create: `frontend/src/test-setup.ts`
- Modify: `frontend/vitest.config.ts`

**Interfaces:**
- Produces: 全局 jest-dom matchers（`toBeInTheDocument` 等）对所有测试可用；setup 文件路径写入 vitest config。

- [ ] **Step 1: 创建 setup 文件**

`frontend/src/test-setup.ts`：
```ts
import '@testing-library/jest-dom';
```

- [ ] **Step 2: 在 vitest.config.ts 注册 setupFiles**

把 `frontend/vitest.config.ts` 的 `test` 块改为：
```ts
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
```

- [ ] **Step 3: 验证 setup 生效（跑现有测试 + 类型检查）**

Run:
```bash
cd frontend && npm test
```
Expected: 现有 3 个测试全 PASS（store, authentication, transformPermissions），无新增失败。

Run:
```bash
cd frontend && npx tsc --noEmit
```
Expected: 无错误退出（exit 0）。

- [ ] **Step 4: Commit**

```bash
git add frontend/src/test-setup.ts frontend/vitest.config.ts
git commit -m "test(fe): 注册 jest-dom matchers setup 文件"
```

---

## Phase 1：样板页改造（先验证标准可行）

### Task 1: 操作日志页 — 改造为标准布局

操作日志页是最典型的多字段查询页（搜索+模块+结果+日期+刷新），且存在双重 padding bug（`<div style={{padding:16}}>` 叠加 layout 的 padding）。本任务把它改造成标准样板。

**现状**（`operation-log/index.tsx:80-113`）：
- 外层 `<div style={{ padding: 16 }}>` → 双重 padding bug
- `<Space wrap>` 横排 5 个控件，硬编码宽度
- 字段 onChange 自动触发查询（违反标准）
- 按钮叫「刷新」用 `IconRefresh`，无「重置」

**目标**：单 `<Card>` + 标准查询区（Form+Grid 三列 + 外侧查询/重置按钮 div）+ 工具栏（此页无新建，只有刷新放右侧 Space）+ 保留 Drawer 详情逻辑。

**Files:**
- Create: `frontend/src/pages/system/operation-log/style/index.module.less`
- Create: `frontend/src/pages/system/operation-log/__tests__/index.test.tsx`
- Modify: `frontend/src/pages/system/operation-log/index.tsx`

**Interfaces:**
- Consumes: `getOperationLogs`/`getOperationLog` from `@/api/auditLog`（不变）；`OperationLogQuery` 类型（不变）
- Produces: 标准布局操作日志页；`OperationLogQuery` 字段含义不变（keyword/module/result/startTime/endTime/page/pageSize）

**注意：查询行为语义改变（故意）**——从「字段 onChange 即查」改为「仅按钮触发」。这是标准要求。`query` state 仍持有筛选条件，但只在点查询/重置时更新 `query`，不再逐字段 onChange 写 query。

- [ ] **Step 1: 写失败的结构断言测试**

`frontend/src/pages/system/operation-log/__tests__/index.test.tsx`：
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import OperationLogPage from '../index';

// mock API 模块，避免真实网络请求
vi.mock('@/api/auditLog', () => ({
  getOperationLogs: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getOperationLog: vi.fn().mockResolvedValue({}),
}));

// mock 权限包装，直接渲染 children
vi.mock('@/components/RequirePermission', () => ({
  __esModule: true,
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('OperationLogPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在单个 Card 内', async () => {
    render(<OperationLogPage />);
    // Card 存在（arco Card 渲染 .arco-card）
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('查询区用 Form（非裸 Space）', async () => {
    const { container } = render(<OperationLogPage />);
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置两个按钮，且文案正确', async () => {
    render(<OperationLogPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('不再有"刷新"按钮（标准要求改为查询/重置）', async () => {
    render(<OperationLogPage />);
    expect(screen.queryByRole('button', { name: '刷新' })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
cd frontend && npx vitest run src/pages/system/operation-log
```
Expected: FAIL —— 找不到 `.arco-form`、找不到「查询」按钮、「刷新」按钮仍存在。

- [ ] **Step 3: 创建样式文件**

`frontend/src/pages/system/operation-log/style/index.module.less`（标准三段，照抄模板）：
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

.search-form {
  padding-right: 20px;
}

.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}
```

- [ ] **Step 4: 重写页面（保留所有数据逻辑，只换布局层）**

`frontend/src/pages/system/operation-log/index.tsx` 完整新内容：
```tsx
import React, { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, DatePicker, Drawer, Tag, Typography,
  Card, Form, Grid,
} from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import RequirePermission from '@/components/RequirePermission';
import {
  getOperationLogs,
  getOperationLog,
  type OperationLogListItem,
  type OperationLogDetail,
  type OperationLogQuery,
} from '@/api/auditLog';
import styles from './style/index.module.less';

const { Title, Paragraph } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const { RangePicker } = DatePicker;

const RESULT_OPTIONS = [
  { label: '全部', value: '' },
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
];

const MODULE_OPTIONS = [
  { label: '用户', value: 'User' },
  { label: '角色', value: 'Role' },
  { label: '编号', value: 'Numbering' },
  { label: '认证', value: 'Auth' },
];

export default function OperationLogPage() {
  const [formInstance] = Form.useForm();
  const [data, setData] = useState<OperationLogListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState<OperationLogQuery>({ page: 1, pageSize: 10 });
  const [detail, setDetail] = useState<OperationLogDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getOperationLogs(query);
      setData(res.items);
      setTotal(res.total);
    } finally {
      setLoading(false);
    }
  }, [query]);

  useEffect(() => { fetchData(); }, [fetchData]);

  // 标准 2.4：仅按钮触发查询
  const handleSearch = () => {
    const values = formInstance.getFieldsValue();
    setQuery((q) => ({
      ...q,
      keyword: values.keyword || undefined,
      module: values.module || undefined,
      result: values.result || undefined,
      startTime: values.timeRange?.[0] || undefined,
      endTime: values.timeRange?.[1] || undefined,
      page: 1,
    }));
  };

  const handleReset = () => {
    formInstance.resetFields();
    setQuery({ page: 1, pageSize: 10 });
  };

  const onPageChange = (page: number, pageSize: number) =>
    setQuery((q) => ({ ...q, page, pageSize }));

  const openDetail = async (id: string) => {
    const d = await getOperationLog(id);
    setDetail(d);
    setDetailVisible(true);
  };

  const columns = [
    { title: '时间', dataIndex: 'createdAt', width: 170 },
    { title: '用户', dataIndex: 'username', width: 120 },
    { title: '模块', dataIndex: 'module', width: 110 },
    { title: '动作', dataIndex: 'action', width: 120 },
    { title: '目标', dataIndex: 'targetName', width: 140 },
    {
      title: '结果', dataIndex: 'result', width: 90,
      render: (v: string) => v === 'Success'
        ? <Tag color="green">成功</Tag>
        : <Tag color="red">失败</Tag>,
    },
    { title: '状态码', dataIndex: 'statusCode', width: 80 },
    { title: '耗时(ms)', dataIndex: 'durationMs', width: 90 },
    {
      title: '操作', width: 80,
      render: (_: unknown, record: OperationLogListItem) =>
        <Button type="text" size="small" onClick={() => openDetail(record.id)}>详情</Button>,
    },
  ];

  return (
    <RequirePermission resource="system:audit" actions={['view']}>
      <Card>
        <Title heading={6}>操作日志</Title>

        <div className={styles['search-form-wrapper']}>
          <Form
            form={formInstance}
            className={styles['search-form']}
            labelAlign="left"
            labelCol={{ span: 5 }}
            wrapperCol={{ span: 19 }}
          >
            <Row gutter={24}>
              <Col span={8}>
                <FormItem label="关键词" field="keyword">
                  <Input allowClear placeholder="搜索 路径/目标/错误信息" />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="模块" field="module">
                  <Select allowClear placeholder="选择模块" options={MODULE_OPTIONS} />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="结果" field="result">
                  <Select allowClear placeholder="选择结果" options={RESULT_OPTIONS} />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="时间" field="timeRange">
                  <RangePicker showTime style={{ width: '100%' }} />
                </FormItem>
              </Col>
            </Row>
          </Form>
          <div className={styles['right-button']}>
            <Button type="primary" icon={<IconSearch />} onClick={handleSearch}>查询</Button>
            <Button icon={<IconRefresh />} onClick={handleReset}>重置</Button>
          </div>
        </div>

        <div className={styles['button-group']}>
          <Space />
          <Space>
            <Button icon={<IconRefresh />} onClick={fetchData}>刷新</Button>
          </Space>
        </div>

        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          data={data}
          pagination={{
            current: query.page, pageSize: query.pageSize, total,
            onChange: onPageChange, showTotal: true, sizeCanChange: true,
          } as PaginationProps}
        />

        <Drawer
          title="操作日志详情"
          visible={detailVisible}
          width={640}
          onCancel={() => setDetailVisible(false)}
          footer={null}
        >
          {detail && (
            <div>
              {([
                ['时间', detail.createdAt],
                ['用户', `${detail.username} (${detail.userId ?? '-'})`],
                ['模块/动作', `${detail.module} / ${detail.action}`],
                ['目标', detail.targetName ? `${detail.targetName} (${detail.targetId ?? '-'})` : '-'],
                ['请求', `${detail.httpMethod} ${detail.requestPath}`],
                ['状态码', String(detail.statusCode)],
                ['耗时', `${detail.durationMs} ms`],
                ['IP', detail.ipAddress ?? '-'],
                ['TraceId', detail.traceId ?? '-'],
              ] as [string, string][]).map(([k, v]) => (
                <Paragraph key={k} style={{ marginBottom: 8 }}><b>{k}：</b>{v}</Paragraph>
              ))}
              {detail.errorMessage && (
                <Paragraph><b>错误：</b><span style={{ color: 'red' }}>{detail.errorMessage}</span></Paragraph>
              )}
              {detail.requestPayload && (
                <div>
                  <b>请求体（已脱敏）：</b>
                  <pre style={{ background: '#f5f5f5', padding: 8, borderRadius: 4, maxHeight: 200, overflow: 'auto' }}>
                    {detail.requestPayload}
                  </pre>
                </div>
              )}
              {detail.stackTrace && (
                <div>
                  <b>堆栈：</b>
                  <pre style={{ background: '#fff1f0', padding: 8, borderRadius: 4, maxHeight: 240, overflow: 'auto', fontSize: 12 }}>
                    {detail.stackTrace}
                  </pre>
                </div>
              )}
            </div>
          )}
        </Drawer>
      </Card>
    </RequirePermission>
  );
}
```

- [ ] **Step 5: 运行测试，确认通过**

Run:
```bash
cd frontend && npx vitest run src/pages/system/operation-log
```
Expected: 4 个测试全 PASS。

- [ ] **Step 6: 类型检查 + 全量测试 + 构建**

Run:
```bash
cd frontend && npx tsc --noEmit && npm test && npm run build
```
Expected: tsc 无错；测试全绿；build 成功。

- [ ] **Step 7: Commit**

```bash
git add frontend/src/pages/system/operation-log/
git commit -m "refactor(fe): 操作日志页改造为列表查询标准布局（Card+Grid+查询重置按钮）"
```

---

### Task 2: 编号管理页 — 规则配置 tab + 生成日志 tab 两个查询区改造

编号管理页是项目最复杂的页面（731 行，含 3 个 Tabs + 抽屉编辑 + 实时预览）。本任务只改两个查询区（规则配置 tab、生成日志 tab）的布局，**保留**所有数据逻辑、抽屉、预览、Tabs 结构不动。

**现状**：
- 规则配置 tab（`index.tsx:429-484`）：`<Space space-between>` + 3 字段 onChange 自动查 + 无查询/重置按钮 + 新建按钮混在 Space 里
- 生成日志 tab（`index.tsx:508-575`）：`<Space wrap>` + 4 字段 + 查询/重置按钮（已有按钮但布局乱）

**目标**：两个查询区都改为标准布局（Form+Grid 三列 + 外侧查询/重置按钮 div）。新建按钮移到规则 tab 的标准工具栏。

**关键：受控 Form 改造**——现状用散落的 useState（`keyword`/`filterTargetType`/`filterIsActive`/`logFilter`），改为每个查询区用独立的 `Form.useForm()`。规则 tab 用 `ruleForm`，日志 tab 用 `logForm`。

**Files:**
- Create: `frontend/src/pages/system/numbering/style/index.module.less`
- Create: `frontend/src/pages/system/numbering/__tests__/index.test.tsx`
- Modify: `frontend/src/pages/system/numbering/index.tsx`

**Interfaces:**
- Consumes: `getNumberingRules`/`getNumberingLogs` 等接口（不变）；`NumberingRuleListItem`/`NumberingLogItem` 类型（不变）；`useLocale` hook（不变）
- Produces: 标准布局编号管理页；查询参数字段语义不变（keyword/targetType/isActive；targetType/categoryCode/code/dateRange）

- [ ] **Step 1: 写失败的结构断言测试**

`frontend/src/pages/system/numbering/__tests__/index.test.tsx`：
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import NumberingManagement from '../index';

vi.mock('@/api/numbering', () => ({
  getNumberingRules: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getNumberingRule: vi.fn().mockResolvedValue({}),
  createNumberingRule: vi.fn(),
  updateNumberingRule: vi.fn(),
  updateNumberingRuleStatus: vi.fn(),
  getNumberingLogs: vi.fn().mockResolvedValue({ items: [], total: 0 }),
}));
vi.mock('@/api/numberingDictionary', () => ({
  getAllActiveTargetTypes: vi.fn().mockResolvedValue([]),
  getActiveCategories: vi.fn().mockResolvedValue([]),
}));

describe('NumberingManagement — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('规则配置 tab 渲染在 Card 内且有查询表单', async () => {
    render(<NumberingManagement />);
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
    // 默认就是 rules tab
    expect(document.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('规则配置 tab 有查询和重置按钮', async () => {
    render(<NumberingManagement />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('切换到生成日志 tab 也有查询和重置按钮', async () => {
    render(<NumberingManagement />);
    // 点"生成日志"tab（Arco Tabs 用 tab title 文本）
    // 先等 rules tab 的查询按钮出现
    await screen.findByRole('button', { name: '查询' });
    // 找到并点击 logs tab
    const logsTab = screen.getByText('生成日志');
    fireEvent.click(logsTab);
    // logs tab 也有查询按钮（现在会有 2 个，logs tab 的在后面）
    expect(screen.getAllByRole('button', { name: '查询' }).length).toBeGreaterThanOrEqual(2);
  });
});
```

> 注：测试里点击 tab 用中文「生成日志」。实际实现需确认 `locale.ts` 里该 tab 的 title 是不是「生成日志」；若 locale 用了 t['numbering.tab.logs'] 且值是「生成日志」则匹配。实现时若文案不符，按实际 locale 值调整测试断言文案，不要改 locale。

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
cd frontend && npx vitest run src/pages/system/numbering
```
Expected: FAIL —— 找不到 `.arco-card`、规则 tab 无「查询」按钮。

- [ ] **Step 3: 创建样式文件**

`frontend/src/pages/system/numbering/style/index.module.less`（与 Task 1 相同的三段标准样式，照抄）：
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

.search-form {
  padding-right: 20px;
}

.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}
```

- [ ] **Step 4: 改造页面 — 新增两个 Form 实例 + import**

在 `frontend/src/pages/system/numbering/index.tsx`：

4a. 在顶部 import 块加入 `Card`、`Grid`、`IconSearch`：
- 第 2-19 行的 `@arco-design/web-react` import 加入 `Card`、`Grid`
- 第 20 行 `@arco-design/web-react/icon` import 加入 `IconSearch`（如已有则不重复）
- 新增 `import styles from './style/index.module.less';`

修改后的 import 头部：
```tsx
import { useEffect, useState, useCallback, useMemo } from 'react';
import {
  Table, Button, Input, Drawer, Form, Select, InputNumber, Switch, Tag,
  Popconfirm, Message, Space, Tabs, DatePicker, Alert, Typography, Card, Grid,
} from '@arco-design/web-react';
import { IconPlus, IconSearch, IconRefresh } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getNumberingRules, getNumberingRule, createNumberingRule, updateNumberingRule,
  updateNumberingRuleStatus, getNumberingLogs,
  NumberingRuleListItem, NumberingRule, NumberingLogItem, CreateNumberingRuleRequest,
} from '@/api/numbering';
import {
  getAllActiveTargetTypes, getActiveCategories, TargetType,
} from '@/api/numberingDictionary';
import NumberingDictionary from './dict';
import locale from './locale';
import styles from './style/index.module.less';

const FormItem = Form.Item;
const { Row, Col } = Grid;
const { RangePicker } = DatePicker;
const { Text, Title } = Typography;
```

4b. 在 `NumberingManagement` 组件内（第 65 行 `const t = useLocale(locale);` 下方），新增两个 form 实例：
```tsx
  const [ruleForm] = Form.useForm();
  const [logForm] = Form.useForm();
```

- [ ] **Step 5: 改造规则配置 tab 的查询区（替换 429-484 行）**

把原来 429-484 行的 `<Space ...>...</Space>` 整块，替换为：
```tsx
          <div className={styles['search-form-wrapper']}>
            <Form
              form={ruleForm}
              className={styles['search-form']}
              labelAlign="left"
              labelCol={{ span: 5 }}
              wrapperCol={{ span: 19 }}
            >
              <Row gutter={24}>
                <Col span={8}>
                  <FormItem label="关键词" field="keyword">
                    <Input allowClear placeholder={t['numbering.rules.search']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="业务类型" field="targetType">
                    <Select allowClear placeholder={t['numbering.rules.allTargetType']}>
                      {targetTypeOptions.map((tp) => (
                        <Select.Option key={tp.code} value={tp.code}>
                          {tp.nameZh}
                        </Select.Option>
                      ))}
                    </Select>
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="状态" field="isActive">
                    <Select allowClear placeholder={t['numbering.rules.allStatus']}>
                      <Select.Option value="true">{t['numbering.rules.active']}</Select.Option>
                      <Select.Option value="false">{t['numbering.rules.inactive']}</Select.Option>
                    </Select>
                  </FormItem>
                </Col>
              </Row>
            </Form>
            <div className={styles['right-button']}>
              <Button type="primary" icon={<IconSearch />} onClick={() => {
                const v = ruleForm.getFieldsValue();
                setKeyword(v.keyword || '');
                setFilterTargetType(v.targetType);
                setFilterIsActive(
                  v.isActive === undefined ? undefined : v.isActive === 'true',
                );
                setRulePagination((p) => ({ ...p, current: 1 }));
              }}>
                查询
              </Button>
              <Button icon={<IconRefresh />} onClick={() => {
                ruleForm.resetFields();
                setKeyword('');
                setFilterTargetType(undefined);
                setFilterIsActive(undefined);
                setRulePagination((p) => ({ ...p, current: 1 }));
              }}>
                重置
              </Button>
            </div>
          </div>

          <div className={styles['button-group']}>
            <Space>
              <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
                {t['numbering.rules.create']}
              </Button>
            </Space>
            <Space />
          </div>
```

- [ ] **Step 6: 改造生成日志 tab 的查询区（替换 508-575 行）**

把原来 508-575 行的 `<Space wrap>...</Space>` 整块，替换为：
```tsx
          <div className={styles['search-form-wrapper']}>
            <Form
              form={logForm}
              className={styles['search-form']}
              labelAlign="left"
              labelCol={{ span: 5 }}
              wrapperCol={{ span: 19 }}
            >
              <Row gutter={24}>
                <Col span={8}>
                  <FormItem label="业务类型" field="targetType">
                    <Select allowClear placeholder={t['numbering.rules.allTargetType']}>
                      {targetTypeOptions.map((tp) => (
                        <Select.Option key={tp.code} value={tp.code}>
                          {tp.nameZh}
                        </Select.Option>
                      ))}
                    </Select>
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="分类码" field="categoryCode">
                    <Input allowClear placeholder={t['numbering.logs.category.placeholder']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="编号" field="code">
                    <Input allowClear placeholder={t['numbering.logs.code.placeholder']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="时间" field="dateRange">
                    <RangePicker style={{ width: '100%' }} />
                  </FormItem>
                </Col>
              </Row>
            </Form>
            <div className={styles['right-button']}>
              <Button type="primary" icon={<IconSearch />} onClick={() => {
                const v = logForm.getFieldsValue();
                setLogFilter({
                  targetType: v.targetType,
                  categoryCode: v.categoryCode || '',
                  code: v.code || '',
                  dateRange: (v.dateRange as string[]) || [],
                });
                setLogPagination((p) => ({ ...p, current: 1 }));
              }}>
                {t['numbering.logs.search']}
              </Button>
              <Button icon={<IconRefresh />} onClick={() => {
                logForm.resetFields();
                setLogFilter({
                  targetType: undefined,
                  categoryCode: '',
                  code: '',
                  dateRange: [],
                });
                setLogPagination((p) => ({ ...p, current: 1 }));
              }}>
                {t['numbering.logs.reset']}
              </Button>
            </div>
          </div>
```

- [ ] **Step 7: 给整页套 Card（改 return 的最外层）**

把第 424-425 行的：
```tsx
  return (
    <div>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
```
改为：
```tsx
  return (
    <Card>
      <Title heading={6}>编号管理</Title>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
```
并把文件末尾对应的 `</Tabs>` 后的 `</div>`（约 591 行附近的 `</div>`）改为 `</Card>`。注意：Drawer 在 `</Tabs>` 之后、`</Card>` 之前，保持不变（Drawer 应在 Card 内还是外不影响功能，Arco Drawer 用 Portal 渲染，放 Card 内没问题；保持最小改动即可）。

- [ ] **Step 8: 运行测试，确认通过**

Run:
```bash
cd frontend && npx vitest run src/pages/system/numbering
```
Expected: 3 个测试全 PASS。若 tab 文案断言失败，先确认 `locale.ts` 实际值再调整测试断言文案。

- [ ] **Step 9: 类型检查 + 全量测试 + 构建**

Run:
```bash
cd frontend && npx tsc --noEmit && npm test && npm run build
```
Expected: 全绿。

- [ ] **Step 10: Commit**

```bash
git add frontend/src/pages/system/numbering/
git commit -m "refactor(fe): 编号管理两个查询区改造为标准布局（Card+Grid+查询重置按钮）"
```

---

## ⏸️ Phase 1 Checkpoint

Phase 1（Task 0-2）完成后**暂停**，人工验收：
1. `npm run dev` 启动，肉眼检查操作日志页、编号管理两个 tab 的查询区视觉是否符合 Arco Pro 截图样式（Grid 三列、外侧按钮、竖直分隔线）。
2. 验证查询/重置/分页交互正常。
3. 确认无双重 padding、无 `<span/>` hack 残留。

验收通过后再进入 Phase 2。**若发现问题，回到对应 Task 修正，不要带病进入 Phase 2。**

---

## Phase 2：推广剩余页

### Task 3: 登录日志页改造

与操作日志页结构几乎一致（同样有双重 padding bug + 刷新而非重置）。复用 Task 1 的模式。

**Files:**
- Create: `frontend/src/pages/system/login-log/style/index.module.less`
- Create: `frontend/src/pages/system/login-log/__tests__/index.test.tsx`
- Modify: `frontend/src/pages/system/login-log/index.tsx`

**Interfaces:**
- Consumes: 登录日志 API from `@/api/auditLog`（或对应模块）；现有 `query` state 结构
- Produces: 标准布局登录日志页

- [ ] **Step 1: 读现有页面，确认 API 和字段**

Run:
```bash
cd frontend && cat src/pages/system/login-log/index.tsx
```
确认：import 的 API 函数名、`query` state 字段（应为 keyword/event/result/startTime/endTime/page/pageSize）、Drawer 是否存在。记录实际字段名，用于 Step 3。

- [ ] **Step 2: 写失败的结构断言测试**

`frontend/src/pages/system/login-log/__tests__/index.test.tsx`（结构与 Task 1 测试相同，mock 按上一步确认的 API 路径）：
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import LoginLogPage from '../index';

vi.mock('@/api/auditLog', () => ({
  // 按 Step 1 确认的实际导出名调整
  getLoginLogs: vi.fn().mockResolvedValue({ items: [], total: 0 }),
}));
vi.mock('@/components/RequirePermission', () => ({
  __esModule: true,
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('LoginLogPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在 Card 内且有查询表单', async () => {
    const { container } = render(<LoginLogPage />);
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置按钮', async () => {
    render(<LoginLogPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('不再有"刷新"按钮', async () => {
    render(<LoginLogPage />);
    expect(screen.queryByRole('button', { name: '刷新' })).not.toBeInTheDocument();
  });
});
```
> 若 Step 1 发现登录日志 mock 函数名不是 `getLoginLogs`，按实际名称修改上面的 mock。

- [ ] **Step 3: 运行测试确认失败**

Run: `cd frontend && npx vitest run src/pages/system/login-log`
Expected: FAIL。

- [ ] **Step 4: 创建样式文件**

`frontend/src/pages/system/login-log/style/index.module.less`：与 Task 1 Step 3 **完全相同**的三段标准样式（照抄）。

- [ ] **Step 5: 重写页面**

参照 Task 1 Step 4 的完整模式改造 `login-log/index.tsx`：
- 移除外层 `<div style={{ padding: 16 }}>`，改 `<Card>` + `<Title heading={6}>登录日志</Title>`
- 查询区改为标准 `search-form-wrapper` + Form + Grid 三列 + `right-button`
- 「刷新」按钮 → 标准「查询」+「重置」按钮组
- 工具栏 `button-group`（登录日志若无其他操作，左右 Space 都留空或右侧放刷新）
- 保留表格、分页、Drawer 详情等所有逻辑
- 按字段名（Step 1 记录的）填入 FormItem 的 `field`：keyword/event/result/timeRange

字段映射示例（按实际确认调整）：
```tsx
<Col span={8}><FormItem label="关键词" field="keyword"><Input allowClear/></FormItem></Col>
<Col span={8}><FormItem label="事件" field="event"><Select allowClear options={...}/></FormItem></Col>
<Col span={8}><FormItem label="结果" field="result"><Select allowClear options={RESULT_OPTIONS}/></FormItem></Col>
<Col span={8}><FormItem label="时间" field="timeRange"><RangePicker showTime style={{ width: '100%' }}/></FormItem></Col>
```

- [ ] **Step 6: 运行测试 + 类型检查 + 构建 + Commit**

Run: `cd frontend && npx vitest run src/pages/system/login-log && npx tsc --noEmit && npm run build`
Expected: 全绿。
```bash
git add frontend/src/pages/system/login-log/
git commit -m "refactor(fe): 登录日志页改造为标准布局，修复双重 padding"
```

---

### Task 4: 用户管理页改造

用户管理页查询区极简（单 `Input.Search` + 右侧新建按钮）。仍套标准骨架。

**Files:**
- Create: `frontend/src/pages/system/user/style/index.module.less`
- Create: `frontend/src/pages/system/user/__tests__/index.test.tsx`
- Modify: `frontend/src/pages/system/user/index.tsx`

**Interfaces:**
- Consumes: 用户管理 API；现有搜索 state（keyword）
- Produces: 标准布局用户管理页

- [ ] **Step 1: 读现有页面**

Run: `cd frontend && cat src/pages/system/user/index.tsx`
记录：搜索字段名（应为 keyword）、新建按钮 handler、表格列、是否有 Drawer。

- [ ] **Step 2: 写失败测试**

`frontend/src/pages/system/user/__tests__/index.test.tsx`：
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import UserPage from '../index';

vi.mock('@/api/user', () => ({
  // 按实际 API 名调整
  getUsers: vi.fn().mockResolvedValue({ items: [], total: 0 }),
}));
vi.mock('@/components/RequirePermission', () => ({
  __esModule: true,
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('UserPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在 Card 内', async () => {
    render(<UserPage />);
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('有查询和重置按钮', async () => {
    render(<UserPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('有新建按钮在工具栏左侧', async () => {
    render(<UserPage />);
    // 按实际 locale 确认文案，可能是"新增"或"新建"
    expect(await screen.findByRole('button', { name: /新增|新建/ })).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: 运行测试确认失败**

Run: `cd frontend && npx vitest run src/pages/system/user`
Expected: FAIL（无 Card、无查询/重置按钮）。

- [ ] **Step 4: 创建样式文件**

`frontend/src/pages/system/user/style/index.module.less`：与 Task 1 Step 3 完全相同的三段标准样式。

- [ ] **Step 5: 重写页面**

参照标准模板改造：单字段查询区也用 Form+Grid（一个 `Col span={8}` 放 keyword Input），外侧查询/重置按钮。新建按钮移到 `button-group` 左侧 Space。保留表格、分页、Drawer 等逻辑。

- [ ] **Step 6: 测试 + 类型检查 + 构建 + Commit**

Run: `cd frontend && npx vitest run src/pages/system/user && npx tsc --noEmit && npm run build`
Expected: 全绿。
```bash
git add frontend/src/pages/system/user/
git commit -m "refactor(fe): 用户管理页改造为标准布局"
```

---

### Task 5: 无查询区页面套 Card（角色/权限/编号字典）

这三个页面没有查询筛选，只需套 `<Card>` + `<Title>` 骨架，统一视觉。编号字典页额外移除 `<span/>` hack。

**Files:**
- Modify: `frontend/src/pages/system/role/index.tsx`
- Modify: `frontend/src/pages/system/permission/index.tsx`
- Modify: `frontend/src/pages/system/numbering/dict/index.tsx`

**Interfaces:**
- Consumes: 各页现有逻辑（不变）
- Produces: 三个页面都包在 `<Card>` + `<Title>` 内

- [ ] **Step 1: 角色管理页 — 套 Card**

读 `role/index.tsx`，把最外层 `<div>`（或裸返回的 Fragment）改为：
```tsx
<Card>
  <Title heading={6}>角色管理</Title>
  {/* 原有工具栏（新建按钮）移到标准 button-group */}
  <div className={styles['button-group']}>
    <Space>
      <Button type="primary" icon={<IconPlus />} onClick={...}>新建</Button>
    </Space>
    <Space />
  </div>
  {/* Table 原样保留 */}
</Card>
```
需要在 role 页也建 `style/index.module.less`（含 `.button-group` 一段即可）。import 加入 `Card, Typography` + `import styles from './style/index.module.less'`。

- [ ] **Step 2: 权限管理页 — 套 Card**

读 `permission/index.tsx`（最简单的页，裸 `<Table>`）。改为：
```tsx
<Card>
  <Title heading={6}>权限管理</Title>
  <Table ...原样... />
</Card>
```
import 加入 `Card, Typography`。无需样式文件（无工具栏）。

- [ ] **Step 3: 编号字典页 — 套 Card + 移除 span hack**

读 `numbering/dict/index.tsx`。找到用 `<span />` 占位 hack 撑开右对齐的两处（约 253-258、287-292 行的 `<Space justifyContent="space-between"><span/>...<Button></Space>`），替换为标准 `button-group` 模式：
```tsx
<div className={styles['button-group']}>
  <Space />
  <Space>
    <Button type="primary" icon={<IconPlus />} onClick={...}>新增</Button>
  </Space>
</div>
```
最外层套 `<Card>` + `<Title heading={6}>业务字典</Title>`。建 `style/index.module.less`（含 `.button-group`）。import 加入 `Card, Typography` + styles。

> 注：编号字典页是 master-detail 结构（业务类型表 + 分类表），两个 section 各有标题和工具栏，改造时两个工具栏都用 `button-group`，section 标题保留原 `<div style={{ fontWeight: 600 }}>` 或也换成更统一的写法（保持最小改动，只换按钮区结构）。

- [ ] **Step 4: 类型检查 + 全量测试 + 构建**

Run:
```bash
cd frontend && npx tsc --noEmit && npm test && npm run build
```
Expected: 全绿（这三个页无新增测试，因为只是套容器；现有测试不受影响）。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/system/role/ frontend/src/pages/system/permission/ frontend/src/pages/system/numbering/dict/
git commit -m "refactor(fe): 角色/权限/编号字典页套 Card 标准容器，移除 span hack"
```

---

## 完成验收

全部 Task 完成后：
- [ ] **Final: 全量验证**

Run:
```bash
cd frontend && npm test && npx tsc --noEmit && npm run build
```
Expected: 所有测试通过、类型无错、构建成功。

- [ ] **Final: 人工视觉检查**

`npm run dev`，逐页检查 7 个列表页的查询区视觉一致性，对照 Arco Pro 截图确认：Grid 三列对齐、外侧查询/重置按钮、竖直分隔线、单 Card 容器、无双重 padding。

- [ ] **Final: Commit（若有最终修正）**

```bash
git add -A
git commit -m "refactor(fe): 列表查询页标准迁移完成（7 页全部对齐）"
```
