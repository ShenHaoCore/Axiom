// 启动时读取当前用户，未登录交由客户端统一跳转。
import { useEffect, useState } from 'react';
import { Route, Routes } from 'react-router-dom';
import { fetchCurrentUser } from './api/news';
import { ApiError } from './api/client';
import Layout from './components/Layout';
import ErrorBanner from './components/ErrorBanner';
import NewsDetail from './pages/NewsDetail';
import NewsForm from './pages/NewsForm';
import NewsList from './pages/NewsList';
import type { CurrentUser } from './types';

export default function App() {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchCurrentUser()
      .then(setUser)
      .catch((cause: unknown) => {
        // 401 已由客户端跳转登录页，这里无需重复提示。
        if (cause instanceof ApiError && cause.status === 401) {
          return;
        }
        setError(cause instanceof Error ? cause.message : '加载会话失败。');
      });
  }, []);

  if (error) {
    return (
      <div className="page">
        <ErrorBanner message={error} />
      </div>
    );
  }

  if (!user) {
    return <div className="loading">正在加载会话…</div>;
  }

  return (
    <Routes>
      <Route element={<Layout user={user} />}>
        <Route index element={<NewsList />} />
        <Route path="news/new" element={<NewsForm mode="create" />} />
        <Route path="news/:id" element={<NewsDetail />} />
        <Route path="news/:id/edit" element={<NewsForm mode="edit" />} />
        <Route path="*" element={<ErrorBanner message="页面不存在。" />} />
      </Route>
    </Routes>
  );
}
