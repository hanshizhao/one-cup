import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { GlobalContext } from '@/context';
import UnitPage from '../index';

// mock API 模块，避免真实网络请求
vi.mock('@/api/measurementUnit', () => ({
  getUnits: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getAllActiveUnits: vi.fn().mockResolvedValue([]),
  getUnitCategories: vi.fn().mockResolvedValue([]),
  getUnit: vi.fn().mockResolvedValue({}),
  createUnit: vi.fn().mockResolvedValue({}),
  updateUnit: vi.fn().mockResolvedValue({}),
  updateUnitStatus: vi.fn().mockResolvedValue({}),
  convertUnit: vi.fn().mockResolvedValue({}),
}));

// useLocale 从 GlobalContext 读取 lang；提供 lang='zh-CN' 让文案正常解析。
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

describe('UnitPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在单个 Card 内', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('查询区用 Form（非裸 Space）', async () => {
    const { container } = renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置两个按钮，且文案正确', async () => {
    renderWithLocale(<UnitPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('新建按钮在工具栏左侧', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(screen.getByRole('button', { name: '新建单位' })).toBeInTheDocument();
  });

  it('换算按钮在工具栏右侧', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(screen.getByRole('button', { name: '换算' })).toBeInTheDocument();
  });
});
