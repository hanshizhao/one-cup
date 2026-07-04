import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { GlobalContext } from '@/context';
import UserPage from '../index';

// mock API 模块，避免真实网络请求
vi.mock('@/api/user', () => ({
  getUserList: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getUserById: vi.fn().mockResolvedValue({}),
  createUser: vi.fn().mockResolvedValue({}),
  updateUser: vi.fn().mockResolvedValue({}),
  resetPassword: vi.fn().mockResolvedValue({}),
  updateUserStatus: vi.fn().mockResolvedValue({}),
}));

// mock 角色列表 API（页面加载时拉取角色选项）
vi.mock('@/api/role', () => ({
  getRoleList: vi.fn().mockResolvedValue([]),
}));

// useLocale 从 GlobalContext 读取 lang；测试环境无 provider，默认值为 {}，
// 会导致所有 t[...] 文案为 undefined（标题渲染为空）。
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

describe('UserPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在单个 Card 内', async () => {
    renderWithLocale(<UserPage />);
    // 等待异步 effect（fetchData）落定，避免 act() 警告
    await screen.findByText('用户管理');
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('查询区用 Form（非裸 Space）', async () => {
    const { container } = renderWithLocale(<UserPage />);
    await screen.findByText('用户管理');
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置两个按钮，且文案正确', async () => {
    renderWithLocale(<UserPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('新建按钮（新增用户）在工具栏左侧', async () => {
    renderWithLocale(<UserPage />);
    await screen.findByText('用户管理');
    expect(screen.getByRole('button', { name: '新增用户' })).toBeInTheDocument();
  });
});
