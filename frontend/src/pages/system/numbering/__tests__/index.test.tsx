import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { GlobalContext } from '@/context';
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

// useLocale 从 GlobalContext 读取 lang；测试环境无 provider，默认值为 {}，
// 会导致所有 t[...] 文案为 undefined（tab 标题渲染为空）。
// 提供 lang='zh-CN' 让页面文案按 zh-CN locale 解析。
// PermissionWrapper 通过 useAppSelector 读权限；布局测试需所有按钮可见，
// 故注入通配权限 {'*':['*']}（admin 语义），让写操作按钮全部渲染。
const renderWithLocale = (ui: React.ReactElement) => {
  const store = configureStore({
    reducer: () => ({ userInfo: { userInfo: { permissions: { '*': ['*'] } } } }),
  });
  return render(
    <Provider store={store}>
      <GlobalContext.Provider value={{ lang: 'zh-CN' }}>
        {ui}
      </GlobalContext.Provider>
    </Provider>,
  );
};

describe('NumberingManagement — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('规则配置 tab 渲染在 Card 内且有查询表单', async () => {
    renderWithLocale(<NumberingManagement />);
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
    // 默认就是 rules tab
    expect(document.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('规则配置 tab 有查询和重置按钮', async () => {
    renderWithLocale(<NumberingManagement />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('切换到生成日志 tab 也有查询和重置按钮', async () => {
    renderWithLocale(<NumberingManagement />);
    // 先等 rules tab 的查询按钮出现
    await screen.findByRole('button', { name: '查询' });
    // logs tab 特有字段（规则 tab 没有"分类码"），点击前不应可见
    expect(screen.queryByText('分类码')).not.toBeInTheDocument();
    // 点击"生成日志" tab（Arco Tabs 用 tab title 文本切换）
    const logsTab = screen.getByText('生成日志');
    fireEvent.click(logsTab);
    // 切换后 logs tab 成为当前面板：分类码字段出现，证明切换成功
    await screen.findByText('分类码');
    // logs tab 也有查询和重置按钮（rules tab 此刻 display:none，故 getByRole 命中的是 logs tab 的按钮）
    expect(screen.getByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });
});
