import { describe, it, expect } from 'vitest';
import { transformPermissions } from '@/router';

describe('transformPermissions (后端 permCodes → 前端权限结构)', () => {
  it('普通 permCodes 拆分为 resource→actions', () => {
    expect(transformPermissions(['fabric:read'], [])).toEqual({ fabric: ['read'] });
  });

  it('同一资源多动作聚合到同一 key', () => {
    expect(transformPermissions(['fabric:read', 'fabric:write'], [])).toEqual({
      fabric: ['read', 'write'],
    });
  });

  it('不同资源分别建立 key', () => {
    expect(
      transformPermissions(['fabric:read', 'system:user:manage'], [])
    ).toEqual({
      fabric: ['read'],
      'system:user': ['manage'],
    });
  });

  it('admin 角色 → {"*":["*"]}', () => {
    expect(transformPermissions(['fabric:read'], ['admin'])).toEqual({
      '*': ['*'],
    });
  });

  it('permCodes 含 "*" → {"*":["*"]}', () => {
    expect(transformPermissions(['*'], ['developer'])).toEqual({ '*': ['*'] });
  });

  it('admin 角色优先级高于具体权限码', () => {
    expect(
      transformPermissions(['fabric:read', 'system:user:manage'], ['admin'])
    ).toEqual({ '*': ['*'] });
  });

  it('空 permCodes → {}', () => {
    expect(transformPermissions([], [])).toEqual({});
  });

  it('空 permCodes + 非 admin 角色 → {}', () => {
    expect(transformPermissions([], ['developer'])).toEqual({});
  });

  it('多段码(system:user:manage → {system:user:[manage]})', () => {
    expect(transformPermissions(['system:user:manage'], [])).toEqual({
      'system:user': ['manage'],
    });
  });

  it('三段码拆分:最后一段为 action,前面拼接为 resource', () => {
    expect(
      transformPermissions(['a:b:c:read', 'a:b:c:write'], [])
    ).toEqual({
      'a:b:c': ['read', 'write'],
    });
  });

  it('单段码(无冒号)被忽略(parts.length < 2)', () => {
    expect(transformPermissions(['standalone', 'fabric:read'], [])).toEqual({
      fabric: ['read'],
    });
  });

  it('仅含无效单段码 → {}', () => {
    expect(transformPermissions(['noprefix'], [])).toEqual({});
  });
});
