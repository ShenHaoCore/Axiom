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

    // 新建国家：国家代码改完两位后带 query 刷新 Kind / 父级树（保留 parentId）。
    document.querySelectorAll('input[data-reload-country]').forEach(function (input) {
        input.addEventListener('change', function () {
            var code = (input.value || '').trim().toUpperCase();
            if (code.length !== 2) {
                return;
            }

            input.value = code;
            var url = new URL(window.location.href);
            url.searchParams.set('countryCode', code);
            window.location.href = url.pathname + '?' + url.searchParams.toString();
        });
    });

    // ---------- 父级树形下拉 ----------
    document.querySelectorAll('[data-tree-select]').forEach(function (root) {
        var toggle = root.querySelector('.tree-select-toggle');
        var panel = root.querySelector('.tree-select-panel');
        var label = root.querySelector('.tree-select-label');
        var input = root.querySelector('input[type="hidden"]');
        var filter = root.querySelector('[data-tree-select-filter]');
        if (!toggle || !panel || !label || !input) {
            return;
        }

        function setOpen(open) {
            panel.hidden = !open;
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (open && filter) {
                filter.focus();
            }
        }

        toggle.addEventListener('click', function () {
            setOpen(panel.hidden);
        });

        panel.querySelectorAll('.tree-select-option').forEach(function (option) {
            option.addEventListener('click', function () {
                var value = option.getAttribute('data-value') || '';
                var text = option.getAttribute('data-label') || option.textContent || '';
                input.value = value;
                label.textContent = text;
                panel.querySelectorAll('.tree-select-option.is-selected').forEach(function (selected) {
                    selected.classList.remove('is-selected');
                    selected.setAttribute('aria-selected', 'false');
                });
                option.classList.add('is-selected');
                option.setAttribute('aria-selected', 'true');
                setOpen(false);

                if (root.classList.contains('tree-select-autosubmit') && input.form) {
                    input.form.requestSubmit();
                }
            });
        });

        if (filter) {
            filter.addEventListener('input', function () {
                var query = filter.value.trim().toLowerCase();
                root.querySelectorAll('.tree-select-item[data-tree-label]').forEach(function (item) {
                    var text = (item.getAttribute('data-tree-label') || '').toLowerCase();
                    var selfMatch = !query || text.indexOf(query) !== -1;
                    var childMatch = false;
                    item.querySelectorAll('.tree-select-item[data-tree-label]').forEach(function (child) {
                        var childText = (child.getAttribute('data-tree-label') || '').toLowerCase();
                        if (!query || childText.indexOf(query) !== -1) {
                            childMatch = true;
                            child.classList.remove('is-filtered-out');
                        } else {
                            child.classList.add('is-filtered-out');
                        }
                    });
                    if (selfMatch || childMatch) {
                        item.classList.remove('is-filtered-out');
                    } else {
                        item.classList.add('is-filtered-out');
                    }
                });
            });
        }

        document.addEventListener('click', function (event) {
            if (!root.contains(event.target)) {
                setOpen(false);
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && !panel.hidden) {
                setOpen(false);
                toggle.focus();
            }
        });
    });

    // ---------- 界面语言切换 ----------

    // 记录用户已修改过的表单：切换语言会整页刷新，先确认避免丢失未保存内容。
    function markFormDirty(event) {
        var element = event.target;
        if (!(element instanceof Element)) {
            return;
        }

        var form = element.closest('form[method="post"]');
        if (form) {
            form.setAttribute('data-dirty', '1');
        }
    }

    document.addEventListener('input', markFormDirty, true);
    document.addEventListener('change', markFormDirty, true);

    var languageSelect = document.querySelector('select[data-language-select]');
    if (languageSelect && languageSelect.form) {
        var languageForm = languageSelect.form;
        var initialCulture = languageSelect.value;

        languageSelect.addEventListener('change', function () {
            if (languageSelect.value === initialCulture) {
                return;
            }

            var dirtyForm = document.querySelector('form[method="post"][data-dirty="1"]');
            var message = languageSelect.getAttribute('data-unsaved-message');
            if (dirtyForm && message && !window.confirm(message)) {
                // 取消切换：恢复原语言，避免界面与服务端状态不一致。
                languageSelect.value = initialCulture;
                return;
            }

            languageForm.requestSubmit();
        });
    }

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

    // ---------- 行政区树：按级惰性加载 ----------

    // 拉取某节点的子级片段；已加载或加载中的容器直接跳过。
    function loadRegionChildren(container) {
        var url = container.getAttribute('data-children-url');
        if (!url
            || container.getAttribute('data-loaded') === '1'
            || container.getAttribute('data-loading') === '1') {
            return;
        }

        container.setAttribute('data-loading', '1');
        container.innerHTML = '<span class="region-children-loading" aria-hidden="true"></span>';

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('HTTP ' + response.status);
                }

                return response.text();
            })
            .then(function (html) {
                container.innerHTML = html;
                container.setAttribute('data-loaded', '1');
            })
            .catch(function () {
                // 失败保留重试入口：点击后重新拉取。
                container.innerHTML = '';
                var retry = document.createElement('button');
                retry.type = 'button';
                retry.className = 'region-children-retry';
                retry.textContent = container.getAttribute('data-error-text') || 'Load failed';
                retry.addEventListener('click', function () {
                    loadRegionChildren(container);
                });
                container.appendChild(retry);
            })
            .finally(function () {
                container.removeAttribute('data-loading');
            });
    }

    // 点击展开箭头时触发加载；Bootstrap collapse 只负责展开动画与 aria 状态。
    document.addEventListener('click', function (event) {
        var toggle = event.target.closest('.region-toggle[data-bs-toggle="collapse"]');
        if (!toggle) {
            return;
        }

        var target = document.getElementById(toggle.getAttribute('aria-controls'));
        var container = target && target.querySelector('[data-children-container]');
        if (container) {
            loadRegionChildren(container);
        }
    });

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

    // ---------- 译文页签状态 ----------

    // 页签上的“已翻译/未翻译”随输入实时更新：该语言任一字段有内容即视为已翻译。
    document.querySelectorAll('[data-translation-pane]').forEach(function (pane) {
        var badge = document.querySelector('[data-translation-status-for="' + pane.id + '"]');
        if (!badge) {
            return;
        }

        var inputs = pane.querySelectorAll('[data-translation-input]');
        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                var translated = Array.prototype.some.call(inputs, function (field) {
                    return field.value.trim().length > 0;
                });

                badge.classList.toggle('is-translated', translated);
                badge.classList.toggle('is-missing', !translated);
                badge.textContent = translated
                    ? badge.getAttribute('data-translated-text')
                    : badge.getAttribute('data-missing-text');
            });
        });
    });

    // “填入原文”：把默认语言内容填到当前译文输入框，并触发表单脏数据与状态更新。
    document.querySelectorAll('[data-copy-source]').forEach(function (button) {
        var target = document.getElementById(button.getAttribute('data-copy-source'));
        if (!target) {
            return;
        }

        button.addEventListener('click', function () {
            target.value = button.getAttribute('data-source-value') || '';
            target.dispatchEvent(new Event('input', { bubbles: true }));
            target.focus();

            var label = button.textContent;
            button.textContent = button.getAttribute('data-copied-text');
            window.setTimeout(function () {
                button.textContent = label;
            }, 1500);
        });
    });

    // 正常提交时清除脏标记，避免保存后跳转再触发一次离开提示。
    document.addEventListener('submit', function (event) {
        if (!(event.target instanceof HTMLFormElement) || event.defaultPrevented) {
            return;
        }

        document.querySelectorAll('form[data-dirty]').forEach(function (form) {
            form.removeAttribute('data-dirty');
        });
    });

    // 关闭页签或跳转离开时，若仍有未保存的表单修改则交给浏览器提示。
    window.addEventListener('beforeunload', function (event) {
        if (!document.querySelector('form[method="post"][data-dirty="1"]')) {
            return;
        }

        event.preventDefault();
        event.returnValue = '';
    });
})();
