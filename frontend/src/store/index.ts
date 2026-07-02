import { configureStore, createSlice } from '@reduxjs/toolkit';
import type { TypedUseSelectorHook } from 'react-redux';
import { useDispatch, useSelector } from 'react-redux';
import defaultSettings from '../settings.json';

export interface UserInfo {
  name?: string;
  permissions: Record<string, string[]>;
}

// ── userInfo slice ──
interface UserInfoState {
  userInfo: UserInfo;
  userLoading: boolean;
}

const initialUserInfoState: UserInfoState = {
  userInfo: { permissions: {} },
  userLoading: false,
};

const userInfoSlice = createSlice({
  name: 'userInfo',
  initialState: initialUserInfoState,
  reducers: {
    setUserInfo(state, action: { payload: Partial<UserInfoState> }) {
      Object.assign(state, action.payload);
    },
  },
});

// ── settings slice ──
const settingsSlice = createSlice({
  name: 'settings',
  initialState: defaultSettings as typeof defaultSettings,
  reducers: {
    setSettings(_state, action: { payload: typeof defaultSettings }) {
      return action.payload;
    },
  },
});

export const { setUserInfo } = userInfoSlice.actions;
export const { setSettings } = settingsSlice.actions;

export const store = configureStore({
  reducer: {
    userInfo: userInfoSlice.reducer,
    settings: settingsSlice.reducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;
export const useAppDispatch: () => AppDispatch = useDispatch;

// 兼容旧代码中对 GlobalState 类型的引用
export interface GlobalState extends RootState {}
