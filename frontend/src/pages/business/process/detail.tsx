import { Descriptions, Drawer } from '@arco-design/web-react';
import { ProcessDetail } from '@/api/process';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function ProcessDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: ProcessDetail | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['process.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['process.column.code'], value: data.code },
            { label: t['process.column.name'], value: data.name },
            { label: t['process.column.category'], value: data.category || '-' },
            { label: t['process.column.sortOrder'], value: data.sortOrder },
            { label: t['process.form.remark'], value: data.remark || '-' },
            {
              label: t['process.column.status'],
              value: data.isActive ? t['process.active'] : t['process.inactive'],
            },
            { label: t['process.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
