// 定义 BFF 返回的新闻与分页数据结构。
export interface NewsArticle {
  id: string;
  title: string;
  summary: string | null;
  content: string;
  category: string;
  status: string;
  author: string;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedNews {
  items: NewsArticle[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NewsInput {
  title: string;
  summary: string | null;
  content: string;
  category: string;
  author: string;
  status: string;
}

export interface CurrentUser {
  authenticated: boolean;
  name: string;
  csrfToken: string;
}

export interface Region {
  id: string;
  parentId: string | null;
  level: number;
  countryCode: string;
  admin1Code: string | null;
  code: string;
  name: string;
  asciiName: string | null;
  localName: string | null;
  latitude: number | null;
  longitude: number | null;
  population: number | null;
  geoNameId: number | null;
}

export interface RegionTree {
  id: string;
  level: number;
  countryCode: string;
  admin1Code: string | null;
  code: string;
  name: string;
  population: number | null;
  children: RegionTree[];
}

export interface PagedRegions {
  items: Region[];
  totalCount: number;
  page: number;
  pageSize: number;
}
