// 新闻接口封装，所有请求都经过 BFF。
import type { CurrentUser, NewsArticle, NewsInput, PagedNews } from '../types';
import { apiFetch, setCsrfToken, throwIfFailed } from './client';

export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await apiFetch('/bff/me');
  await throwIfFailed(response);
  const user = (await response.json()) as CurrentUser;
  setCsrfToken(user.csrfToken);
  return user;
}

export async function logout(): Promise<void> {
  const response = await apiFetch('/bff/logout', { method: 'POST' });
  await throwIfFailed(response);
}

export async function listNews(page: number, pageSize: number, status: string): Promise<PagedNews> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status) {
    query.set('status', status);
  }

  const response = await apiFetch(`/bff/news?${query.toString()}`);
  await throwIfFailed(response);
  return (await response.json()) as PagedNews;
}

export async function getNews(id: string): Promise<{ article: NewsArticle; etag: string | null }> {
  const response = await apiFetch(`/bff/news/${id}`);
  await throwIfFailed(response);
  return {
    article: (await response.json()) as NewsArticle,
    etag: response.headers.get('ETag')
  };
}

export async function createNews(input: NewsInput): Promise<{ article: NewsArticle; etag: string | null }> {
  const response = await apiFetch('/bff/news', { method: 'POST', body: JSON.stringify(input) });
  await throwIfFailed(response);
  return {
    article: (await response.json()) as NewsArticle,
    etag: response.headers.get('ETag')
  };
}

export async function updateNews(
  id: string,
  etag: string,
  input: NewsInput
): Promise<{ article: NewsArticle; etag: string | null }> {
  const response = await apiFetch(`/bff/news/${id}`, {
    method: 'PUT',
    headers: { 'If-Match': etag },
    body: JSON.stringify(input)
  });
  await throwIfFailed(response);
  return {
    article: (await response.json()) as NewsArticle,
    etag: response.headers.get('ETag')
  };
}

export async function deleteNews(id: string, etag: string): Promise<void> {
  const response = await apiFetch(`/bff/news/${id}`, {
    method: 'DELETE',
    headers: { 'If-Match': etag }
  });
  await throwIfFailed(response);
}
