import { Descriptions, Drawer } from '@arco-design/web-react';
import { MaterialDetail } from '@/api/material';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function MaterialDetailDrawer({
  visible,
  data,
  unitMap,
  onClose,
}: {
  visible: boolean;
  data: MaterialDetail | null;
  unitMap: Record<string, string>;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['material.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['material.column.code'], value: data.code },
            { label: t['material.column.name'], value: data.name },
            { label: t['material.column.spec'], value: data.spec },
            { label: t['material.column.category'], value: data.category },
            {
              label: t['material.column.unit'],
              value: data.unitId ? unitMap[data.unitId] ?? '-' : '-',
            },
            { label: t['material.column.sortOrder'], value: data.sortOrder },
            { label: t['material.form.remark'], value: data.remark || '-' },
            {
              label: t['material.column.status'],
              value: data.isActive ? t['material.active'] : t['material.inactive'],
            },
            { label: t['material.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
