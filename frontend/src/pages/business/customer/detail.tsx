import { Descriptions, Drawer } from '@arco-design/web-react';
import { CustomerDetail } from '@/api/customer';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function CustomerDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: CustomerDetail | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['customer.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['customer.column.code'], value: data.code },
            { label: t['customer.column.name'], value: data.name },
            { label: t['customer.column.shortName'], value: data.shortName || '-' },
            { label: t['customer.column.contactPerson'], value: data.contactPerson || '-' },
            { label: t['customer.column.contactPhone'], value: data.contactPhone || '-' },
            { label: t['customer.form.remark'], value: data.remark || '-' },
            {
              label: t['customer.column.status'],
              value: data.isActive ? t['customer.active'] : t['customer.inactive'],
            },
            { label: t['customer.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
