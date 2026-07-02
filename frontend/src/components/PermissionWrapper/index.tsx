import React, { useMemo } from 'react';
import { useAppSelector } from '@/store';
import authentication, { AuthParams } from '@/utils/authentication';

type PermissionWrapperProps = AuthParams & {
  backup?: React.ReactNode;
};

const PermissionWrapper = (
  props: React.PropsWithChildren<PermissionWrapperProps>
) => {
  const { backup, requiredPermissions, oneOfPerm } = props;
  const userInfo = useAppSelector((state) => state.userInfo.userInfo);

  const hasPermission = useMemo(() => {
    return authentication(
        {requiredPermissions, oneOfPerm},
        userInfo.permissions
    );
  },[oneOfPerm, requiredPermissions, userInfo.permissions]);

  if (hasPermission) {
    return <>{convertReactElement(props.children)}</>;
  }
  if (backup) {
    return <>{convertReactElement(backup)}</>;
  }
  return null;
};

function convertReactElement(node: React.ReactNode): React.ReactElement {
  if (!React.isValidElement(node)) {
    return <>{node}</>;
  }
  return node;
}

export default PermissionWrapper;
