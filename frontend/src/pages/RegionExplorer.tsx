// 行政区层级浏览：国家列表 + 选中国家的层级树 + 搜索。
import { useCallback, useEffect, useState } from 'react';
import type { Region, RegionTree } from '../types';
import { getRegion, getRegionTree, listCountries, listRegions } from '../api/regions';
import { ApiError } from '../api/client';
import ErrorBanner from '../components/ErrorBanner';

type Tab = 'tree' | 'search';

export default function RegionExplorer() {
  const [countries, setCountries] = useState<Region[]>([]);
  const [selectedCountry, setSelectedCountry] = useState<string | null>(null);
  const [tree, setTree] = useState<RegionTree | null>(null);
  const [selectedRegionId, setSelectedRegionId] = useState<string | null>(null);
  const [selectedRegion, setSelectedRegion] = useState<Region | null>(null);
  const [tab, setTab] = useState<Tab>('tree');
  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Region[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // 初始化：加载国家列表（仅首次）。
  useEffect(() => {
    listCountries()
      .then((result) => {
        setCountries(result.items);
        if (result.items.length > 0) {
          setSelectedCountry(result.items[0].countryCode);
        }
      })
      .catch((cause: unknown) => {
        if (cause instanceof ApiError && cause.status === 401) return;
        setError(cause instanceof Error ? cause.message : '加载国家列表失败。');
      })
      .finally(() => setLoading(false));
  }, []);

  // 选中国家后加载层级树
  useEffect(() => {
    if (!selectedCountry) return;

    getRegionTree(selectedCountry, 3)
      .then(setTree)
      .catch((cause: unknown) => {
        if (cause instanceof ApiError && cause.status === 401) return;
        setError(cause instanceof Error ? cause.message : '加载行政区层级失败。');
      });
  }, [selectedCountry]);

  // 搜索
  const handleSearch = useCallback(async () => {
    if (!query.trim()) {
      setSearchResults([]);
      return;
    }
    setError(null);
    try {
      const result = await listRegions({
        page: 1,
        pageSize: 50,
        countryCode: selectedCountry ?? undefined,
        q: query.trim()
      });
      setSearchResults(result.items);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) return;
      setError(cause instanceof Error ? cause.message : '搜索失败。');
    }
  }, [query, selectedCountry]);

  const handleSelectRegion = async (id: string) => {
    setSelectedRegionId(id);
    setError(null);
    try {
      setSelectedRegion(await getRegion(id));
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) return;
      setError(cause instanceof Error ? cause.message : '加载行政区详情失败。');
    }
  };

  return (
    <section>
      <div className="toolbar">
        <h1>行政区浏览</h1>
        <div className="filters">
          <select value={selectedCountry ?? ''} onChange={(e) => setSelectedCountry(e.target.value || null)}>
            <option value="">选择国家</option>
            {countries.map((c) => (
              <option key={c.countryCode} value={c.countryCode}>
                {c.name} ({c.countryCode})
              </option>
            ))}
          </select>
          <div className="tabs">
            <button type="button" className={tab === 'tree' ? 'active' : ''} onClick={() => setTab('tree')}>
              层级树
            </button>
            <button type="button" className={tab === 'search' ? 'active' : ''} onClick={() => setTab('search')}>
              搜索
            </button>
          </div>
          {tab === 'search' ? (
            <>
              <input
                type="text"
                placeholder="搜索行政区名称…"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
              <button type="button" className="secondary" onClick={handleSearch}>
                查询
              </button>
            </>
          ) : null}
        </div>
      </div>

      {error ? <ErrorBanner message={error} /> : null}
      {loading ? <div className="loading">加载中…</div> : null}

      <div className="region-layout">
        {/* 左栏：树或搜索结果 */}
        <div className="region-panel">
          {tab === 'tree' ? (
            tree ? (
              <RegionTreeView node={tree} level={0} onSelect={handleSelectRegion} selectedId={selectedRegionId} />
            ) : (
              <p className="empty">选择一个国家查看其行政区层级。</p>
            )
          ) : searchResults.length > 0 ? (
            <RegionListView items={searchResults} onSelect={handleSelectRegion} selectedId={selectedRegionId} />
          ) : (
            <p className="empty">输入关键词后点击查询。</p>
          )}
        </div>

        {/* 右栏：详情 */}
        <div className="region-detail">
          {selectedRegion ? (
            <article>
              <h2>{selectedRegion.name}</h2>
              {selectedRegion.localName ? <p className="muted">本地名：{selectedRegion.localName}</p> : null}
              <dl className="detail-list">
                <div>
                  <dt>层级</dt>
                  <dd>{levelLabel(selectedRegion.level)}</dd>
                </div>
                <div>
                  <dt>代码</dt>
                  <dd>{selectedRegion.code}</dd>
                </div>
                <div>
                  <dt>国家</dt>
                  <dd>{selectedRegion.countryCode}</dd>
                </div>
                {selectedRegion.admin1Code ? (
                  <div>
                    <dt>Admin1 Code</dt>
                    <dd>{selectedRegion.admin1Code}</dd>
                  </div>
                ) : null}
                {selectedRegion.population != null ? (
                  <div>
                    <dt>人口</dt>
                    <dd>{selectedRegion.population.toLocaleString()}</dd>
                  </div>
                ) : null}
                {selectedRegion.latitude != null && selectedRegion.longitude != null ? (
                  <div>
                    <dt>经纬度</dt>
                    <dd>
                      {selectedRegion.latitude.toFixed(4)}, {selectedRegion.longitude.toFixed(4)}
                    </dd>
                  </div>
                ) : null}
                {selectedRegion.geoNameId != null ? (
                  <div>
                    <dt>GeoNames ID</dt>
                    <dd>{selectedRegion.geoNameId}</dd>
                  </div>
                ) : null}
              </dl>
            </article>
          ) : (
            <p className="empty">点击左侧行政区查看详情。</p>
          )}
        </div>
      </div>
    </section>
  );
}

function RegionTreeView({
  node,
  level,
  onSelect,
  selectedId
}: {
  node: RegionTree;
  level: number;
  onSelect: (id: string) => void;
  selectedId: string | null;
}) {
  const [expanded, setExpanded] = useState(level === 0);

  return (
    <div className={`tree-node level-${level}`}>
      <div className={`tree-row${selectedId === node.id ? ' selected' : ''}`} onClick={() => onSelect(node.id)}>
        {node.children.length > 0 ? (
          <button
            type="button"
            className="toggle"
            onClick={(e) => {
              e.stopPropagation();
              setExpanded(!expanded);
            }}
          >
            {expanded ? '▼' : '▶'}
          </button>
        ) : (
          <span className="toggle placeholder">•</span>
        )}
        <span className={`level-badge level-${node.level}`}>{levelLabel(node.level)}</span>
        <span className="name">{node.name}</span>
        {node.population != null ? <span className="muted">({formatPopulation(node.population)})</span> : null}
      </div>
      {expanded && node.children.length > 0 ? (
        <div className="tree-children">
          {node.children.map((child) => (
            <RegionTreeView key={child.id} node={child} level={level + 1} onSelect={onSelect} selectedId={selectedId} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function RegionListView({
  items,
  onSelect,
  selectedId
}: {
  items: Region[];
  onSelect: (id: string) => void;
  selectedId: string | null;
}) {
  return (
    <ul className="list">
      {items.map((item) => (
        <li
          key={item.id}
          className={`list-item${selectedId === item.id ? ' selected' : ''}`}
          onClick={() => onSelect(item.id)}
        >
          <span className={`level-badge level-${item.level}`}>{levelLabel(item.level)}</span>
          <span className="name">{item.name}</span>
          <span className="muted">{item.countryCode}</span>
        </li>
      ))}
    </ul>
  );
}

function levelLabel(level: number): string {
  return level === 1 ? '国家' : level === 2 ? '省/州' : level === 3 ? '市' : `L${level}`;
}

function formatPopulation(pop: number): string {
  if (pop >= 1_000_000) return `${(pop / 1_000_000).toFixed(1)}M`;
  if (pop >= 1_000) return `${(pop / 1_000).toFixed(0)}K`;
  return pop.toString();
}
