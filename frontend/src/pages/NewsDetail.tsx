// 新闻详情：只读展示并保留 ETag 供删除使用。
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { deleteNews, getNews } from '../api/news';
import ErrorBanner from '../components/ErrorBanner';
import type { NewsArticle } from '../types';

export default function NewsDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [article, setArticle] = useState<NewsArticle | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) {
      setError('缺少新闻编号。');
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    getNews(id)
      .then((result) => {
        if (!cancelled) {
          setArticle(result.article);
          setEtag(result.etag);
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : '加载新闻失败。');
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
  }, [id]);

  async function handleDelete() {
    if (!id || !etag) {
      setError('缺少版本信息，请刷新后重试。');
      return;
    }
    if (!window.confirm('确认删除这条新闻？')) {
      return;
    }

    try {
      await deleteNews(id, etag);
      navigate('/');
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        setError('这条新闻已被其他请求修改，请刷新页面后重试。');
        return;
      }
      setError(cause instanceof Error ? cause.message : '删除失败。');
    }
  }

  if (loading) {
    return <div className="loading">加载中…</div>;
  }

  if (error) {
    return (
      <section>
        <ErrorBanner message={error} />
        <Link to="/">返回列表</Link>
      </section>
    );
  }

  if (!article) {
    return <ErrorBanner message="新闻不存在。" />;
  }

  return (
    <section>
      <div className="toolbar">
        <h1>{article.title}</h1>
        <div className="actions">
          <Link to={`/news/${article.id}/edit`}>编辑</Link>
          <button type="button" className="link danger" onClick={handleDelete}>
            删除
          </button>
          <Link to="/">返回列表</Link>
        </div>
      </div>

      <dl className="detail">
        <dt>分类</dt>
        <dd>{article.category}</dd>
        <dt>状态</dt>
        <dd>{article.status}</dd>
        <dt>作者</dt>
        <dd>{article.author}</dd>
        <dt>发布时间</dt>
        <dd>{article.publishedAt ? new Date(article.publishedAt).toLocaleString('zh-CN') : '未发布'}</dd>
        <dt>更新时间</dt>
        <dd>{new Date(article.updatedAt).toLocaleString('zh-CN')}</dd>
      </dl>

      {article.summary ? <p className="summary">{article.summary}</p> : null}
      <div className="content">{article.content}</div>
    </section>
  );
}
