# 阶段 F:测试补充 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan step-by-step (inline execution). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为后端补集成测试(WebApplicationFactory 覆盖中间件/策略/限流/异常映射),为前端补单元测试(vitest 覆盖权限转换/判定/RTK store)。

**Architecture:** 后端新建 `OneCup.IntegrationTests` 项目,xUnit + WebApplicationFactory<Program> + EF InMemory(管道测试不需真实 PG);前端引入 vitest + @testing-library/react,优先测纯逻辑模块(authentication/transformPermissions/store)。两项独立,可分别完成。

**Tech Stack:** 后端 .NET 10 xUnit + Microsoft.AspNetCore.Mvc.Testing;前端 vitest + @testing-library/react + jsdom。

## Global Constraints

- 后端 Program.cs 须加 `public partial class Program;`(WebApplicationFactory<Program> 要求)。
- 后端集成测试用 EF InMemory(不依赖真实 PG/Testcontainers,管道行为测试不需要)。
- 后端现有 ~135 个单元测试不动,集成测试是新增。
- 前端测试优先纯逻辑(transformPermissions/authentication),组件测试(RequirePermission)次之。
- 每步质量门:后端 `dotnet test`;前端 `npx vitest run`。
- 工作目录:根目录 `C:\Users\mi\Desktop\work_space\one-cup`。

---

## Part 1:后端集成测试

### Task 1:集成测试项目骨架 + Program 可测化

**Files:**
- Create: `backend/tests/OneCup.IntegrationTests/OneCup.IntegrationTests.csproj`
- Create: `backend/tests/OneCup.IntegrationTests/IntegrationTestFactory.cs`(WebApplicationFactory 定制:换 InMemory + 覆盖 Jwt secret)
- Modify: `backend/src/OneCup.Api/Program.cs`(加 partial class + 暴露 app)

**Interfaces:**
- Produces: `IntegrationTestFactory : WebApplicationFactory<Program>`,覆盖 DbContext 为 InMemory、Jwt secret 为合规测试值。

- [ ] **Step 1.1: Program.cs 加 partial class + 可测化**

`Program.cs` 末尾(`app.Run();` 之前)加:
```csharp
public partial class Program { }
```
这样 WebApplicationFactory<Program> 能引用。同时确认 `app.Run()` 前能被 factory 接管(factory 调 ConfigureTestServices 替换 DbContext)。

- [ ] **Step 1.2: 创建集成测试项目**

```xml
<!-- backend/tests/OneCup.IntegrationTests/OneCup.IntegrationTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OneCup.Api\OneCup.Api.csproj" />
  </ItemGroup>
</Project>
```

> 注:引用 OneCup.Api(非 Infrastructure),因为集成测试测的是 API 管道整体。

- [ ] **Step 1.3: 创建 IntegrationTestFactory**

```csharp
// backend/tests/OneCup.IntegrationTests/IntegrationTestFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Infrastructure.Persistence;

namespace OneCup.IntegrationTests;

/// <summary>
/// 测试用 WebApplicationFactory:DbContext 换 InMemory,Jwt secret 覆盖为合规测试值。
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"itest-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // 移除真实 DbContext 注册,换 InMemory
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OneCupDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);

            var ctxDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(OneCupDbContext));
            if (ctxDescriptor is not null) services.Remove(ctxDescriptor);

            services.AddDbContext<OneCupDbContext>(opt => opt.UseInMemoryDatabase(_dbName));

            // 覆盖 Jwt secret 为合规测试值(≥32 字节,非占位符)
            // JwtOptions 从 IConfiguration 绑定,这里覆盖配置
        });

        builder.UseEnvironment("Testing");
    }
}
```

> Jwt secret 覆盖:在 appsettings.Testing.json 或 ConfigureTestServices 里设 `Configuration["Jwt:SecretKey"]`。最简单:创建 `backend/src/OneCup.Api/appsettings.Testing.json` 含合规 Jwt 配置。

- [ ] **Step 1.4: 创建 appsettings.Testing.json**

```json
// backend/src/OneCup.Api/appsettings.Testing.json
{
  "Jwt": {
    "Issuer": "OneCup",
    "Audience": "OneCup",
    "SecretKey": "integration-test-secret-key-at-least-32-bytes!!",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 7
  },
  "ConnectionStrings": {
    "DefaultConnection": "InMemory"
  }
}
```
csproj 里配置 Testing 环境时复制此文件。

- [ ] **Step 1.5: 把测试项目加入 solution(若有 sln)**

若有 `backend/OneCup.sln`,加项目引用。若无 sln,跳过(dotnet test 直接指定 csproj)。

- [ ] **Step 1.6: 冒烟测试**

创建一个最小测试确认 factory 能启动:
```csharp
// backend/tests/OneCup.IntegrationTests/SmokeTest.cs
namespace OneCup.IntegrationTests;

public class SmokeTest : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    public SmokeTest(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Factory_starts_app()
    {
        var client = _factory.CreateClient();
        // 访问任意端点,确认 app 能启动(401 也算成功——说明中间件管道活着)
        var resp = await client.GetAsync("/api/auth/me");
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                 || resp.StatusCode == System.Net.HttpStatusCode.OK);
    }
}
```

Run: `dotnet test backend/tests/OneCup.IntegrationTests`
Expected: SmokeTest PASS(app 能启动)。

- [ ] **Step 1.7: 提交**

```bash
git add -A backend
git commit -m "test: 后端集成测试项目骨架 (WebApplicationFactory + InMemory + Testing配置)"
```

---

### Task 2:认证与授权集成测试

**Files:**
- Create: `backend/tests/OneCup.IntegrationTests/AuthAuthorizationTests.cs`

**场景覆盖:**
- 无 token 访问受保护端点 → 401
- 无效 token → 401
- 有效 token 但无权限(developer)访问 `/api/users` → 403
- 有效 token 且有权限(admin)访问 `/api/users` → 200
- admin 通配(`*`)放行所有策略

> 需要 helper 生成测试 token:用 JwtTokenService 或直接构造。InMemory DB 需种子一个 admin + developer 用户。

- [ ] **Step 2.1: 写测试(覆盖上述场景)**

测试需:
- 一个 helper 种子 InMemory(admin/developer 用户 + 角色 + 权限)
- 一个 helper 生成 JWT(用与后端相同的 issuer/audience/secret 签发,或调登录端点拿 token)

```csharp
// 伪代码结构
public class AuthAuthorizationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AuthAuthorizationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SeedDatabase();  // 种子 admin + developer
    }

    [Fact] public async Task No_token_protected_endpoint_returns_401() { ... }
    [Fact] public async Task Invalid_token_returns_401() { ... }
    [Fact] public async Task Developer_token_users_endpoint_returns_403() { ... }
    [Fact] public async Task Admin_token_users_endpoint_returns_200() { ... }
}
```

> 生成 token:调 `/api/auth/login`(admin/Admin@123)拿真实 token,最贴近真实流程。需种子 admin 用户的 BCrypt 哈希(用 SeedData.AdminPasswordHash)。

- [ ] **Step 2.2: 运行测试**

Run: `dotnet test backend/tests/OneCup.IntegrationTests --filter "FullyQualifiedName~AuthAuthorizationTests"`
Expected: PASS。

- [ ] **Step 2.3: 提交**

```bash
git add -A backend/tests/OneCup.IntegrationTests
git commit -m "test: 认证授权集成测试 (401/403/200/通配)"
```

---

### Task 3:全局异常映射 + 输入校验集成测试

**Files:**
- Create: `backend/tests/OneCup.IntegrationTests/ExceptionValidationTests.cs`

**场景:**
- 提交空 username 登录 → 400(FluentValidation 拦截)
- 弱密码创建用户 → 400
- 未知错误路径 → 500(Testing 环境异常处理)

- [ ] **Step 3.1: 写测试 + 运行 + 提交**

```bash
git commit -m "test: 异常映射 + 输入校验集成测试"
```

---

## Part 2:前端单元测试

### Task 4:前端 vitest 接入 + 纯逻辑测试

**Files:**
- Modify: `frontend/package.json`(加 vitest + @testing-library/react + jsdom)
- Create: `frontend/vitest.config.ts`(或并入 vite.config)
- Create: `frontend/src/utils/__tests__/authentication.test.ts`
- Create: `frontend/src/__tests__/transformPermissions.test.ts`

- [ ] **Step 4.1: 安装 vitest + jsdom**

```bash
cd frontend && npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom @vitejs/plugin-react --legacy-peer-deps --ignore-scripts
```

- [ ] **Step 4.2: vitest 配置**

`frontend/vite.config.ts` 加 test 配置(environment jsdom),或新建 vitest.config.ts:
```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: { environment: 'jsdom', globals: true },
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
});
```
package.json scripts 加:`"test": "vitest run"`、`"test:watch": "vitest"`。

- [ ] **Step 4.3: authentication.ts 测试**

```ts
// frontend/src/utils/__tests__/authentication.test.ts
import auth from '../authentication';

describe('authentication', () => {
  const adminPerm = { '*': ['*'] };
  const devPerm = { 'system:user': ['manage'], 'fabric': ['read'] };

  it('admin wildcard passes all', () => {
    expect(auth({ requiredPermissions: [{ resource: 'system:user', actions: ['manage'] }] }, adminPerm)).toBe(true);
  });
  it('matching resource+action passes', () => {
    expect(auth({ requiredPermissions: [{ resource: 'system:user', actions: ['manage'] }] }, devPerm)).toBe(true);
  });
  it('missing action fails', () => {
    expect(auth({ requiredPermissions: [{ resource: 'system:user', actions: ['read'] }] }, devPerm)).toBe(false);
  });
  it('missing resource fails', () => {
    expect(auth({ requiredPermissions: [{ resource: 'system:role', actions: ['manage'] }] }, devPerm)).toBe(false);
  });
  it('oneOfPerm any-satisfies', () => {
    expect(auth({ requiredPermissions: [{ resource: 'x' }, { resource: 'system:user', actions: ['manage'] }], oneOfPerm: true }, devPerm)).toBe(true);
  });
});
```

- [ ] **Step 4.4: transformPermissions 测试**

transformPermissions 在 `router.tsx` 里(非导出)。需先把它**导出**(或抽到 utils)才能测。
- 方案:`router.tsx` 加 `export function transformPermissions(...)`(已是非导出函数,加 export 即可)。
- 测试:普通权限拆分、admin 通配 `*`、空权限。

- [ ] **Step 4.5: 运行 + 提交**

Run: `cd frontend && npx vitest run`
Expected: 全 PASS。

```bash
git add -A frontend
git commit -m "test(fe): vitest 接入 + authentication/transformPermissions 单测"
```

---

## Self-Review

**1. Spec 覆盖(spec 第 8 节 F):**
- 8.1 后端集成测试 → Task 1+2+3 ✓
- 8.2 前端单测 → Task 4 ✓

**2. 范围合理性:** 后端集成测试聚焦管道行为(中间件/策略/异常),不重复单元测试已覆盖的 Service 逻辑。前端聚焦纯逻辑(权限转换/判定),组件测试作为可选。

**3. 数据库策略:** 后端集成测试用 InMemory(管道测试不需真实 PG);NumberingServiceConcurrencyTests 那种需真实 PG 的不在本阶段(已有独立测试)。
