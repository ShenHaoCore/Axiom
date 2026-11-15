# 分支管理规范

本项目采用简化 GitFlow：`main` 为可发布版本，`develop` 为日常集成分支，功能、修复、发布与热修都通过 PR 合入。

## 分支模型

| 分支 | 来源 | 合入目标 | 用途 | 合并方式 |
| --- | --- | --- | --- | --- |
| `main` | — | — | 可发布版本，每次合入对应一个发布 | 仅接受 `release/*`、`hotfix/*`、`develop` 的 PR |
| `develop` | `main` | `main`（发版时） | 日常集成，所有功能与缺陷分支的汇合点 | PR + squash |
| `feature/<简述>` | `develop` | `develop` | 单个功能开发 | PR + squash |
| `bugfix/<简述>` | `develop` | `develop` | 非线上缺陷修复 | PR + squash |
| `hotfix/<简述>` | `main` | `main`、`develop` | 线上紧急修复 | PR + squash，修复后回合 `develop` |
| `release/<版本号>` | `develop` | `main`、`develop` | 发版准备（版本号、变更说明） | PR + squash，发布后回合 `develop` |
| `chore/*`、`docs/*`、`refactor/*` | `develop` | `develop` | 构建、文档、重构等非功能变更 | PR + squash |
| `dependabot/*` | 自动 | `main` | 依赖升级，由 Dependabot 创建 | 自动审批后合并 |

## 命名规范

- 前缀固定为 `feature/`、`bugfix/`、`hotfix/`、`release/`、`chore/`、`docs/`、`refactor/`。
- 描述部分使用小写字母、数字、点、下划线或连字符，推荐短横线分词，例如 `feature/region-schema-redesign`、`release/0.3.0`。
- 不使用中文、空格和大写字母；一个分支只做一件事。
- PR 创建后由 [branch-naming.yml](../.github/workflows/branch-naming.yml) 校验 head 分支名，不符合规范时检查失败。

## 日常开发流程

```powershell
git switch develop
git pull --ff-only
git switch -c feature/<简述>

# 提交前本地验证
dotnet build Axiom.slnx -c Release
dotnet test Axiom.slnx -c Release
dotnet format Axiom.slnx --verify-no-changes

git push -u origin feature/<简述>
gh pr create --base develop --fill
```

- PR 必须通过 CI（restore、build、test、format）与 `branch-naming` 检查。
- `main`、`develop` 禁止直接推送、禁止强制推送与删除，历史保持线性。
- PR 合并后远程分支自动删除，无需手动清理。

## 发布流程

1. 从 `develop` 切 `release/<版本号>`，只提交版本号与变更说明。
2. PR 合入 `main`，由 Release Drafter 生成发布说明并打 tag。
3. 将 `main` 回合 `develop`，保持两边一致：

```powershell
git switch develop
git merge --ff-only origin/main
git push origin develop
```

## 线上热修流程

1. 从 `main` 切 `hotfix/<简述>`，修复并补充测试。
2. PR 合入 `main`，发布补丁版本。
3. 回合 `develop`，避免修复丢失：

```powershell
git switch develop
git merge --no-ff origin/main
git push origin develop
```

## 保护规则

`main`、`develop`、`release/*`、`hotfix/*` 通过 GitHub Ruleset 保护：

- 禁止删除、禁止非快进推送（强推）。
- 必须通过 PR 合入，要求线性历史（squash 或 rebase）。
- 推送新提交后自动取消过期审批，未解决的评审线程会阻止合并。
- `main` 额外要求 `build-test` 检查通过。

## 自动化

| 工作流 | 触发 | 作用 |
| --- | --- | --- |
| [ci.yml](../.github/workflows/ci.yml) | PR / push 到 `main`、`develop` | restore、build、test、format |
| [branch-naming.yml](../.github/workflows/branch-naming.yml) | PR | 校验 head 分支命名 |
| [branch-cleanup.yml](../.github/workflows/branch-cleanup.yml) | PR 关闭、每周定时 | 删除已合并 / 已关闭 PR 的分支；巡检 90 天无提交且无 PR 的分支（仅提醒） |
| [codeql.yml](../.github/workflows/codeql.yml) | PR / 定时 | 代码安全扫描 |
| [release-drafter.yml](../.github/workflows/release-drafter.yml) | 推送 tag / main | 生成发布说明 |
| [dependabot-auto-merge.yml](../.github/workflows/dependabot-auto-merge.yml) | Dependabot PR | 自动审批补丁与次版本升级 |

仓库已开启 `delete_branch_on_merge`：PR 合并时 GitHub 自动删除 head 分支；未合并就关闭的 PR 由 `branch-cleanup.yml` 删除。

## 提交信息

遵循 Conventional Commits，scope 使用模块名：

```text
feat(geo-divisions): Kind 字典按国家分域
fix(web): 译文原文页签不再跟随界面语言
chore(ci): 增加分支命名校验
```

常用类型：`feat`、`fix`、`refactor`、`docs`、`chore`、`test`、`perf`。
