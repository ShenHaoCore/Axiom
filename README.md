# Axiom

[![build-test](https://github.com/ShenHaoCore/Axiom/actions/workflows/ci.yml/badge.svg)](https://github.com/ShenHaoCore/Axiom/actions/workflows/ci.yml)
[![codeql](https://github.com/ShenHaoCore/Axiom/actions/workflows/codeql.yml/badge.svg)](https://github.com/ShenHaoCore/Axiom/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ShenHaoCore/Axiom/blob/main/LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-blue.svg)](https://learn.microsoft.com/ef/core/)
[![OpenIddict](https://img.shields.io/badge/OpenIddict-7.x-1f4e79.svg)](https://openiddict.com/)

基于 .NET 10 的微服务练习项目，覆盖统一网关、OIDC 单点登录、REST 与 gRPC、Transactional Outbox/Inbox、乐观并发与 Docker Compose 本地运行。

## 架构总览

```
┌─────────────────────────────────────────────────────────┐
│ 浏览器                                                   │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Razor Pages (:3000)     Cookie Auth                 │ │
│ └─────────────────────────┬───────────────────────────┘ │
└───────────────────────────┼─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│              Axiom.Web (:3000)                           │
│        OIDC 授权码 + PKCE → Cookie 会话                  │
│        服务端持有访问令牌，浏览器不接触 JWT              │
└───────────────────────────┬─────────────────────────────┘
                            │ Bearer JWT (scope 映射 audience)
┌───────────────────────────▼─────────────────────────────┐
│           Axiom.ApiGateway (:5100)                       │
│     YARP 反向代理 + JWT Bearer 验证                      │
│     gRPC 直连 ReferenceData                              │
└───┬──────────────┬──────────────────┬──────────────────┘
    │              │                  │
┌───▼───┐     ┌────▼─────┐    ┌──────▼──────┐
│News   │     │Reference  │    │ Reference   │
│Api    │     │Data REST │    │ Data gRPC   │
│:5104  │     │:5102      │    │ :5103       │
└───┬───┘     └────┬─────┘    └──────────────┘
    │              │
┌───▼──────────────▼──────────────────────────────────────┐
│     PostgreSQL（每服务独立数据库）  +  RabbitMQ            │
│     Outbox / Inbox 事务性消息                              │
└──────────────────────────────────────────────────────────┘
```

## 服务划分

| 项目 | 端口 | 职责 |
| --- | --- | --- |
| `Axiom.AuthHub` | 5101 | 用户登录、OIDC 授权码 + PKCE、JWT 签发、客户端与演示用户初始化 |
| `Axiom.ApiGateway` | 5100 | 统一入口、JWT 验证、机场查询走 gRPC、其余请求转发 |
| `Axiom.ReferenceData.Api` | 5102 REST / 5103 gRPC | 基础档案管理、地点事件发布与幂等消费 |
| `Axiom.News.Api` | 5104 | 新闻稿管理、筛选分页、ETag 乐观并发 |
| `Axiom.Web` | 3000 | Razor Pages 客户端、SSO 会话、机场与新闻管理 |
| PostgreSQL / RabbitMQ | 5432 / 5672、15672 | 每服务独立数据库、集成事件总线 |

## 技术栈

- .NET 10、ASP.NET Core Minimal API、Razor Pages
- OpenIddict（授权码 + PKCE）、JWT Bearer、声明式权限策略
- YARP 反向代理、gRPC、RabbitMQ（Outbox/Inbox、死信队列）
- EF Core + PostgreSQL、幂等迁移脚本
- OpenTelemetry、健康检查、ProblemDetails、OpenAPI

## 目录结构

```text
src/
  Axiom.AuthHub/            认证与授权服务
  Axiom.ApiGateway/         网关
  Axiom.ReferenceData.Api/  基础档案服务
  Axiom.News.Api/           新闻服务
  Axiom.Web/                Razor Pages 前端
  Axiom.Contracts/          跨服务契约
  Axiom.SharedKernel/       共享内核与权限常量
  Axiom.Observability/      可观测性注册
tests/
  Axiom.UnitTests/
  Axiom.IntegrationTests/
deploy/                     Postgres 初始化脚本
docs/architecture.md        架构与验收记录
compose.yaml                Docker Compose 编排
```

## 本地启动

前置：.NET 10 SDK、Docker（WSL2 或 Docker Desktop）。

```powershell
Copy-Item .env.example .env
# 编辑 .env，填入本地使用的数据库、RabbitMQ 和演示账号密码
docker compose up -d --build
```

默认端口：Postgres `5432`、RabbitMQ `5672`、管理台 `15672`、Gateway `5100`、AuthHub `5101`、News.Api `5104`、Web `3000`。端口被占用时通过 `POSTGRES_PORT`、`RABBITMQ_PORT`、`RABBITMQ_MANAGEMENT_PORT`、`AUTHHUB_PORT`、`GATEWAY_PORT`、`NEWS_PORT`、`WEB_PORT` 覆盖。

浏览器访问 `http://localhost:3000`，使用 `.env` 中配置的演示账号登录。

会话 Cookie 由 `Axiom.Web` 服务端签发并持有访问令牌，浏览器不接触 JWT；写操作走 ASP.NET Core 内建的防伪令牌校验。

## 多环境启动

服务通过 `ASPNETCORE_ENVIRONMENT` 选择配置：`appsettings.json` 提供公共默认值，`appsettings.{Environment}.json` 覆盖该环境的日志级别；连接串、对外地址、证书等敏感或环境相关的值一律由环境变量注入，不写入配置文件。

| 环境 | 应用配置 | 编排与变量 | 启动方式 |
| --- | --- | --- | --- |
| Development | `appsettings.Development.json` | `compose.yaml`、`.env` | `docker compose up -d --build` |
| Staging | `appsettings.Staging.json` | `compose.staging.yaml`、`.env.staging` | `docker compose --env-file .env.staging -f compose.yaml -f compose.staging.yaml up -d --build` |
| Production | `appsettings.Production.json` | `compose.production.yaml`、`.env.production` | `docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --build` |

```powershell
Copy-Item .env.staging.example .env.staging
# 填写数据库、RabbitMQ、证书密码与对外域名
New-Item -ItemType Directory -Force deploy/certs
# 将 signing.pfx 与 encryption.pfx 放入 deploy/certs
```

- 非开发环境启动时 AuthHub 从 `OpenIddict:Certificates:{Signing|Encryption}:Path` 加载 PFX 证书，密码由 `OPENIDDICT_CERT_PASSWORD` 注入；缺少证书会直接启动失败。
- 对外地址：`PUBLIC_AUTH_ISSUER`（AuthHub issuer、Web 授权地址）与 `PUBLIC_WEB_CALLBACK_URL`（回调地址）；内部调用使用 `INTERNAL_AUTH_URL`、`INTERNAL_GATEWAY_URL`。
- 内部链路走 HTTP 时保持 `AUTH_REQUIRE_HTTPS_METADATA=false`；启用内部 TLS 后改为 `true`。
- `.env.staging`、`.env.production` 与 `deploy/certs/` 已被忽略，证书和密码不入库。

## 构建与测试

```powershell
dotnet build Axiom.slnx -c Release
dotnet test Axiom.slnx -c Release
dotnet format Axiom.slnx --verify-no-changes
```

- 集成测试默认使用 EF Core 内存库；设置 `AXIOM_TEST_POSTGRES` 指向可用 PostgreSQL 后，每个测试会创建并清理独立数据库，走真实 PostgreSQL（CI 已内置该服务容器）。本机可用 compose 实例：`Host=localhost;Port=5433;Database=postgres;Username=axiom;Password=<POSTGRES_PASSWORD>`。

## 分支与合并

| 分支 | 用途 | 合并方式 |
| --- | --- | --- |
| `main` | 可发布版本 | 仅从 `develop` 或 `release/*` 通过 PR 合入 |
| `develop` | 日常集成 | 功能与缺陷分支通过 PR 合入 |
| `feature/*` | 单个功能开发 | PR → `develop` |
| `bugfix/*` | 缺陷修复 | PR → `develop` |
| `release/*` | 发版准备 | PR → `main`，并回合 `develop` |
| `hotfix/*` | 线上紧急修复 | PR → `main`，并回合 `develop` |

- 分支命名使用 `feature/<简述>`、`bugfix/<简述>`、`release/<版本号>`、`hotfix/<简述>`。
- `main`、`develop`、`release/*`、`hotfix/*` 受保护：禁止删除与强制推送，必须通过 PR，仅允许线性历史（squash 或 rebase）。
- 提交信息遵循 Conventional Commits：`feat:`、`fix:`、`chore:`、`docs:` 等。
- PR 会自动执行 `.github/workflows/ci.yml`：restore、build、test、format 检查。

## 接口约定

- 统一分页响应 `PagedResponse<T>`，列表按稳定排序返回。
- 详情返回强 ETag `"{{guid:N}}"`；更新与删除要求 `If-Match`，缺失 428、格式错误 400、版本冲突 409。
- 错误统一返回 ProblemDetails。
- 权限：`referencedata.read`、`referencedata.manage`、`news.read`、`news.manage`；令牌 audience 按申请 scope 映射到 `axiom-reference-data` / `axiom-news`。

## 文档

架构边界、鉴权链路、数据库划分和容器验收结论见 [docs/architecture.md](docs/architecture.md)。

未验收项：Jaeger、Loki、Prometheus、Grafana 与 otel-collector 因镜像不可用尚未启动。
