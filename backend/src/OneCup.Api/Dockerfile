# ─────────────────────────────────────────────────────────────
# OneCup 后端 Dockerfile
# build context = 仓库根 (compose 里设 context: ..)
# 多阶段构建：sdk 镜像编译 → aspnet runtime 镜像运行
# ─────────────────────────────────────────────────────────────

# ── 构建阶段 ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 拷贝整个 backend（含 slnx + 4 个项目 + 项目间引用）
COPY backend/ ./backend/

# 发布 Api 项目（自动 restore 关联项目），Release 配置
RUN dotnet publish ./backend/src/OneCup.Api/OneCup.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ── 运行阶段（不含 SDK，镜像更小）──
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# 装 curl 供 docker-compose healthcheck 探活 /health 用(aspnet 基础镜像默认无 curl)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# 监听 5000 端口（与项目约定一致）
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

ENTRYPOINT ["dotnet", "OneCup.Api.dll"]
