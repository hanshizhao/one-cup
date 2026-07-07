import { Descriptions, Drawer, Popover, Tag } from '@arco-design/web-react';
import { IconRight } from '@arco-design/web-react/icon';
import {
  EquipmentDto,
  EquipmentTypeDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';
import TemplateValuePopover from './TemplateValuePopover';

interface Props {
  visible: boolean;
  data: EquipmentDto | null;
  /** 所属类型的完整数据（含参数定义 + 模板列表），由父组件额外 fetch 传入，用于关联区展示 */
  typeDetailData?: EquipmentTypeDto | null;
  onClose: () => void;
}

/**
 * 设备详情 Drawer（优化版）：12 字段分 4 组 + 关联区（所属类型 / 可运行模板）。
 * 关联区为纯只读快照——浏览场景不打断、不跳转编辑页；
 * 点击类型卡片 / 模板项用 Popover 弹出详情，不离开当前抽屉。
 */
export default function EquipmentDetailDrawer({
  visible,
  data,
  typeDetailData,
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

  // Descriptions label 列统一样式：浅灰背景 + 固定宽度
  const descLabelStyle = {
    background: 'var(--color-fill-2)',
    width: 100,
  } as React.CSSProperties;

  // 分组标题
  const GroupTitle = ({ children }: { children: React.ReactNode }) => (
    <div className={styles['detail-group-title']}>{children}</div>
  );

  // 类型卡片 Popover：展示该类型的参数定义（只读）
  const paramColor = (vt: string) =>
    vt === 'Number' ? 'arcoblue' : vt === 'Enum' ? 'green' : 'gray';

  const paramConstraint = (p: { valueType: string; minValue?: string | null; maxValue?: string | null; unitSymbol?: string | null; options?: string[] | null }) => {
    if (p.valueType === 'Number') {
      return `${p.minValue ?? '−'} ~ ${p.maxValue ?? '+'} ${p.unitSymbol || ''}`;
    }
    if (p.valueType === 'Enum') {
      return (p.options || []).join('/') || t['equipment.type.param.preview.noOptions'];
    }
    return t['equipment.type.param.preview.unnamed'];
  };

  const typePopoverContent = typeDetailData?.parameters?.length ? (
    <div className={styles['mini-param-grid']}>
      {typeDetailData.parameters.map((p) => (
        <div key={p.id} className={styles['mini-param']}>
          <div className={styles['mini-param-name']}>
            {p.name}
            {p.required && <span style={{ color: 'var(--color-danger-6)' }}>*</span>}
          </div>
          <div className={styles['mini-param-constraint']}>
            <Tag size="small" color={paramColor(p.valueType)} style={{ marginRight: 4 }}>
              {t[`equipment.type.param.valueType.${p.valueType.toLowerCase()}`]}
            </Tag>
            {paramConstraint(p)}
          </div>
        </div>
      ))}
    </div>
  ) : (
    <span style={{ color: 'var(--color-text-4)' }}>{t['equipment.type.param.preview.empty']}</span>
  );

  return (
    <Drawer
      title={t['equipment.item.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <>
          {/* 分组 1：基础信息 */}
          <GroupTitle>{t['equipment.item.detail.group.base']}</GroupTitle>
          <Descriptions
            column={1}
            border
            labelStyle={descLabelStyle}
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
          <GroupTitle>{t['equipment.item.detail.group.runStatus']}</GroupTitle>
          <Descriptions
            column={1}
            border
            labelStyle={descLabelStyle}
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
                value: (
                  <Tag color={data.isActive ? 'green' : 'gray'}>
                    {data.isActive ? t['equipment.item.active'] : t['equipment.item.inactive']}
                  </Tag>
                ),
              },
            ]}
            style={{ marginBottom: 20 }}
          />

          {/* 分组 3：资产时间 */}
          <GroupTitle>{t['equipment.item.detail.group.assetTime']}</GroupTitle>
          <Descriptions
            column={1}
            border
            labelStyle={descLabelStyle}
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
          <GroupTitle>{t['equipment.item.detail.group.remark']}</GroupTitle>
          <Descriptions
            column={1}
            border
            labelStyle={descLabelStyle}
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
            {/* 所属类型卡片：点击 Popover 展示参数定义 */}
            {typeDetailData && (
              <Popover
                trigger="click"
                position="top"
                title={t['equipment.item.detail.relation.paramList']}
                content={typePopoverContent}
              >
                <div className={styles['relation-link']}>
                  <div className={styles['relation-link-icon']}>
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.6">
                      <rect x="3" y="8" width="18" height="10" rx="2" />
                      <path d="M7 8V5h10v3M7 18v2M17 18v2" />
                    </svg>
                  </div>
                  <div className={styles['relation-link-text']}>
                    <div className={styles['relation-link-title']}>
                      {t['equipment.item.detail.relation.type']}：{data.equipmentTypeName}
                    </div>
                    <div className={styles['relation-link-sub']}>
                      {`${typeDetailData.code} · ${typeDetailData.parameters?.length || 0} ${t['equipment.item.detail.relation.params']} · ${typeDetailData.templates?.length || 0} ${t['equipment.item.detail.relation.templates']}`}
                    </div>
                  </div>
                  <IconRight className={styles['relation-link-arrow']} />
                </div>
              </Popover>
            )}

            {/* 可运行模板列表：点击 Popover 展示模板参数值 */}
            {typeDetailData && typeDetailData.templates && typeDetailData.templates.length > 0 && (
              <div>
                <div className={styles['relation-sub-title']}>
                  {t['equipment.item.detail.relation.templateList']}
                </div>
                <div className={styles['relation-template-list']}>
                  {typeDetailData.templates.map((tpl) => (
                    <TemplateValuePopover
                      key={tpl.id}
                      typeId={data.equipmentTypeId}
                      templateId={tpl.id}
                      templateName={tpl.name}
                    >
                      <div className={styles['relation-template-item']}>
                        <div>
                          <div className={styles['relation-template-name']}>{tpl.name}</div>
                          <div className={styles['relation-template-meta']}>
                            {t['equipment.template.form.process']}：{tpl.processName || '-'}
                          </div>
                        </div>
                        <IconRight className={styles['relation-template-arrow']} />
                      </div>
                    </TemplateValuePopover>
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
