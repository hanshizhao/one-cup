import { Descriptions, Drawer, Tag, Typography } from '@arco-design/web-react';
import { IconRight } from '@arco-design/web-react/icon';
import {
  EquipmentDto,
  EquipmentTypeDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';

const { Title } = Typography;

interface Props {
  visible: boolean;
  data: EquipmentDto | null;
  /** 所属类型的完整数据（含参数定义 + 模板列表），由父组件额外 fetch 传入，用于关联区展示 */
  typeDetailData?: EquipmentTypeDto | null;
  /** 点击"可运行模板"项的回调（打开模板编辑） */
  onEditTemplate?: (templateId: string) => void;
  onClose: () => void;
}

/**
 * 设备详情 Drawer（优化版）：12 字段分 4 组 + 关联区（所属类型 / 参数定义 / 可运行模板）。
 */
export default function EquipmentDetailDrawer({
  visible,
  data,
  typeDetailData,
  onEditTemplate,
  onClose,
}: Props) {
  const t = useLocale(locale);

  const statusLed = (status: string) => {
    const cls =
      status === 'Running' ? styles['led-run'] : status === 'Maintenance' ? styles['led-fix'] : styles['led-stop'];
    return <span className={`${styles['status-led']} ${cls}`} />;
  };

  // 保修是否过期
  const warrantyExpired =
    data?.warrantyExpiry && new Date(data.warrantyExpiry) < new Date();

  return (
    <Drawer
      title={t['equipment.item.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={560}
    >
      {data && (
        <>
          {/* 分组 1：基础信息 */}
          <Title heading={6} style={{ marginBottom: 8 }}>
            {t['equipment.item.detail.group.base']}
          </Title>
          <Descriptions
            column={1}
            data={[
              { label: t['equipment.item.column.code'], value: data.code },
              { label: t['equipment.item.column.name'], value: data.name },
              { label: t['equipment.item.column.spec'], value: data.specification || '-' },
              { label: t['equipment.item.column.supplier'], value: data.supplier || '-' },
              { label: t['equipment.item.column.location'], value: data.location || '-' },
            ]}
            style={{ marginBottom: 20 }}
          />

          {/* 分组 2：运行状态 */}
          <Title heading={6} style={{ marginBottom: 8 }}>
            {t['equipment.item.detail.group.runStatus']}
          </Title>
          <Descriptions
            column={1}
            data={[
              {
                label: t['equipment.item.column.status'],
                value: (
                  <span>
                    {statusLed(data.status)}
                    <Tag
                      size="small"
                      color={
                        data.status === 'Running'
                          ? 'green'
                          : data.status === 'Maintenance'
                          ? 'orange'
                          : 'gray'
                      }
                      style={{ marginLeft: 6 }}
                    >
                      {t[`equipment.item.status.${data.status.toLowerCase()}`]}
                    </Tag>
                  </span>
                ),
              },
              {
                label: t['equipment.item.column.status2'],
                value: data.isActive ? t['equipment.item.active'] : t['equipment.item.inactive'],
              },
            ]}
            style={{ marginBottom: 20 }}
          />

          {/* 分组 3：资产时间 */}
          <Title heading={6} style={{ marginBottom: 8 }}>
            {t['equipment.item.detail.group.assetTime']}
          </Title>
          <Descriptions
            column={1}
            data={[
              { label: t['equipment.item.form.purchaseDate'], value: data.purchaseDate || '-' },
              {
                label: t['equipment.item.form.warrantyExpiry'],
                value: warrantyExpired ? (
                  <span style={{ color: 'var(--color-warning-6)' }}>
                    {data.warrantyExpiry}（{t['equipment.item.detail.warrantyExpired']}）
                  </span>
                ) : (
                  data.warrantyExpiry || '-'
                ),
              },
              { label: t['equipment.item.column.createdAt'], value: data.createdAt },
            ]}
            style={{ marginBottom: 20 }}
          />

          {/* 分组 4：备注 */}
          <Title heading={6} style={{ marginBottom: 8 }}>
            {t['equipment.item.detail.group.remark']}
          </Title>
          <Descriptions
            column={1}
            data={[
              { label: t['equipment.item.form.remark'], value: data.remark || '-' },
            ]}
            style={{ marginBottom: 24 }}
          />

          {/* 关联区 */}
          <div className={styles['detail-relation-title']}>
            {t['equipment.item.detail.relation.title']}
          </div>
          <div className={styles['relation-card']}>
            {/* 所属类型 */}
            <div className={styles['relation-link']}>
              <div className={styles['relation-link-text']}>
                <div className={styles['relation-link-title']}>
                  {t['equipment.item.detail.relation.type']}：{data.equipmentTypeName}
                </div>
                <div className={styles['relation-link-sub']}>
                  {typeDetailData
                    ? `${typeDetailData.code} · ${typeDetailData.parameters?.length || 0} ${t['equipment.item.detail.relation.params']} · ${typeDetailData.templates?.length || 0} ${t['equipment.item.detail.relation.templates']}`
                    : data.equipmentTypeName}
                </div>
              </div>
            </div>

            {/* 参数定义 mini 网格 */}
            {typeDetailData && typeDetailData.parameters && typeDetailData.parameters.length > 0 && (
              <div style={{ marginTop: 12 }}>
                <div className={styles['relation-sub-title']}>
                  {t['equipment.item.detail.relation.paramList']}
                </div>
                <div className={styles['mini-param-grid']}>
                  {typeDetailData.parameters.map((p) => (
                    <div key={p.id} className={styles['mini-param']}>
                      <div className={styles['mini-param-name']}>
                        {p.name}
                        {p.required && <span style={{ color: 'var(--color-danger-6)' }}>*</span>}
                      </div>
                      <div className={styles['mini-param-constraint']}>
                        <Tag
                          size="small"
                          color={
                            p.valueType === 'Number'
                              ? 'arcoblue'
                              : p.valueType === 'Enum'
                              ? 'green'
                              : 'gray'
                          }
                          style={{ marginRight: 4 }}
                        >
                          {t[`equipment.type.param.valueType.${p.valueType.toLowerCase()}`]}
                        </Tag>
                        {p.valueType === 'Number'
                          ? `${p.minValue ?? '−'} ~ ${p.maxValue ?? '+'} ${p.unitSymbol || ''}`
                          : p.valueType === 'Enum'
                          ? (p.options || []).join('/')
                          : t['equipment.type.param.preview.unnamed']}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* 可运行模板 */}
            {typeDetailData && typeDetailData.templates && typeDetailData.templates.length > 0 && (
              <div style={{ marginTop: 12 }}>
                <div className={styles['relation-sub-title']}>
                  {t['equipment.item.detail.relation.templateList']}
                </div>
                <div className={styles['relation-template-list']}>
                  {typeDetailData.templates.map((tpl) => (
                    <a
                      key={tpl.id}
                      className={styles['relation-template-item']}
                      onClick={() => onEditTemplate?.(tpl.id)}
                    >
                      <div>
                        <div className={styles['relation-template-name']}>{tpl.name}</div>
                        <div className={styles['relation-template-meta']}>
                          {t['equipment.template.form.process']}：{tpl.processName || '-'}
                        </div>
                      </div>
                      <IconRight style={{ color: 'var(--color-text-4)' }} />
                    </a>
                  ))}
                </div>
              </div>
            )}
          </div>
        </>
      )}
    </Drawer>
  );
}
