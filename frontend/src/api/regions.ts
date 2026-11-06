// 行政区接口封装，只读浏览为主。
import type { PagedRegions, Region, RegionTree } from '../types';
import { apiFetch, throwIfFailed } from './client';

export async function listRegions(params: {
  page: number;
  pageSize: number;
  level?: number;
  countryCode?: string;
  admin1Code?: string;
  q?: string;
}): Promise<PagedRegions> {
  const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
  if (params.level !== undefined) query.set('level', String(params.level));
  if (params.countryCode) query.set('countryCode', params.countryCode);
  if (params.admin1Code) query.set('admin1Code', params.admin1Code);
  if (params.q) query.set('q', params.q);

  const response = await apiFetch(`/bff/regions?${query.toString()}`);
  await throwIfFailed(response);
  return (await response.json()) as PagedRegions;
}

export async function getRegion(id: string): Promise<Region> {
  const response = await apiFetch(`/bff/regions/${id}`);
  await throwIfFailed(response);
  return (await response.json()) as Region;
}

export async function getRegionTree(countryCode: string, depth: number = 2): Promise<RegionTree> {
  const response = await apiFetch(`/bff/regions/tree?countryCode=${encodeURIComponent(countryCode)}&depth=${depth}`);
  await throwIfFailed(response);
  return (await response.json()) as RegionTree;
}

export async function listCountries(): Promise<PagedRegions> {
  return listRegions({ page: 1, pageSize: 200, level: 1 });
}
