using Microsoft.EntityFrameworkCore;
using OneCup.Application.Interfaces;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
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

var app = builder.Build();

// ── 中间件管道 ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// 全局异常处理:领域异常 → 400,其他 → 500
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/json";

        context.Response.StatusCode = exception switch
        {
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
