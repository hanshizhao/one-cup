import { describe, it, expect, beforeEach } from 'vitest';
import { store, setUserInfo, setSettings } from '../index';
import defaultSettings from '../../settings.json';

/**
 * 直接在导出的单例 store 上测两个 reducer。
 * - setUserInfo:Object.assign 合并语义(偏更新);
 * - setSettings:返回 payload(整体替换)。
 * 两个 reducer 结果均由 payload 完全决定。用 beforeEach 重置避免跨用例污染。
 */

describe('userInfo slice — setUserInfo reducer', () => {
  beforeEach(() => {
    store.dispatch(
      setUserInfo({
        userInfo: { permissions: {} },
        userLoading: false,
      })
    );
  });

  it('setUserInfo 更新 userInfo(含 name + permissions)', () => {
    store.dispatch(
      setUserInfo({
        userInfo: {
          name: 'Alice',
          permissions: { 'system:user': ['manage'] },
        },
        userLoading: false,
      })
    );
    expect(store.getState().userInfo).toEqual({
      userInfo: {
        name: 'Alice',
        permissions: { 'system:user': ['manage'] },
      },
      userLoading: false,
    });
  });

  it('setUserInfo 更新 userLoading', () => {
    store.dispatch(setUserInfo({ userLoading: true }));
    expect(store.getState().userInfo.userLoading).toBe(true);
  });

  it('setUserInfo 同时更新 userInfo + userLoading', () => {
    store.dispatch(
      setUserInfo({
        userInfo: { name: 'Bob', permissions: { fabric: ['read'] } },
        userLoading: true,
      })
    );
    const state = store.getState().userInfo;
    expect(state.userInfo.name).toBe('Bob');
    expect(state.userInfo.permissions).toEqual({ fabric: ['read'] });
    expect(state.userLoading).toBe(true);
  });

  it('setUserInfo 偏更新(Object.assign 合并语义,保留未传字段)', () => {
    store.dispatch(
      setUserInfo({
        userInfo: { name: 'Carol', permissions: { a: ['x'] } },
        userLoading: true,
      })
    );
    // 仅传 userLoading,userInfo 应保留(Object.assign 不清空未传字段)
    store.dispatch(setUserInfo({ userLoading: false }));
    const state = store.getState().userInfo;
    expect(state.userLoading).toBe(false);
    expect(state.userInfo.name).toBe('Carol');
  });
});

describe('settings slice — setSettings reducer', () => {
  beforeEach(() => {
    store.dispatch(setSettings(defaultSettings));
  });

  it('setSettings 整体替换 settings(返回 payload 语义)', () => {
    const next = {
      colorWeek: true,
      navbar: false,
      menu: false,
      footer: false,
      themeColor: '#FF0000',
      menuWidth: 100,
    };
    store.dispatch(setSettings(next));
    expect(store.getState().settings).toEqual(next);
  });

  it('setSettings 切换 themeColor 生效', () => {
    store.dispatch(setSettings({ ...defaultSettings, themeColor: '#00FF00' }));
    expect(store.getState().settings.themeColor).toBe('#00FF00');
  });
});
