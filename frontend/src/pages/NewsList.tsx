// 新闻列表：状态筛选、分页与删除入口。
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { deleteNews, getNews, listNews } from '../api/news';
import ErrorBanner from '../components/ErrorBanner';
import type { PagedNews } from '../types';

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: '', label: '全部状态' },
  { value: 'Draft', label: '草稿' },
  { value: 'Published', label: '已发布' },
  { value: 'Archived', label: '已归档' }
];

function statusLabel(status: string): string {
  return STATUS_OPTIONS.find((option) => option.value === status)?.label ?? status;
}

export default function NewsList() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [version, setVersion] = useState(0);
  const [data, setData] = useState<PagedNews | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    listNews(page, PAGE_SIZE, status)
      .then((result) => {
        if (!cancelled) {
          setData(result);
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : '加载新闻列表失败。');
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [page, status, version]);

  async function handleDelete(id: string) {
    if (!window.confirm('确认删除这条新闻？')) {
      return;
    }

    try {
      const { etag } = await getNews(id);
      if (!etag) {
        setError('缺少版本信息，请刷新后重试。');
        return;
      }

      await deleteNews(id, etag);
      setVersion((current) => current + 1);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        setError('这条新闻已被其他请求修改，列表已刷新，请重试。');
        setVersion((current) => current + 1);
        return;
      }
      setError(cause instanceof Error ? cause.message : '删除失败。');
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <section>
      <div className="toolbar">
        <h1>新闻列表</h1>
        <div className="filters">
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
            }}
          >
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <Link className="button" to="/news/new">
            新建新闻
          </Link>
        </div>
      </div>

      {error ? <ErrorBanner message={error} /> : null}
      {loading ? <div className="loading">加载中…</div> : null}

      {data && !loading ? (
        data.items.length === 0 ? (
          <p className="empty">暂无新闻。</p>
        ) : (
          <>
            <table className="data-table">
              <thead>
                <tr>
                  <th>标题</th>
                  <th>分类</th>
                  <th>状态</th>
                  <th>更新时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((article) => (
                  <tr key={article.id}>
                    <td>
                      <Link to={`/news/${article.id}`}>{article.title}</Link>
                    </td>
                    <td>{article.category}</td>
                    <td>{statusLabel(article.status)}</td>
                    <td>{new Date(article.updatedAt).toLocaleString('zh-CN')}</td>
                    <td className="actions">
                      <Link to={`/news/${article.id}/edit`}>编辑</Link>
                      <button type="button" className="link danger" onClick={() => handleDelete(article.id)}>
                        删除
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="pager">
              <button
                type="button"
                className="secondary"
                disabled={page <= 1}
                onClick={() => setPage((current) => current - 1)}
              >
                上一页
              </button>
              <span>
                第 {page} / {totalPages} 页，共 {data.totalCount} 条
              </span>
              <button
                type="button"
                className="secondary"
                disabled={page >= totalPages}
                onClick={() => setPage((current) => current + 1)}
              >
                下一页
              </button>
            </div>
          </>
        )
      ) : null}
    </section>
  );
}
