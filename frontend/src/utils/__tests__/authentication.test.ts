import { describe, it, expect } from 'vitest';
import auth from '../authentication';
import type { UserPermission } from '../authentication';

describe('authentication (permission judging)', () => {
  const adminPerm: UserPermission = { '*': ['*'] };
  // developer: 能管理 system:user、读取 fabric
  const devPerm: UserPermission = {
    'system:user': ['manage'],
    'system:role': ['manage'],
    fabric: ['read', 'write'],
  };

  describe('admin wildcard', () => {
    it('admin wildcard {"*":["*"]} 放行所有资源', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:user', actions: ['manage'] },
            ],
          },
          adminPerm
        )
      ).toBe(true);
    });

    it('admin 通配对不带 actions 的资源也放行', () => {
      expect(
        auth({ requiredPermissions: [{ resource: 'any:thing' }] }, adminPerm)
      ).toBe(true);
    });

    it('admin 通配对空 requiredPermissions 也放行', () => {
      expect(auth({}, adminPerm)).toBe(true);
    });
  });

  describe('resource + action 精确匹配', () => {
    it('资源+动作都命中 → 通过', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:user', actions: ['manage'] },
            ],
          },
          devPerm
        )
      ).toBe(true);
    });

    it('多动作全部命中 → 通过', () => {
      expect(
        auth(
          {
            requiredPermissions: [{ resource: 'fabric', actions: ['read', 'write'] }],
          },
          devPerm
        )
      ).toBe(true);
    });

    it('缺少动作(missing action)→ 失败', () => {
      // fabric 有 read/write,但没有 delete
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'fabric', actions: ['delete'] },
            ],
          },
          devPerm
        )
      ).toBe(false);
    });

    it('动作部分命中部分缺失 → 失败(every 语义)', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'fabric', actions: ['read', 'delete'] },
            ],
          },
          devPerm
        )
      ).toBe(false);
    });

    it('缺少资源(missing resource)→ 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:permission', actions: ['manage'] },
            ],
          },
          devPerm
        )
      ).toBe(false);
    });

    it('用户权限为空对象 → 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:user', actions: ['manage'] },
            ],
          },
          {}
        )
      ).toBe(false);
    });
  });

  describe('oneOfPerm(任一满足即可)', () => {
    it('一个命中即通过', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'x' },
              { resource: 'system:user', actions: ['manage'] },
            ],
            oneOfPerm: true,
          },
          devPerm
        )
      ).toBe(true);
    });

    it('全部不命中 → 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'x', actions: ['a'] },
              { resource: 'y', actions: ['b'] },
            ],
            oneOfPerm: true,
          },
          devPerm
        )
      ).toBe(false);
    });
  });

  describe('all-match(默认:全部满足)', () => {
    it('多条件全部命中 → 通过', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:user', actions: ['manage'] },
              { resource: 'fabric', actions: ['read'] },
            ],
          },
          devPerm
        )
      ).toBe(true);
    });

    it('多条件部分缺失 → 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [
              { resource: 'system:user', actions: ['manage'] },
              { resource: 'fabric', actions: ['delete'] },
            ],
          },
          devPerm
        )
      ).toBe(false);
    });
  });

  describe('RegExp resource matching', () => {
    it('正则匹配到的资源全部满足动作 → 通过', () => {
      expect(
        auth(
          {
            requiredPermissions: [{ resource: /^system:/, actions: ['manage'] }],
          },
          devPerm
        )
      ).toBe(true);
    });

    it('正则匹配到某资源但动作不满足 → 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [{ resource: /^system:/, actions: ['delete'] }],
          },
          devPerm
        )
      ).toBe(false);
    });

    it('正则匹配不到任何资源 → 失败', () => {
      expect(
        auth(
          {
            requiredPermissions: [{ resource: /^nope/, actions: ['manage'] }],
          },
          devPerm
        )
      ).toBe(false);
    });
  });
});
