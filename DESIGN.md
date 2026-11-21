# DESIGN.md — Axiom 管理后台视觉世界

> 世界：**Tabler**（@tabler/core 1.7.5，用户 2026-11-21 点名"使用它重做全部页面"，pin 全保真执行）。
> 模式：Operate（管理后台）。工艺基准：preview.tabler.io 官方预览。

## 框架与资源

- UI 库：`wwwroot/lib/tabler/css/tabler.min.css` + `js/tabler.min.js`（内置 Bootstrap 5 JS：dropdown/toast/collapse/offcanvas）。
- `bootstrap.min.css` 已移除（Tabler 自带完整 Bootstrap 5 CSS）；jQuery 仅为 unobtrusive validation 保留。
- `<html data-bs-theme="light">` 必须保留——Tabler 1.7 主题由该属性驱动。
- 垂直侧边栏类：`navbar navbar-vertical navbar-expand-md`（<768px 抽屉、≥768px 停靠；1.7.5 移除了 `navbar-expand-xs` docking）。

## 结构约定

- 页面骨架：`.page` > `aside.navbar-vertical` + `.page-wrapper` > `main.page-body > .container-xl` + `.footer`。
- 页头：`.page-header.d-print-none > .row > .col(.page-title/.page-subtitle.text-secondary) + .col-auto(.btn-list)`。
- 列表卡：`.card > .card-header(.card-title + .badge.bg-secondary-lt 计数) + .table-responsive > .table.table-vcenter.card-table`。
- 空状态：Tabler `.empty / .empty-title / .empty-subtitle / .empty-action`。
- 状态徽章：`.badge.bg-success-lt`（已发布）/`.bg-secondary-lt`（草稿）/`.bg-neutral-lt`（归档）。

## site.css（薄层，只承载产品特有组件）

保留的组件块：`filter-bar/segmented/search-field`、`form-card/form-section/section-heading/form-actions/required-mark`、`badge-soft + status-*/level-*`（树与旧状态的 lt 风格，值取 Tabler 语义色）、`detail-*`（详情描述列表）、`region-* / tree-select*`（行政区树与父级选择器）、`translation-*`（译文编辑器）、`count-pill/row-actions/cell-num/cell-muted`、`danger-zone`、`pager`、`confirm-dialog`、`toast-danger`、`skip-link`、`nav-category`。
无障碍/响应式约定不变：skip link 首焦点、`:focus-visible` 双层光环、`@media (hover: hover)` 包裹全部 hover、`pointer: coarse` 触控 ≥44px、`prefers-reduced-motion` 降级、表格操作列小屏 sticky、`html { scroll-padding-block-start: 72px }`。

## 不变的功能契约

- `data-confirm` / `data-confirm-button`（删除确认点名对象 + 动词按钮）、`data-tree-select`、`data-children-url`、`data-language-select`。
- 表单内隐藏字段：`ETag`、`__RequestVerificationToken`；错误走 `alert-danger`（TempData 跨重定向），成功走 `alert-success`。
- 所有交互测试契约（77 个）在迁移中保持绿色。
