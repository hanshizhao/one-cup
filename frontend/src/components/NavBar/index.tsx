import React, { useContext } from 'react';
import {
  Tooltip,
  Input,
  Avatar,
  Select,
  Dropdown,
  Menu,
  Message,
} from '@arco-design/web-react';
import {
  IconLanguage,
  IconSunFill,
  IconMoonFill,
  IconPoweroff,
  IconLoading,
} from '@arco-design/web-react/icon';
import { useAppSelector } from '@/store';
import { GlobalContext } from '@/context';
import useLocale from '@/utils/useLocale';
import Logo from '@/assets/logo.svg';
import IconButton from './IconButton';
import styles from './style/index.module.less';
import defaultLocale from '@/locale';
import { logout as logoutApi } from '@/api/auth';
import { removeTokens } from '@/utils/token';

function Navbar({ show }: { show: boolean }) {
  const t = useLocale();
  const { userInfo, userLoading } = useAppSelector((state) => state.userInfo);

  const { setLang, lang, theme, setTheme } = useContext(GlobalContext);

  function logout() {
    logoutApi()
      .catch(() => {})
      .finally(() => {
        removeTokens();
        window.location.href = '/login';
      });
  }

  function onMenuItemClick(key: string) {
    if (key === 'logout') {
      logout();
    }
  }

  const droplist = (
    <Menu onClickMenuItem={onMenuItemClick}>
      <Menu.Item key="logout">
        <IconPoweroff className={styles['dropdown-icon']} />
        {t['navbar.logout']}
      </Menu.Item>
    </Menu>
  );

  return (
    <div className={styles.navbar}>
      <div className={styles.left}>
        <div className={styles.logo}>
          <Logo />
          <div className={styles['logo-name']}>OneCup</div>
        </div>
      </div>
      <ul className={styles.right}>
        <li>
          <Input.Search
            className={styles.round}
            placeholder={t['navbar.search.placeholder']}
          />
        </li>
        <li>
          <Select
            triggerElement={<IconButton icon={<IconLanguage />} />}
            options={[
              { label: '中文', value: 'zh-CN' },
              { label: 'English', value: 'en-US' },
            ]}
            value={lang}
            triggerProps={{
              autoAlignPopupWidth: false,
              autoAlignPopupMinWidth: true,
              position: 'br',
            }}
            trigger="hover"
            onChange={(value: string) => {
              setLang?.(value);
              const nextLang = defaultLocale[value as keyof typeof defaultLocale];
              Message.info(`${nextLang['message.lang.tips']}${value}`);
            }}
          />
        </li>
        <li>
          <Tooltip
            content={
              theme === 'light'
                ? t['settings.navbar.theme.toDark']
                : t['settings.navbar.theme.toLight']
            }
          >
            <IconButton
              icon={theme !== 'dark' ? <IconMoonFill /> : <IconSunFill />}
              onClick={() => setTheme?.(theme === 'light' ? 'dark' : 'light')}
            />
          </Tooltip>
        </li>
        {userInfo && (
          <li>
            <Dropdown droplist={droplist} position="br" disabled={userLoading}>
              <Avatar size={32} style={{ cursor: 'pointer' }}>
                {userLoading ? (
                  <IconLoading />
                ) : (
                  <span>{(userInfo.name || 'U').slice(0, 1).toUpperCase()}</span>
                )}
              </Avatar>
            </Dropdown>
          </li>
        )}
      </ul>
    </div>
  );
}

export default Navbar;
