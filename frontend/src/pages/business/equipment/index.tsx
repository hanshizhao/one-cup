import { useSearchParams } from 'react-router-dom';
import { Card, Tabs, Typography } from '@arco-design/web-react';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import EquipmentTab from './equipment/EquipmentTab';
import TypeTab from './type/TypeTab';
import TemplateTab from './template/TemplateTab';

const { Title } = Typography;

/**
 * 设备管理容器页：双 Tab。
 * - 「设备」：设备实例（EquipmentTab）
 * - 「设备类型」：设备类型 + 参数定义 + 运行模板（TypeTab，权限守 equipment-type.read）
 *
 * 本文件解析 router.tsx 中 `lazy(() => import('@/pages/business/equipment'))` 的默认导出。
 */
export default function EquipmentPage() {
  const t = useLocale(locale);
  // Tab 状态持久化到 URL query（?tab=equipment|type），刷新/返回可恢复
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = searchParams.get('tab');
  const activeTab = tab === 'type' ? 'type' : tab === 'template' ? 'template' : 'equipment';
  const setActiveTab = (key: string) => {
    // 函数式更新：只改 tab，保留其它 query 参数（如未来列表页的 ?page=）
    setSearchParams(
      (prev) => {
        prev.set('tab', key);
        return prev;
      },
      { replace: true }
    );
  };

  return (
    <Card>
      <Title heading={6}>{t['equipment.title']}</Title>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
        <Tabs.TabPane key="equipment" title={t['equipment.tab.equipment']}>
          <EquipmentTab />
        </Tabs.TabPane>
        <Tabs.TabPane
          key="type"
          title={t['equipment.tab.type']}
        >
          <PermissionWrapper
            requiredPermissions={[{ resource: 'equipment-type', actions: ['read'] }]}
          >
            <TypeTab />
          </PermissionWrapper>
        </Tabs.TabPane>
        <Tabs.TabPane
          key="template"
          title={t['equipment.tab.template']}
        >
          <PermissionWrapper
            requiredPermissions={[{ resource: 'equipment-type', actions: ['read'] }]}
          >
            <TemplateTab />
          </PermissionWrapper>
        </Tabs.TabPane>
      </Tabs>
    </Card>
  );
}
