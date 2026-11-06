// 新闻新建与编辑表单，编辑时使用 ETag 做乐观并发控制。
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { createNews, getNews, updateNews } from '../api/news';
import ErrorBanner from '../components/ErrorBanner';
import type { NewsInput } from '../types';

const STATUS_OPTIONS = [
  { value: 'Draft', label: '草稿' },
  { value: 'Published', label: '已发布' },
  { value: 'Archived', label: '已归档' }
];

const EMPTY_FORM: NewsInput = {
  title: '',
  summary: '',
  content: '',
  category: '',
  author: '',
  status: 'Draft'
};

function extractFieldErrors(problem: unknown): Record<string, string> {
  const data = problem as { errors?: Record<string, string[]> } | undefined;
  if (!data?.errors) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(data.errors).map(([field, messages]) => [field.toLowerCase(), messages.join(' ')])
  );
}

export default function NewsForm({ mode }: { mode: 'create' | 'edit' }) {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [form, setForm] = useState<NewsInput>(EMPTY_FORM);
  const [etag, setEtag] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(mode === 'edit');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (mode !== 'edit' || !id) {
      return;
    }

    let cancelled = false;
    getNews(id)
      .then((result) => {
        if (cancelled) {
          return;
        }
        setForm({
          title: result.article.title,
          summary: result.article.summary ?? '',
          content: result.article.content,
          category: result.article.category,
          author: result.article.author,
          status: result.article.status
        });
        setEtag(result.etag);
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
  }, [id, mode]);

  function updateField(field: keyof NewsInput, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setFieldErrors({});

    if (!form.title.trim() || !form.content.trim() || !form.category.trim() || !form.author.trim()) {
      setError('标题、正文、分类和作者均为必填项。');
      return;
    }

    if (mode === 'edit' && (!id || !etag)) {
      setError('缺少版本信息，请刷新页面后重试。');
      return;
    }

    setSaving(true);
    try {
      if (mode === 'create') {
        const result = await createNews(form);
        navigate(`/news/${result.article.id}`);
        return;
      }

      const result = await updateNews(id!, etag!, form);
      navigate(`/news/${result.article.id}`);
    } catch (cause) {
      if (cause instanceof ApiError) {
        if (cause.status === 409) {
          setError('这条新闻已被其他请求修改，正在为你刷新最新内容。');
          if (mode === 'edit' && id) {
            const latest = await getNews(id);
            setForm({
              title: latest.article.title,
              summary: latest.article.summary ?? '',
              content: latest.article.content,
              category: latest.article.category,
              author: latest.article.author,
              status: latest.article.status
            });
            setEtag(latest.etag);
          }
          return;
        }

        if (cause.status === 428) {
          setError('缺少版本信息，请刷新页面后重试。');
          return;
        }

        if (cause.status === 400) {
          setFieldErrors(extractFieldErrors(cause.problem));
        }
      }

      setError(cause instanceof Error ? cause.message : '保存失败。');
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <div className="loading">加载中…</div>;
  }

  return (
    <section>
      <div className="toolbar">
        <h1>{mode === 'create' ? '新建新闻' : '编辑新闻'}</h1>
        <Link to={mode === 'edit' && id ? `/news/${id}` : '/'}>取消</Link>
      </div>

      {error ? <ErrorBanner message={error} /> : null}

      <form className="news-form" onSubmit={handleSubmit}>
        <label>
          标题
          <input
            value={form.title}
            onChange={(event) => updateField('title', event.target.value)}
            maxLength={200}
            required
          />
          {fieldErrors.title ? <span className="field-error">{fieldErrors.title}</span> : null}
        </label>

        <label>
          摘要
          <textarea
            value={form.summary ?? ''}
            onChange={(event) => updateField('summary', event.target.value)}
            rows={2}
          />
        </label>

        <label>
          正文
          <textarea
            value={form.content}
            onChange={(event) => updateField('content', event.target.value)}
            rows={10}
            required
          />
          {fieldErrors.content ? <span className="field-error">{fieldErrors.content}</span> : null}
        </label>

        <label>
          分类
          <input
            value={form.category}
            onChange={(event) => updateField('category', event.target.value)}
            maxLength={100}
            required
          />
          {fieldErrors.category ? <span className="field-error">{fieldErrors.category}</span> : null}
        </label>

        <label>
          作者
          <input
            value={form.author}
            onChange={(event) => updateField('author', event.target.value)}
            maxLength={100}
            required
          />
          {fieldErrors.author ? <span className="field-error">{fieldErrors.author}</span> : null}
        </label>

        <label>
          状态
          <select value={form.status} onChange={(event) => updateField('status', event.target.value)}>
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <button type="submit" className="button" disabled={saving}>
          {saving ? '保存中…' : '保存'}
        </button>
      </form>
    </section>
  );
}
