# 安全策略

## 报告漏洞

请勿在公开 Issue 或 PR 中报告安全漏洞。请通过以下方式报告：

- 邮箱：`security@axiom.dev`（替换为实际联系方式）
- GitHub Security：通过仓库 **Settings → Security → Advisories → New draft security advisory** 提交

## 已知安全特性

- OIDC 授权码 + PKCE：防止授权码拦截
- Cookie Auth（Axiom.Web）：HttpOnly、SameSite=Lax，服务端持有令牌
- JWT Bearer：网关验证 audience 与 scope
- 密码哈希：AuthHub 使用 ASP.NET Core Identity 默认实现（PBKDF2）
- Secret 注入：所有敏感配置走环境变量 / `.env`，不硬编码
- Docker：生产镜像使用 `USER $APP_UID` 非 root

## 安全加固 TODO

- [ ] 所有生产容器启用只读文件系统（`read_only: true` + tmpfs 挂载）
- [ ] 启用 HTTPS（TLS Termination 由反向代理或 Gateway 处理）
- [ ] 速率限制（YARP 的 [RateLimiter](https://github.com/microsoft/reverse-proxy/blob/main/docs/rate-limit.md) 或网关层中间件）
- [ ] CORS 白名单
- [ ] 审计日志（登录、权限变更）
