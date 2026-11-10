// 通用交互增强：危险操作确认、表单提交防重复、下拉自动提交、提示横幅转 Toast。
(function () {
    'use strict';

    // 带 data-confirm 的表单：提交前弹出确认框。
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirm')) {
            return;
        }

        if (!window.confirm(form.getAttribute('data-confirm'))) {
            event.preventDefault();
        }
    }, true);

    // 表单提交后禁用提交按钮，避免重复提交（延迟一拍以确保提交事件已触发）。
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || form.hasAttribute('data-no-loading')) {
            return;
        }

        window.setTimeout(function () {
            // 确认框取消或客户端校验失败时，不进入加载状态。
            if (event.defaultPrevented) {
                return;
            }

            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button[type="submit"]').forEach(function (button) {
                button.disabled = true;
                button.classList.add('is-loading');
            });
        }, 50);
    });

    // 带 data-auto-submit 的下拉框：选择后自动提交所在表单。
    document.querySelectorAll('select[data-auto-submit]').forEach(function (select) {
        select.addEventListener('change', function () {
            if (select.form) {
                // requestSubmit 保留提交事件、原生校验和加载反馈。
                select.form.requestSubmit();
            }
        });
    });

    // ---------- 侧边栏折叠 ----------

    // 桌面端切换折叠宽度并记住状态；移动端打开抽屉导航。
    var navToggle = document.getElementById('navToggle');
    if (navToggle) {
        try {
            if (window.localStorage.getItem('axiom-sidebar-collapsed') === '1') {
                document.body.classList.add('sidebar-collapsed');
            }
        } catch (e) {
            // localStorage 不可用时忽略，仅本次会话不记忆折叠状态。
        }

        navToggle.addEventListener('click', function () {
            if (window.matchMedia('(min-width: 768px)').matches) {
                var collapsed = !document.body.classList.contains('sidebar-collapsed');
                document.body.classList.toggle('sidebar-collapsed', collapsed);
                try {
                    window.localStorage.setItem('axiom-sidebar-collapsed', collapsed ? '1' : '0');
                } catch (e) {
                    // 忽略存储异常。
                }
            } else if (window.bootstrap && window.bootstrap.Offcanvas) {
                window.bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('mobileNav')).show();
            }
        });
    }

    // ---------- Toast 通知 ----------

    var toastContainer = null;

    // 惰性创建右下角 Toast 容器。
    function ensureToastContainer() {
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }
        return toastContainer;
    }

    // 在右下角弹出 Toast：type 为 Bootstrap 语义色（success/danger），autoHide 表示是否自动消失。
    function showToast(message, type, autoHide) {
        if (!message || typeof window.bootstrap === 'undefined' || !window.bootstrap.Toast) {
            return;
        }

        var toast = document.createElement('div');
        toast.className = 'toast align-items-center border-0 toast-' + type;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        var layout = document.createElement('div');
        layout.className = 'd-flex';

        var body = document.createElement('div');
        body.className = 'toast-body';
        body.textContent = message;

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close me-2 m-auto';
        close.setAttribute('data-bs-dismiss', 'toast');
        close.setAttribute('aria-label', 'Close');

        layout.appendChild(body);
        layout.appendChild(close);
        toast.appendChild(layout);
        ensureToastContainer().appendChild(toast);

        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });
        new window.bootstrap.Toast(toast, { autohide: !!autoHide, delay: 5000 }).show();
    }

    // 操作成功横幅 → 自动消失的成功 Toast。
    document.querySelectorAll('.alert-success').forEach(function (banner) {
        var message = banner.textContent.trim();
        banner.remove();
        if (message) {
            showToast(message, 'success', true);
        }
    });

    // 错误横幅 → 需手动关闭的错误 Toast（错误不应自动消失）。
    document.querySelectorAll('.alert-danger').forEach(function (banner) {
        var message = banner.textContent.trim();
        banner.remove();
        if (message) {
            showToast(message, 'danger', false);
        }
    });

    // 表单级校验摘要（asp-validation-summary=ModelOnly）→ 汇总为一条需手动关闭的错误 Toast。
    document.querySelectorAll('.validation-summary-errors').forEach(function (summary) {
        var messages = [];
        summary.querySelectorAll('ul > li').forEach(function (item) {
            var text = item.textContent.trim();
            if (text) {
                messages.push(text);
            }
        });

        if (messages.length === 0) {
            return;
        }

        summary.remove();
        showToast(messages.join('；'), 'danger', false);
    });
})();
