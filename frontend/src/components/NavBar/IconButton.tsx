import React, { forwardRef } from 'react';
import { Button } from '@arco-design/web-react';
import styles from './style/icon-button.module.less';
import cs from 'classnames';

type IconButtonProps = {
  icon?: React.ReactNode;
  className?: string;
  onClick?: (e: Event) => void;
};

function IconButton(props: IconButtonProps, ref: React.Ref<HTMLButtonElement>) {
  const { icon, className, onClick } = props;

  return (
    <Button
      ref={ref}
      icon={icon}
      shape="circle"
      type="secondary"
      className={cs(styles['icon-button'], className)}
      onClick={onClick}
    />
  );
}

export default forwardRef(IconButton);
