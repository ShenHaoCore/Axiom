# Axiom

基于 .NET 10 的微服务练习项目，覆盖统一网关、OIDC 单点登录、REST 与 gRPC、Transactional Outbox/Inbox、乐观并发与 Docker Compose 本地运行。

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

默认端口：Postgres `5432`、RabbitMQ `5672`、管理台 `15672`、Gateway `5100`、AuthHub `5101`、News.Api `5104`、Web `3000`。端口被占用时通过 `POSTGRES_PORT`、`RABBITMQ_PORT`、`RABBITMQ_MANAGEMENT_PORT` 覆盖。

浏览器访问 `http://localhost:3000`，使用 `.env` 中配置的演示账号登录。

## 构建与测试

```powershell
dotnet build Axiom.slnx -c Release
dotnet test Axiom.slnx -c Release
dotnet format Axiom.slnx --verify-no-changes
```

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
