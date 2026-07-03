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
    // 等待异步 effect（fetchData）落定，避免 act() 警告
    await screen.findByText('操作日志');
    // Card 存在（arco Card 渲染 .arco-card）
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('查询区用 Form（非裸 Space）', async () => {
    const { container } = render(<OperationLogPage />);
    await screen.findByText('操作日志');
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置两个按钮，且文案正确', async () => {
    render(<OperationLogPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('工具栏保留"刷新"按钮（手动刷新数据，与查询/重置分属不同区）', async () => {
    render(<OperationLogPage />);
    expect(await screen.findByRole('button', { name: '刷新' })).toBeInTheDocument();
  });
});
