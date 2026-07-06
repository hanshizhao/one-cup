import { Descriptions, Drawer } from '@arco-design/web-react';
import { EquipmentDto } from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';

export default function EquipmentDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: EquipmentDto | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['equipment.item.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['equipment.item.column.code'], value: data.code },
            { label: t['equipment.item.column.name'], value: data.name },
            { label: t['equipment.item.column.type'], value: data.equipmentTypeName },
            {
              label: t['equipment.item.column.status'],
              value: t[`equipment.item.status.${data.status.toLowerCase()}`],
            },
            { label: t['equipment.item.column.spec'], value: data.specification || '-' },
            { label: t['equipment.item.column.supplier'], value: data.supplier || '-' },
            { label: t['equipment.item.column.location'], value: data.location || '-' },
            { label: t['equipment.item.form.purchaseDate'], value: data.purchaseDate || '-' },
            {
              label: t['equipment.item.form.warrantyExpiry'],
              value: data.warrantyExpiry || '-',
            },
            { label: t['equipment.item.form.remark'], value: data.remark || '-' },
            {
              label: t['equipment.item.column.status2'],
              value: data.isActive
                ? t['equipment.item.active']
                : t['equipment.item.inactive'],
            },
            { label: t['equipment.item.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
