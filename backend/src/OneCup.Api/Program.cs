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
using OneCup.Infrastructure.Interfaces;
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
});

// ── 限流 ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 登录/刷新:按 IP 固定窗口,10 次/分钟
    options.AddFixedWindowLimiter("auth-login", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // 全局兜底:按 IP,120 次/分钟
    options.AddFixedWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 120;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

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

// ── 中间件管道 ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 全局异常处理:认证异常 → 401, 领域异常 → 400, 其他 → 500
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/json";

        context.Response.StatusCode = exception switch
        {
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            DomainException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        var response = new
        {
            message = exception?.Message ?? "An unexpected error occurred.",
        };
        await context.Response.WriteAsJsonAsync(response);
    });
});

app.MapControllers();

app.Run();
