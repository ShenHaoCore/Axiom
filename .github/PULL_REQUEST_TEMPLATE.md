## 摘要
<!-- 一句话 PR 做了什么。 -->

## 变更类型
- [ ] 新功能 (feat)
- [ ] 缺陷修复 (fix)
- [ ] 重构 (refactor)
- [ ] 文档 (docs)
- [ ] 构建 / CI (chore)
- [ ] 其他：

## 影响范围
- [ ] Axiom.AuthHub
- [ ] Axiom.ApiGateway
- [ ] Axiom.ReferenceData.Api
- [ ] Axiom.News.Api
- [ ] Axiom.Bff
- [ ] Axiom.Web
- [ ] frontend/
- [ ] SharedKernel / Contracts / Observability
- [ ] compose.yaml / Dockerfile / CI

## 依赖
- [ ] 有依赖的 PR / Issue 编号：
- [ ] 阻塞后续开发：

## 验证
- [ ] `dotnet build Axiom.slnx -c Release`
- [ ] `dotnet test Axiom.slnx -c Release`
- [ ] `dotnet format Axiom.slnx --verify-no-changes`
- [ ] `npm run build`（如有前端改动）
- [ ] 本地冒烟：
- [ ] 集成测试新增 / 更新：

## 截图 / 日志
<!-- 贴前端 UI 截图、API 响应或关键日志片段。 -->

## 风险 / 回滚
- 可能影响：
- 回滚方式：

## Checklist
- [ ] 分支命名符合 `feature/*` / `bugfix/*` / `hotfix/*`
- [ ] 提交信息遵循 Conventional Commits
- [ ] 无 `Console.WriteLine` / `Debug.WriteLine` 遗留
- [ ] 配置新增项已在 appsettings.example / .env.example 同步
