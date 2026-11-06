// 统一封装 BFF 请求：注入 CSRF 令牌，未登录时跳转登录页。
let csrfToken = '';

export function setCsrfToken(value: string): void {
  csrfToken = value;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly problem?: unknown
  ) {
    super(message);
  }
}

export function redirectToLogin(): void {
  const returnUrl = `${window.location.pathname}${window.location.search}`;
  window.location.href = `/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const method = (init.method ?? 'GET').toUpperCase();
  const headers = new Headers(init.headers);
  if (init.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (method !== 'GET' && method !== 'HEAD' && csrfToken) {
    headers.set('X-CSRF-Token', csrfToken);
  }

  const response = await fetch(path, { ...init, method, headers, credentials: 'same-origin' });
  if (response.status === 401) {
    redirectToLogin();
    throw new ApiError(401, '未登录或会话已过期。');
  }

  return response;
}

// 把 ProblemDetails 或自定义错误码转成可读信息。
export async function throwIfFailed(response: Response): Promise<void> {
  if (response.ok) {
    return;
  }

  let problem: unknown;
  try {
    problem = await response.json();
  } catch {
    problem = undefined;
  }

  const data = problem as { title?: string; detail?: string; code?: string } | undefined;
  if (data?.code === 'csrf_failed') {
    throw new ApiError(response.status, '安全令牌校验失败，请刷新页面后重试。', problem);
  }

  throw new ApiError(response.status, data?.title ?? data?.detail ?? `请求失败（${response.status}）`, problem);
}
