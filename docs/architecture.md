# Axiom 架构设计

## 1. 项目目标

Axiom 是一个用于练习 C# 微服务开发的项目。当前交付一条最小但完整的垂直链路：

`浏览器 → Axiom.Web → AuthHub 登录与授权 → Axiom.Web → ApiGateway → ReferenceData.Api → PostgreSQL`

机场列表在 Gateway 与 ReferenceData 之间使用 gRPC，地点写操作通过 Transactional Outbox 和 RabbitMQ 发布集成事件。新闻模块作为独立服务 `Axiom.News.Api` 接入同一网关与鉴权体系，但拥有独立数据库。当前不包含 BusinessA、多副本或 Kubernetes，避免基础设施先于业务闭环膨胀。

## 2. 技术栈

- .NET 10 LTS、ASP.NET Core、Razor Pages
- Cookie、OIDC 授权码 + PKCE
- Entity Framework Core、Npgsql、PostgreSQL
- YARP 反向代理
- gRPC、Protobuf 版本化契约
- RabbitMQ、Transactional Outbox、Inbox 幂等消费
- OpenIddict、ASP.NET Core Identity
- JWT Bearer 认证
- OpenTelemetry
- Jaeger、Loki、Prometheus、Grafana
- xUnit、FluentAssertions
- Docker Compose

## 3. 服务与公共项目

| 项目 | 职责 |
| --- | --- |
| `Axiom.Web` | Razor Pages 客户端、SSO 会话、机场列表与新增、新闻管理、通过网关调用 API |
| `Axiom.AuthHub` | 用户登录、OIDC 授权码 + PKCE、JWT 签发、客户端和演示用户初始化 |
| `Axiom.ApiGateway` | 统一入口、JWT 验证、机场列表 gRPC 调用和其他 REST 请求转发 |
| `Axiom.ReferenceData.Api` | 基础档案管理、gRPC 查询、地点事件发布与幂等消费示例 |
| `Axiom.ReferenceData.Domain` | 行政区与地点实体、仓储接口、领域异常，无框架依赖 |
| `Axiom.ReferenceData.Application` | 基础档案应用服务、DTO 与手工对象映射，只依赖 Domain |
| `Axiom.ReferenceData.Infrastructure` | EF Core DbContext、仓储、迁移、Outbox/Inbox 与种子服务 |
| `Axiom.News.Api` | 新闻稿管理、状态流转、筛选分页与乐观并发 |
| `Axiom.News.Domain` | 新闻实体、仓储接口、领域异常与长度常量，无框架依赖 |
| `Axiom.News.Application` | 新闻应用服务、DTO 与手工对象映射，只依赖 Domain |
| `Axiom.News.Infrastructure` | 新闻 EF Core DbContext、仓储实现与迁移，依赖 Domain + Application |
| `Axiom.Contracts` | 按版本目录组织的 Protobuf 契约和生成的客户端/服务端类型 |
| `Axiom.SharedKernel` | 跨服务共享的声明类型和权限名称 |
| `Axiom.Observability` | 统一配置 OpenTelemetry 日志、指标和链路导出 |
| `Axiom.ReferenceData.Tests` | 基础档案输入规则的单元测试 |
| `Axiom.IntegrationTests` | API 鉴权行为和 CRUD 流程的集成测试 |

## 4. ReferenceData 边界

`ReferenceData` 表示变化较慢、被其他业务引用的基础档案。第一阶段包括：

- 机场（Airport）
- 港口（Seaport）
- 火车站（RailStation）

航班、船期、车次、调度等运营数据不属于该服务。

当前三类档案共享统一模型，通过 `ReferenceLocationType` 区分。代码在同一类型内唯一：

- 机场代码：3 个英文字母，对应 IATA 代码。
- 港口代码：5 位字母或数字，对应 UN/LOCODE。
- 火车站代码：1～20 位字母、数字或连字符。

REST 端点：

| 资源 | 路径 |
| --- | --- |
| 机场 | `/api/reference-data/airports` |
| 港口 | `/api/reference-data/seaports` |
| 火车站 | `/api/reference-data/rail-stations` |

每个资源支持列表查询、按 ID 查询、新增、修改和删除。

### 4.1 列表查询契约

- 列表支持 `Code` 精确筛选和 `Name` 包含筛选；代码会去除首尾空格并转为大写，名称会去除首尾空格。
- `Page` 从 1 开始，`PageSize` 默认为 20、最大为 100；结果按代码和 ID 稳定排序。
- 列表统一返回 `items`、`totalCount`、`page`、`pageSize`。Gateway 的机场 gRPC 查询使用相同规则，外部 REST 契约保持一致。
- OpenAPI 文档位于 `/openapi/v1.json`，包含查询参数、响应类型和 JWT Bearer 安全定义。

### 4.2 错误与乐观并发

- API 错误使用 `application/problem+json`。输入错误返回 400，资源不存在返回 404，代码或版本冲突返回 409，空响应状态码和未处理异常也转换为 ProblemDetails；500 响应仅附带 `traceId`，不泄露异常细节。
- 按 ID 查询、新增和修改成功时返回强 `ETag`。修改和删除必须携带该值作为 `If-Match`：缺失返回 428，格式错误返回 400，过期版本返回 409。
- `ConcurrencyToken` 由 EF Core 作为并发令牌校验。修改或删除与 Outbox 写入仍在同一事务中提交，并发失败不会产生业务变更或事件。

## 5. News 边界

新闻是独立业务服务 `Axiom.News.Api`，不放进 ReferenceData：

| 能力 | 归属 |
| --- | --- |
| 新闻稿录入、编辑、发布、归档 | News.Api |
| 分类、状态、关键词筛选与分页 | News.Api |
| 基础档案（机场、港口、火车站） | ReferenceData.Api |
| 登录、令牌与权限 | AuthHub |

调用链路：`浏览器 → Axiom.Web（/News 页面）→ ApiGateway → News.Api → axiom_news`。

- 数据独立存放于 `axiom_news` 的 `news_articles` 表，`ConcurrencyToken` 参与 EF Core 乐观并发。
- REST 端点：`GET/POST /api/news/articles`、`GET/PUT/DELETE /api/news/articles/{id}`；列表支持 `Category`、`Status`、`Keyword` 筛选与分页。
- 权限为 `news.read`（查询）和 `news.manage`（新增、修改、删除），访问令牌 audience 为 `axiom-news`。
- 发布状态的新闻由服务端写入 `PublishedAt`，列表按 `PublishedAt ?? CreatedAt` 倒序并以 ID 稳定排序。
- 当前只提供 REST，未建立 gRPC 契约；出现高频只读查询或跨服务强类型调用时再引入。
- Web 页面 `/News`、`/News/Detail`、`/News/Edit` 仅通过网关访问 News.Api，不直接访问数据库。

## 6. 数据库设计

采用 Database per Service 原则，服务之间不共享 DbContext、连接字符串或迁移：

| 服务 | 数据库 | DbContext |
| --- | --- | --- |
| AuthHub | `axiom_auth` | `AuthDbContext` |
| ReferenceData.Api | `axiom_reference_data` | `ReferenceDataDbContext` |
| News.Api | `axiom_news` | `NewsDbContext` |

各数据库可以部署在同一个 PostgreSQL 实例中，但逻辑数据库、连接配置和迁移保持独立。应用启动时调用 `MigrateAsync`，迁移支持生成幂等脚本。

ReferenceData 数据库另含 `outbox_messages` 和 `inbox_messages`：前者保存待发布事件及 Confirm 后的发布时间，后者以事件 ID 和消费者名称的复合主键持久化去重。两张表均归 ReferenceData 独立迁移管理。

## 7. 认证与授权

### 7.1 登录和令牌

- Axiom.Web 使用 Cookie + OIDC 授权码流程，回调路径为 `/callback`，并强制 PKCE。
- AuthHub 使用 Identity Cookie 完成用户登录。
- 公共客户端 ID 默认为 `axiom-web`，不配置客户端密钥。
- Web 请求 `openid`、`profile`、`referencedata.read`、`referencedata.manage`、`news.read` 和 `news.manage` scopes。
- 访问令牌由 Web 服务端写入受保护的 HttpOnly 认证 Cookie，仅在服务端转发给 ApiGateway，不暴露给浏览器脚本。
- Compose 中 `Oidc:Authority` 保留浏览器可访问地址，`Oidc:BackchannelAuthority` 用于容器内令牌交换和签名密钥获取。
- 访问令牌有效期为 15 分钟。
- 用户唯一标识使用 JWT `sub` 声明，其值为 Identity 用户 ID。
- 访问令牌的 audience 由申请的 scope 决定：基础档案相关 scope 映射到 `axiom-reference-data`，新闻相关 scope 映射到 `axiom-news`，同时申请时令牌同时包含两者。
- 签名密钥通过 OIDC 元数据发布，ApiGateway、ReferenceData.Api 和 News.Api 使用 JWT Bearer 验证。
- 浏览器跳转使用 `Oidc:Authority` 的公网授权端点；令牌交换和签名密钥获取使用 `Oidc:BackchannelAuthority`，因为容器内的 `authhub:8080` 对浏览器不可解析。
- AuthHub 登录页的 `ReturnUrl` 使用相对路径，登录成功后能正确回到授权端点，不会退回首页再形成登录回环。

### 7.2 权限

| 权限 | 用途 |
| --- | --- |
| `referencedata.read` | 查询基础档案 |
| `referencedata.manage` | 新增、修改和删除基础档案 |
| `news.read` | 查询新闻 |
| `news.manage` | 新增、修改、删除和发布新闻 |

权限由 AuthHub 写入 `permission` 声明。无有效令牌返回 401；已认证但缺少所需权限返回 403。

Gateway 将当前请求的 Bearer 令牌放入 gRPC metadata，ReferenceData 的 gRPC 端点仍独立执行 JWT 验证和 `referencedata.read` 授权，不信任网关的内网身份声明。

演示用户只在配置 `SeedUser:Password` 时创建。密码和数据库密钥均通过环境变量提供，不写入源码。

## 8. 服务地址

本地开发默认地址：

| 服务 | 地址 |
| --- | --- |
| Axiom.Web | `http://localhost:3000` |
| ApiGateway | `http://localhost:5100` |
| AuthHub | `http://localhost:5101` |
| ReferenceData.Api REST | `http://localhost:5102` |
| ReferenceData.Api gRPC | `http://localhost:5103` |
| News.Api REST | `http://localhost:5104` |
| RabbitMQ AMQP / 管理界面 | `localhost:5672` / `http://localhost:15672` |
| Grafana | `http://localhost:3001` |
| Jaeger | `http://localhost:16686` |
| Prometheus | `http://localhost:9090` |

Compose 中 ReferenceData.Api 不暴露宿主机端口，外部请求通过 ApiGateway 进入。News.Api 仅暴露 `5104` 供调试使用，Web 请求统一走 Gateway。

## 9. 可观测性与健康检查

五个 ASP.NET Core 服务统一输出：

- Traces：经 OpenTelemetry Collector 导出到 Jaeger。
- Metrics：经 Collector 暴露给 Prometheus，并在 Grafana 中查询。
- Logs：经 Collector 以 OTLP 导出到 Loki，并在 Grafana 中查询。

RabbitMQ 发布活动使用 Producer span，并将 W3C `traceparent` 写入消息头；消费者提取父上下文创建 Consumer span，使数据库提交后的异步链路也能在 Jaeger 中关联。

健康检查：

- `/health/live`：进程存活检查。
- `/health/ready`：AuthHub、ReferenceData.Api 和 News.Api 同时检查各自数据库。

ASP.NET Core Generic Host 提供优雅停机能力，异步数据库操作传递 CancellationToken。

## 10. 配置与密钥

主要配置通过 `appsettings.json` 提供非敏感默认值，通过环境变量覆盖：

- `ConnectionStrings__Auth`
- `ConnectionStrings__ReferenceData`
- `ConnectionStrings__News`
- `Authentication__Authority`
- `Authentication__ValidIssuer`
- `OpenIddict__Issuer`
- `Oidc__Authority`
- `Oidc__BackchannelAuthority`（仅容器内需要）
- `ApiGateway__BaseAddress`
- `ReferenceDataGrpc__Address`
- `RabbitMq__HostName`、`RabbitMq__Port`、`RabbitMq__VirtualHost`、`RabbitMq__UserName`
- `RabbitMq__Password`（仅通过环境变量提供）
- `OpenTelemetry__OtlpEndpoint`
- `SeedUser__Password`

Compose 使用 `.env` 提供 PostgreSQL、RabbitMQ 和演示用户密码。`.env` 已被 `.gitignore` 排除，`.env.example` 仅包含占位符。

当前 Compose 和 OpenIddict 证书配置用于本地开发。生产环境必须配置正式签名/加密证书、HTTPS 和生产级密钥管理。

## 11. 本地运行

```powershell
cd E:\Repos\Axiom
Copy-Item .env.example .env
# 修改 .env 中的本地密码
docker compose up --build
```

不使用容器时，需要提供三个数据库连接字符串、可访问的 RabbitMQ 及 `RabbitMq__Password` 环境变量，然后启动五个服务：

```powershell
dotnet run --project src/Axiom.AuthHub
dotnet run --project src/Axiom.ReferenceData.Api
dotnet run --project src/Axiom.News.Api
dotnet run --project src/Axiom.ApiGateway
dotnet run --project src/Axiom.Web
```

浏览器访问 `http://localhost:3000`，Web 会跳转到 AuthHub 登录，再回到机场档案页面，可从导航进入「新闻管理」。

## 12. 构建与测试

```powershell
dotnet tool restore
dotnet format Axiom.slnx --verify-no-changes
dotnet build Axiom.slnx -c Release
dotnet test Axiom.slnx -c Release
```

当前已验证：

- `dotnet format --verify-no-changes` 通过。
- Release 构建：0 警告、0 错误。
- 7 个单元测试和 18 个 API/Web/Messaging 集成测试全部通过，覆盖 OIDC + PKCE、Bearer 转发、机场表单、gRPC DTO 映射、筛选分页、OpenAPI、ProblemDetails、ETag 并发、事务事件写入、Outbox 发布状态和 Inbox 去重。
- 三套迁移（Auth、ReferenceData、News）均可生成幂等脚本。
- Docker Compose 已实际启动 postgres、rabbitmq、authhub、reference-data、news-api、gateway、web 七个容器，`/health/live` 均返回 200。
- 容器内完成端到端验收：OIDC 授权码 + PKCE 登录、JWT audience `axiom-news`、网关 Bearer 转发、新闻列表与详情、创建、ETag 条件更新、过期 ETag 返回 409、缺少 `If-Match` 返回 428、非法 `If-Match` 返回 400、无令牌返回 401，`news_articles` 落库记录核对一致；浏览器侧「新闻管理」页面正常渲染并展示数据。
- 网关到 ReferenceData 的 gRPC 查询在容器内实际返回 200；新增机场后 Outbox 的 `PublishedAtUtc` 写入成功，自消费写入 Inbox，投递毒消息后死信队列计数为 1。
- 尚未验收：Jaeger、Loki、Prometheus、Grafana 和 otel-collector 因镜像不可用未启动。

## 13. gRPC 与 RabbitMQ

### 13.1 同步查询

- 外部契约仍为 REST/JSON，只有 `GET /api/reference-data/airports` 在 Gateway 内转为 gRPC；新增、修改、删除和其他资源继续通过 YARP 调用 REST。
- 契约位于 `Axiom.Contracts/Protos/v1/reference_data.proto`，包名为 `axiom.referencedata.v1`。新增字段使用新编号，禁止修改或复用已有编号。
- ReferenceData 使用独立 HTTP/2 端口提供 h2c gRPC，本地和 Compose 默认分别为 `5103` 和 `8081`。
- 客户端 Deadline 为 3 秒并传递请求 CancellationToken；Deadline 超限映射为 504，服务不可用映射为 503。

### 13.2 异步事件

1. Create、Update、Delete 在同一 EF Core `SaveChangesAsync` 中提交业务数据和 Outbox，Broker 不参与业务事务。
2. 后台发布器逐条发送持久化消息，只有收到 Publisher Confirm 才写入 `PublishedAtUtc`；失败会延迟重试。
3. 如果 Confirm 成功但发布状态保存失败，重启后会再次发布，因此整体语义是至少一次，而不是恰好一次。
4. 消息使用版本化事件类型和包含事件 ID、发生时间、地点快照的 JSON 信封。
5. 当前 ReferenceData 自消费地点事件作为完整链路示例。消费者成功写入 Inbox 后 ACK，重复事件直接 ACK；处理失败 NACK 且不 requeue，通过 DLX 进入持久化 DLQ。停机取消时允许 requeue，避免把未处理消息误判为毒消息。
6. Testing 环境不启动 RabbitMQ HostedService，自动化测试不依赖真实 Broker。

## 14. 后续阶段

按实际需求逐项加入：

1. 接入并验证可观测性组件（otel-collector、Jaeger、Loki、Prometheus、Grafana）。
2. BusinessA 等新的业务服务。
3. 正式证书、密钥管理和生产环境配置。
4. 多副本、Kubernetes 和自动化发布。

每个阶段都应保持独立构建、独立数据库边界、可测试和可观测，不提前创建没有实际职责的空项目。
