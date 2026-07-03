using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OneCup.Api.Authorization;
using OneCup.Api.Services;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Application.Services;
using OneCup.Application.Validators;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Lockout;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── 数据库 (PostgreSQL via EF Core) ──────────────────────────
builder.Services.AddDbContext<OneCupDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 依赖注入:仓储 & 工作单元 ──────────────────────────────────
// 泛型仓储:所有实体共享一个实现
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── Controllers + JSON 约定 ──────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 枚举序列化为字符串,前后端一致
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ── FluentValidation:注册 Application 层所有 Validator(手动校验模式) ──
// 不使用 AspNetCore 自动拦截管道,改为在 Service 层手动 ValidateAsync → DomainException(→400)。
// 这样 Application 层不依赖 AspNetCore,校验逻辑可单测。
// 注:PasswordRules 为静态类,C# 禁止静态类作泛型实参(CS0718),故用 typeof().Assembly 等价注册。
builder.Services.AddValidatorsFromAssembly(typeof(PasswordRules).Assembly);

builder.Services.AddOpenApi();

// ── CORS (前后端分离,允许前端开发服务器访问) ───────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ── 认证授权 (JWT) ─────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddOptions<JwtOptions>().ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.Zero,
        };
    });
// ── 依赖注入:系统管理服务 ─────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<INumberingClock, NumberingClock>();
builder.Services.AddScoped<INumberingService, NumberingService>();
builder.Services.AddScoped<INumberingRuleService, NumberingRuleService>();
builder.Services.AddScoped<INumberingDictionaryService, NumberingDictionaryService>();

// ── 依赖注入:认证相关服务 ─────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<CurrentUserService>();
builder.Services.AddHttpContextAccessor();

// ── 登录失败锁定 (内存方案) ──────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<LockoutOptions>(builder.Configuration.GetSection(LockoutOptions.SectionName));
builder.Services.AddSingleton<ILockoutStore, MemoryLockoutStore>();

// ── 授权策略 (基于 JWT perm_codes claim) ───────────────────────
// admin 角色的 perm_codes 含通配 "*",由 WildcardAuthorizationHandler 放行所有策略。
builder.Services.AddSingleton<IPermissionCalculator, PermissionCalculator>();
builder.Services.AddSingleton<IAuthorizationHandler, WildcardAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user-manage", policy =>
        policy.RequireClaim("perm_codes", "system:user:manage"));
    options.AddPolicy("role-manage", policy =>
        policy.RequireClaim("perm_codes", "system:role:manage"));
    options.AddPolicy("numbering-view", policy =>
        policy.RequireClaim("perm_codes", "system:numbering:view"));
    options.AddPolicy("numbering-manage", policy =>
        policy.RequireClaim("perm_codes", "system:numbering:manage"));
});

// 权限拒绝审计日志:装饰默认 handler,在 403 时记 Warning
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, AuthorizationAuditHandler>();

// ── 限流 ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 登录/刷新:按 IP 固定窗口,10 次/分钟(分区,每 IP 独立桶)
    options.AddPolicy("auth-login", ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    // 部署注意:限流按 RemoteIpAddress 分区。若部署在反向代理(nginx/ALB)后,
    // 必须配置 UseForwardedHeaders,否则所有客户端共享代理 IP,限流失效。
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

var app = builder.Build();

// ── 数据库迁移（可选，由配置开关 Database:MigrateOnStartup 控制）──────────
// 生产默认 false（appsettings.json），开发默认 true（appsettings.Development.json），
// compose 部署用环境变量 Database__MigrateOnStartup=true 显式覆盖。
if (builder.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<OneCup.Infrastructure.Persistence.OneCupDbContext>();
    var migrateLogger = sp.GetRequiredService<ILogger<Program>>();
    migrateLogger.LogInformation("正在应用数据库迁移...");
    await db.Database.MigrateAsync();
    migrateLogger.LogInformation("数据库迁移完成");
}

// ── 中间件管道 ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 全局异常处理:认证异常→401, 领域异常→400, 其他→500;生产环境不回显内部细节
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        context.Response.ContentType = "application/json";

        // 标准错误结构
        var (statusCode, code, message, retryAfter) = exception switch
        {
            AccountLockedException ex => (StatusCodes.Status401Unauthorized, "ACCOUNT_LOCKED", (string?)ex.Message, ex.RetryAfter),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", exception?.Message, (TimeSpan?)null),
            DomainException => (StatusCodes.Status400BadRequest, "DOMAIN_ERROR", exception?.Message, (TimeSpan?)null),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", (string?)null, (TimeSpan?)null),
        };

        // 500 始终记完整堆栈;其他警告级
        if (statusCode >= 500)
        {
            logger?.LogError(exception, "未处理异常:{Type}", exception?.GetType().Name);
        }
        else
        {
            logger?.LogWarning("业务异常:{Code} {Message}", code, exception?.Message);
        }

        // 生产环境 500 不回显 message
        var exposedMessage = statusCode >= 500 && !app.Environment.IsDevelopment()
            ? "服务器内部错误"
            : message ?? "服务器内部错误";

        context.Response.StatusCode = statusCode;
        var response = retryAfter is null
            ? (object)new { code, message = exposedMessage }
            : new { code, message = exposedMessage, retryAfter = (int)Math.Ceiling(retryAfter.Value.TotalSeconds) };
        await context.Response.WriteAsJsonAsync(response);
    });
});

app.MapControllers();

app.Run();

// 暴露为 public partial class,供 WebApplicationFactory<Program>(集成测试)引用。
// 必须位于所有顶级语句之后(类型声明不能穿插在顶级语句中间)。
public partial class Program { }
