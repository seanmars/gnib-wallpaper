# Bing Wallpaper Archive 多國今日桌布抓取實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用 Playwright 從 https://bingwallpaper.anerg.com/ 抓取所有國家今日的桌布 (UHD 4K), 並儲存圖檔與 metadata 至本地檔案系統.

**Architecture:** 採用兩階段抓取流程: (1) 從首頁動態探索所有支援的國家代碼; (2) 對每個國家造訪 `/<country_code>` 取得今日桌布的 detail 頁連結, 再進入 detail 頁解析 4K 下載 URL 與 metadata. 圖片以 HTTP 直接下載 (而非 Playwright), 並輸出 metadata JSON 供後續批次使用.

**Tech Stack:**
- Node.js 20+ (LTS)
- TypeScript 5+
- Playwright (Chromium)
- pnpm (套件管理, 可改用 npm)
- 無資料庫, 純檔案系統輸出

---

## File Structure

```
wallpaper/
├── plan.md                          # 本檔案
├── package.json
├── tsconfig.json
├── playwright.config.ts             # Playwright 設定 (timeout, headless)
├── src/
│   ├── index.ts                     # CLI 進入點, 串接所有步驟
│   ├── config.ts                    # 常數: BASE_URL, OUTPUT_DIR, 預設解析度
│   ├── discoverCountries.ts         # 從首頁 navigation 擷取國家清單
│   ├── fetchCountryToday.ts         # 取得單一國家今日桌布的 detail 連結
│   ├── parseDetailPage.ts           # 從 detail 頁取得 4K / 2K / HD 下載 URL + metadata
│   ├── downloadImage.ts             # 用 fetch 下載圖檔, 處理副檔名與檔案命名
│   ├── writeMetadata.ts             # 輸出 metadata.json
│   └── types.ts                     # Country, Wallpaper, DownloadResult 等型別
├── tests/
│   ├── discoverCountries.test.ts
│   ├── parseDetailPage.test.ts
│   └── fixtures/                    # 儲存抓取下來的 HTML 範本供測試使用
│       ├── homepage.html
│       └── detail-us.html
└── wallpapers/                      # 輸出目錄 (gitignore)
    └── <country_code>/
        ├── 2026-05-22-<slug>.jpg
        └── 2026-05-22-<slug>.json
```

**檔案責任拆分理由:**
- `discoverCountries` 與 `parseDetailPage` 兩者都是 HTML 解析, 但作用於不同頁面, 各自獨立易於測試.
- `downloadImage` 不依賴 Playwright, 純 `fetch`, 因此可單獨平行化或加上 retry.
- `writeMetadata` 隔離出來方便日後改成寫入資料庫.

---

## Output 格式範例

每個國家輸出兩個檔案:

`wallpapers/us/2026-05-22-SichuanTea.jpg` — 4K 圖檔

`wallpapers/us/2026-05-22-SichuanTea.json`:
```json
{
  "country_code": "us",
  "country_name": "United States",
  "date": "2026-05-22",
  "slug": "SichuanTea",
  "title": "Tea terraces in Sichuan Province, China",
  "copyright": "© lzf/Shutterstock",
  "detail_url": "https://bingwallpaper.anerg.com/detail/us/SichuanTea",
  "download_urls": {
    "uhd_4k":   "https://imgproxy.nanxiongnandi.com/.../w:3840/q:100/att:1/...",
    "qhd_2k":   "https://imgproxy.nanxiongnandi.com/.../w:2560/q:100/att:1/...",
    "fhd_1080": "https://imgproxy.nanxiongnandi.com/.../w:1920/q:100/att:1/..."
  },
  "downloaded_resolution": "uhd_4k",
  "image_path": "wallpapers/us/2026-05-22-SichuanTea.jpg",
  "fetched_at": "2026-05-22T10:30:00Z"
}
```

---

## 重要的網站結構備忘 (供 agent 參考)

**首頁 (`https://bingwallpaper.anerg.com/`):**
- Navigation 列出所有國家, link 形式為 `<a href="/<code>">`
- 已知國家: au, ca, cn, de, es, fr, it, jp, nz, uk, us (共 11 國, 但實作要動態抓取)

**國家頁 (`/<country_code>`, e.g. `/us`):**
- 第一張卡片即今日桌布
- 結構: `<a href="/detail/<code>/<slug>"><img src="...imgproxy.../w:800/..."></a>`
- Selector: `a[href^="/detail/<code>/"]:first-of-type` 或更穩固的 `main a[href*="/detail/"]:first-of-type`

**Detail 頁 (`/detail/<code>/<slug>`):**
- 含多解析度下載連結, 文字為 "Download 4K", "Download 2K", "Download 1920x1080"
- 4K URL 含 `w:3840/q:100/att:1`
- 標題與版權位於頁面文字中, 格式: `<title> (© <author>/<source>)`

---

## Task 1: 專案初始化

**Files:**
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `.gitignore`
- Create: `playwright.config.ts`

- [ ] **Step 1: 建立 package.json**

```json
{
  "name": "bing-wallpaper-scraper",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "scrape": "tsx src/index.ts",
    "test": "vitest run",
    "test:watch": "vitest"
  },
  "dependencies": {
    "playwright": "^1.49.0"
  },
  "devDependencies": {
    "@types/node": "^22.0.0",
    "tsx": "^4.19.0",
    "typescript": "^5.6.0",
    "vitest": "^2.1.0"
  }
}
```

- [ ] **Step 2: 建立 tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "esModuleInterop": true,
    "strict": true,
    "skipLibCheck": true,
    "resolveJsonModule": true,
    "outDir": "dist",
    "rootDir": "src"
  },
  "include": ["src/**/*", "tests/**/*"]
}
```

- [ ] **Step 3: 建立 .gitignore**

```gitignore
node_modules/
dist/
wallpapers/
*.log
.DS_Store
```

- [ ] **Step 4: 建立 playwright.config.ts (僅給 e2e fixture 使用)**

```typescript
// 此檔案僅做為 Playwright 預設行為的參考, 主程式直接用 playwright API
export default {
  use: {
    headless: true,
    viewport: { width: 1280, height: 800 },
    userAgent: 'Mozilla/5.0 (compatible; bing-wallpaper-bot/0.1)',
  },
  timeout: 30_000,
};
```

- [ ] **Step 5: 安裝依賴**

Run: `pnpm install && pnpm exec playwright install chromium`
Expected: 套件安裝完成, Chromium 下載完成 (無錯誤)

- [ ] **Step 6: Commit**

```bash
git init
git add package.json tsconfig.json .gitignore playwright.config.ts plan.md
git commit -m "chore: bootstrap bing wallpaper scraper project"
```

---

## Task 2: 共用型別與設定

**Files:**
- Create: `src/types.ts`
- Create: `src/config.ts`

- [ ] **Step 1: 建立 src/types.ts**

```typescript
export type CountryCode = string; // e.g. "us", "jp"

export interface Country {
  code: CountryCode;
  name: string; // e.g. "United States"
}

export interface DetailLink {
  country: Country;
  slug: string;
  detailUrl: string; // absolute URL
}

export type Resolution = 'uhd_4k' | 'qhd_2k' | 'fhd_1080';

export interface Wallpaper {
  country: Country;
  slug: string;
  title: string;
  copyright: string;
  detailUrl: string;
  downloadUrls: Partial<Record<Resolution, string>>;
}

export interface DownloadResult {
  wallpaper: Wallpaper;
  resolution: Resolution;
  imagePath: string;
  bytes: number;
  fetchedAt: string; // ISO 8601 UTC
}
```

- [ ] **Step 2: 建立 src/config.ts**

```typescript
export const BASE_URL = 'https://bingwallpaper.anerg.com';
export const OUTPUT_DIR = 'wallpapers';
export const RESOLUTION_PRIORITY = ['uhd_4k', 'qhd_2k', 'fhd_1080'] as const;
export const NAV_TIMEOUT_MS = 20_000;
export const PER_COUNTRY_RETRY = 2;
export const USER_AGENT = 'Mozilla/5.0 (compatible; bing-wallpaper-bot/0.1)';
```

- [ ] **Step 3: Commit**

```bash
git add src/types.ts src/config.ts
git commit -m "feat: add shared types and config constants"
```

---

## Task 3: 探索國家清單 (TDD)

**Files:**
- Create: `tests/fixtures/homepage.html` (從真實首頁存檔)
- Create: `tests/discoverCountries.test.ts`
- Create: `src/discoverCountries.ts`

- [ ] **Step 1: 抓取首頁 HTML 作為 fixture**

Run:
```bash
mkdir -p tests/fixtures
curl -A "Mozilla/5.0" https://bingwallpaper.anerg.com/ -o tests/fixtures/homepage.html
```
Expected: `tests/fixtures/homepage.html` 約數十 KB, 內含 `<a href="/us">`, `<a href="/jp">` 等連結.

- [ ] **Step 2: 寫失敗測試 (tests/discoverCountries.test.ts)**

```typescript
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { parseCountriesFromHtml } from '../src/discoverCountries';

describe('parseCountriesFromHtml', () => {
  const html = readFileSync('tests/fixtures/homepage.html', 'utf-8');

  it('extracts all 11 known country codes', () => {
    const countries = parseCountriesFromHtml(html);
    const codes = countries.map((c) => c.code).sort();
    expect(codes).toEqual(['au', 'ca', 'cn', 'de', 'es', 'fr', 'it', 'jp', 'nz', 'uk', 'us']);
  });

  it('includes a human-readable name', () => {
    const countries = parseCountriesFromHtml(html);
    const us = countries.find((c) => c.code === 'us');
    expect(us?.name).toMatch(/United States|US/i);
  });

  it('ignores non-country links such as /archive or /detail', () => {
    const countries = parseCountriesFromHtml(html);
    const codes = countries.map((c) => c.code);
    expect(codes).not.toContain('archive');
    expect(codes).not.toContain('detail');
  });
});
```

- [ ] **Step 3: 執行測試確認失敗**

Run: `pnpm test discoverCountries`
Expected: FAIL — `parseCountriesFromHtml is not a function` (模組不存在)

- [ ] **Step 4: 實作 src/discoverCountries.ts**

```typescript
import { chromium } from 'playwright';
import { BASE_URL, USER_AGENT, NAV_TIMEOUT_MS } from './config.js';
import type { Country } from './types.js';

const KNOWN_NON_COUNTRY_PATHS = new Set(['archive', 'detail', 'about', 'api', '']);
const COUNTRY_CODE_PATTERN = /^[a-z]{2}$/;

export function parseCountriesFromHtml(html: string): Country[] {
  const linkPattern = /<a[^>]+href="\/([a-z]+)"[^>]*>([^<]*)<\/a>/gi;
  const found = new Map<string, string>();
  let match: RegExpExecArray | null;
  while ((match = linkPattern.exec(html)) !== null) {
    const code = match[1].toLowerCase();
    if (KNOWN_NON_COUNTRY_PATHS.has(code)) continue;
    if (!COUNTRY_CODE_PATTERN.test(code)) continue;
    const name = match[2].trim() || code.toUpperCase();
    if (!found.has(code)) found.set(code, name);
  }
  return [...found.entries()].map(([code, name]) => ({ code, name }));
}

export async function discoverCountries(): Promise<Country[]> {
  const browser = await chromium.launch();
  try {
    const ctx = await browser.newContext({ userAgent: USER_AGENT });
    const page = await ctx.newPage();
    await page.goto(BASE_URL, { timeout: NAV_TIMEOUT_MS, waitUntil: 'domcontentloaded' });
    const html = await page.content();
    return parseCountriesFromHtml(html);
  } finally {
    await browser.close();
  }
}
```

- [ ] **Step 5: 執行測試確認通過**

Run: `pnpm test discoverCountries`
Expected: PASS (3 個測試全綠)

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/homepage.html tests/discoverCountries.test.ts src/discoverCountries.ts
git commit -m "feat: discover country list from homepage navigation"
```

---

## Task 4: 取得今日桌布的 detail 連結 (TDD)

**Files:**
- Create: `tests/fetchCountryToday.test.ts`
- Create: `src/fetchCountryToday.ts`

- [ ] **Step 1: 抓取 /us 國家頁作為 fixture**

Run:
```bash
curl -A "Mozilla/5.0" https://bingwallpaper.anerg.com/us -o tests/fixtures/country-us.html
```
Expected: 檔案存在, 內含至少一個 `/detail/us/` 連結.

- [ ] **Step 2: 寫失敗測試 (tests/fetchCountryToday.test.ts)**

```typescript
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { parseTodayDetailLink } from '../src/fetchCountryToday';

const country = { code: 'us', name: 'United States' };

describe('parseTodayDetailLink', () => {
  const html = readFileSync('tests/fixtures/country-us.html', 'utf-8');

  it('returns the first /detail/us/<slug> link as today', () => {
    const link = parseTodayDetailLink(html, country);
    expect(link).not.toBeNull();
    expect(link!.detailUrl).toMatch(/^https:\/\/bingwallpaper\.anerg\.com\/detail\/us\/[^/]+$/);
    expect(link!.slug.length).toBeGreaterThan(0);
  });

  it('returns null when no detail link present', () => {
    const link = parseTodayDetailLink('<html><body>no links here</body></html>', country);
    expect(link).toBeNull();
  });
});
```

- [ ] **Step 3: 執行測試確認失敗**

Run: `pnpm test fetchCountryToday`
Expected: FAIL — module not found

- [ ] **Step 4: 實作 src/fetchCountryToday.ts**

```typescript
import { chromium } from 'playwright';
import { BASE_URL, USER_AGENT, NAV_TIMEOUT_MS } from './config.js';
import type { Country, DetailLink } from './types.js';

export function parseTodayDetailLink(html: string, country: Country): DetailLink | null {
  const pattern = new RegExp(`href="(/detail/${country.code}/([^"/?#]+))"`, 'i');
  const match = pattern.exec(html);
  if (!match) return null;
  return {
    country,
    slug: match[2],
    detailUrl: `${BASE_URL}${match[1]}`,
  };
}

export async function fetchCountryToday(country: Country): Promise<DetailLink | null> {
  const browser = await chromium.launch();
  try {
    const ctx = await browser.newContext({ userAgent: USER_AGENT });
    const page = await ctx.newPage();
    await page.goto(`${BASE_URL}/${country.code}`, {
      timeout: NAV_TIMEOUT_MS,
      waitUntil: 'domcontentloaded',
    });
    const html = await page.content();
    return parseTodayDetailLink(html, country);
  } finally {
    await browser.close();
  }
}
```

- [ ] **Step 5: 執行測試確認通過**

Run: `pnpm test fetchCountryToday`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/country-us.html tests/fetchCountryToday.test.ts src/fetchCountryToday.ts
git commit -m "feat: extract today's detail link from each country page"
```

---

## Task 5: 解析 detail 頁取得 4K URL 與 metadata (TDD)

**Files:**
- Create: `tests/fixtures/detail-us.html`
- Create: `tests/parseDetailPage.test.ts`
- Create: `src/parseDetailPage.ts`

- [ ] **Step 1: 抓取 detail 頁作為 fixture**

Run:
```bash
# 替換 SichuanTea 為 country-us.html 中實際存在的 slug
curl -A "Mozilla/5.0" https://bingwallpaper.anerg.com/detail/us/SichuanTea -o tests/fixtures/detail-us.html
```
Expected: HTML 內含 "Download 4K", "Download 2K", "Download 1920x1080" 字串.

- [ ] **Step 2: 寫失敗測試 (tests/parseDetailPage.test.ts)**

```typescript
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { parseDetailHtml } from '../src/parseDetailPage';

const country = { code: 'us', name: 'United States' };
const detailLink = {
  country,
  slug: 'SichuanTea',
  detailUrl: 'https://bingwallpaper.anerg.com/detail/us/SichuanTea',
};

describe('parseDetailHtml', () => {
  const html = readFileSync('tests/fixtures/detail-us.html', 'utf-8');
  const wallpaper = parseDetailHtml(html, detailLink);

  it('extracts a non-empty title', () => {
    expect(wallpaper.title.length).toBeGreaterThan(0);
  });

  it('extracts the copyright string', () => {
    expect(wallpaper.copyright).toMatch(/©/);
  });

  it('extracts 4K download URL with w:3840', () => {
    expect(wallpaper.downloadUrls.uhd_4k).toMatch(/w:3840/);
  });

  it('extracts 2K download URL with w:2560', () => {
    expect(wallpaper.downloadUrls.qhd_2k).toMatch(/w:2560/);
  });

  it('extracts 1080p download URL with w:1920', () => {
    expect(wallpaper.downloadUrls.fhd_1080).toMatch(/w:1920/);
  });
});
```

- [ ] **Step 3: 執行測試確認失敗**

Run: `pnpm test parseDetailPage`
Expected: FAIL — module not found

- [ ] **Step 4: 實作 src/parseDetailPage.ts**

```typescript
import { chromium } from 'playwright';
import { USER_AGENT, NAV_TIMEOUT_MS } from './config.js';
import type { DetailLink, Wallpaper, Resolution } from './types.js';

const RES_PATTERNS: Array<{ key: Resolution; widthMarker: string }> = [
  { key: 'uhd_4k', widthMarker: 'w:3840' },
  { key: 'qhd_2k', widthMarker: 'w:2560' },
  { key: 'fhd_1080', widthMarker: 'w:1920' },
];

function extractUrlContaining(html: string, marker: string): string | undefined {
  const re = new RegExp(`https?://[^"'\\s]*${marker}[^"'\\s]*`, 'i');
  return re.exec(html)?.[0];
}

function extractTitleAndCopyright(html: string): { title: string; copyright: string } {
  // alt 例: "Tea terraces in Sichuan Province, China (© lzf/Shutterstock)(Bing United States)"
  const altMatch = /alt="([^"]+)"/i.exec(html);
  if (altMatch) {
    const alt = altMatch[1];
    const m = /^(.+?)\s*\((©[^)]+)\)/.exec(alt);
    if (m) return { title: m[1].trim(), copyright: m[2].trim() };
  }
  // fallback: <title>
  const t = /<title>([^<]+)<\/title>/i.exec(html)?.[1] ?? '';
  return { title: t.trim(), copyright: '' };
}

export function parseDetailHtml(html: string, link: DetailLink): Wallpaper {
  const downloadUrls: Wallpaper['downloadUrls'] = {};
  for (const { key, widthMarker } of RES_PATTERNS) {
    const url = extractUrlContaining(html, widthMarker);
    if (url) downloadUrls[key] = url;
  }
  const { title, copyright } = extractTitleAndCopyright(html);
  return {
    country: link.country,
    slug: link.slug,
    title,
    copyright,
    detailUrl: link.detailUrl,
    downloadUrls,
  };
}

export async function fetchAndParseDetail(link: DetailLink): Promise<Wallpaper> {
  const browser = await chromium.launch();
  try {
    const ctx = await browser.newContext({ userAgent: USER_AGENT });
    const page = await ctx.newPage();
    await page.goto(link.detailUrl, { timeout: NAV_TIMEOUT_MS, waitUntil: 'domcontentloaded' });
    const html = await page.content();
    return parseDetailHtml(html, link);
  } finally {
    await browser.close();
  }
}
```

- [ ] **Step 5: 執行測試確認通過**

Run: `pnpm test parseDetailPage`
Expected: PASS (5 個測試全綠)

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/detail-us.html tests/parseDetailPage.test.ts src/parseDetailPage.ts
git commit -m "feat: parse detail page for 4K/2K/HD URLs and metadata"
```

---

## Task 6: 下載圖片至檔案系統

**Files:**
- Create: `src/downloadImage.ts`
- Create: `tests/downloadImage.test.ts` (mock fetch)

- [ ] **Step 1: 寫失敗測試 (tests/downloadImage.test.ts)**

```typescript
import { describe, it, expect } from 'vitest';
import { pickResolutionAndUrl } from '../src/downloadImage';

const wallpaper = {
  country: { code: 'us', name: 'United States' },
  slug: 'Test',
  title: 't',
  copyright: 'c',
  detailUrl: 'u',
  downloadUrls: {
    uhd_4k: 'https://example.com/4k.jpg',
    qhd_2k: 'https://example.com/2k.jpg',
    fhd_1080: 'https://example.com/1080.jpg',
  },
};

describe('pickResolutionAndUrl', () => {
  it('prefers 4K when available', () => {
    const picked = pickResolutionAndUrl(wallpaper);
    expect(picked).toEqual({ resolution: 'uhd_4k', url: 'https://example.com/4k.jpg' });
  });

  it('falls back to 2K when 4K missing', () => {
    const w = { ...wallpaper, downloadUrls: { qhd_2k: 'x', fhd_1080: 'y' } };
    expect(pickResolutionAndUrl(w as any)?.resolution).toBe('qhd_2k');
  });

  it('returns null if no download urls', () => {
    const w = { ...wallpaper, downloadUrls: {} };
    expect(pickResolutionAndUrl(w as any)).toBeNull();
  });
});
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `pnpm test downloadImage`
Expected: FAIL — module not found

- [ ] **Step 3: 實作 src/downloadImage.ts**

```typescript
import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { OUTPUT_DIR, RESOLUTION_PRIORITY, USER_AGENT } from './config.js';
import type { Wallpaper, Resolution, DownloadResult } from './types.js';

export function pickResolutionAndUrl(
  w: Wallpaper
): { resolution: Resolution; url: string } | null {
  for (const res of RESOLUTION_PRIORITY) {
    const url = w.downloadUrls[res];
    if (url) return { resolution: res, url };
  }
  return null;
}

function todayISO(): string {
  return new Date().toISOString().slice(0, 10); // YYYY-MM-DD (UTC)
}

export async function downloadImage(w: Wallpaper): Promise<DownloadResult | null> {
  const picked = pickResolutionAndUrl(w);
  if (!picked) return null;

  const dir = join(OUTPUT_DIR, w.country.code);
  await mkdir(dir, { recursive: true });

  const date = todayISO();
  const filename = `${date}-${w.slug}.jpg`;
  const imagePath = join(dir, filename);

  const res = await fetch(picked.url, { headers: { 'User-Agent': USER_AGENT } });
  if (!res.ok) throw new Error(`HTTP ${res.status} for ${picked.url}`);
  const buf = Buffer.from(await res.arrayBuffer());
  await writeFile(imagePath, buf);

  return {
    wallpaper: w,
    resolution: picked.resolution,
    imagePath,
    bytes: buf.length,
    fetchedAt: new Date().toISOString(),
  };
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `pnpm test downloadImage`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/downloadImage.ts tests/downloadImage.test.ts
git commit -m "feat: download wallpaper image with resolution fallback"
```

---

## Task 7: 寫入 metadata JSON

**Files:**
- Create: `src/writeMetadata.ts`

- [ ] **Step 1: 實作 src/writeMetadata.ts**

```typescript
import { writeFile } from 'node:fs/promises';
import { join, parse } from 'node:path';
import type { DownloadResult } from './types.js';

export async function writeMetadata(d: DownloadResult): Promise<string> {
  const { dir, name } = parse(d.imagePath);
  const jsonPath = join(dir, `${name}.json`);
  const payload = {
    country_code: d.wallpaper.country.code,
    country_name: d.wallpaper.country.name,
    date: name.slice(0, 10), // YYYY-MM-DD prefix
    slug: d.wallpaper.slug,
    title: d.wallpaper.title,
    copyright: d.wallpaper.copyright,
    detail_url: d.wallpaper.detailUrl,
    download_urls: d.wallpaper.downloadUrls,
    downloaded_resolution: d.resolution,
    image_path: d.imagePath,
    bytes: d.bytes,
    fetched_at: d.fetchedAt,
  };
  await writeFile(jsonPath, JSON.stringify(payload, null, 2));
  return jsonPath;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/writeMetadata.ts
git commit -m "feat: persist wallpaper metadata as JSON"
```

---

## Task 8: 串接 CLI 進入點

**Files:**
- Create: `src/index.ts`

- [ ] **Step 1: 實作 src/index.ts**

```typescript
import { discoverCountries } from './discoverCountries.js';
import { fetchCountryToday } from './fetchCountryToday.js';
import { fetchAndParseDetail } from './parseDetailPage.js';
import { downloadImage } from './downloadImage.js';
import { writeMetadata } from './writeMetadata.js';
import { PER_COUNTRY_RETRY } from './config.js';
import type { Country } from './types.js';

async function withRetry<T>(fn: () => Promise<T>, times: number): Promise<T> {
  let lastErr: unknown;
  for (let i = 0; i <= times; i++) {
    try {
      return await fn();
    } catch (e) {
      lastErr = e;
      console.warn(`  attempt ${i + 1} failed: ${(e as Error).message}`);
    }
  }
  throw lastErr;
}

async function processCountry(country: Country): Promise<void> {
  const code = country.code;
  console.log(`[${code}] fetching country page`);
  const link = await fetchCountryToday(country);
  if (!link) throw new Error(`no today wallpaper for ${code}`);

  console.log(`[${code}] parsing detail ${link.slug}`);
  const wallpaper = await fetchAndParseDetail(link);

  console.log(`[${code}] downloading image`);
  const result = await downloadImage(wallpaper);
  if (!result) throw new Error(`no downloadable URL for ${code}`);

  const json = await writeMetadata(result);
  console.log(`[${code}] saved ${result.imagePath} (${result.bytes} bytes) + ${json}`);
}

async function main(): Promise<void> {
  const countries = await discoverCountries();
  console.log(`Discovered ${countries.length} countries: ${countries.map((c) => c.code).join(', ')}`);

  const failures: Array<{ code: string; error: string }> = [];
  for (const country of countries) {
    try {
      await withRetry(() => processCountry(country), PER_COUNTRY_RETRY);
    } catch (e) {
      failures.push({ code: country.code, error: (e as Error).message });
      console.error(`[${country.code}] FAILED: ${(e as Error).message}`);
    }
  }

  console.log('\n=== Summary ===');
  console.log(`OK: ${countries.length - failures.length}/${countries.length}`);
  if (failures.length) {
    console.log('Failures:');
    for (const f of failures) console.log(`  ${f.code}: ${f.error}`);
    process.exit(1);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
```

- [ ] **Step 2: 執行整體流程**

Run: `pnpm scrape`
Expected:
- console 列出探索到的 11 個國家
- 每個國家輸出 `[xx] saved wallpapers/xx/2026-XX-XX-<slug>.jpg (NNN bytes) + .json`
- 最末 `OK: 11/11`
- 目錄 `wallpapers/` 下每個國家有兩個檔案

- [ ] **Step 3: 抽查圖片可開啟**

Run: `file wallpapers/us/*.jpg` (Linux/macOS) 或於檔案總管開啟
Expected: 是有效的 JPEG, 解析度 3840 寬

- [ ] **Step 4: Commit**

```bash
git add src/index.ts
git commit -m "feat: wire CLI entry point with per-country retry and summary"
```

---

## Task 9: README 與使用說明

**Files:**
- Create: `README.md`

- [ ] **Step 1: 撰寫 README (繁體中文台灣用語, 簡潔)**

````markdown
# Bing Wallpaper Scraper

抓取 https://bingwallpaper.anerg.com/ 各國今日桌布 (4K).

## 需求
- Node.js 20+
- pnpm (或 npm)

## 安裝
```bash
pnpm install
pnpm exec playwright install chromium
```

## 執行
```bash
pnpm scrape
```

輸出於 `wallpapers/<country_code>/YYYY-MM-DD-<slug>.{jpg,json}`.

## 測試
```bash
pnpm test
```
````

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add README"
```

---

## Task 10: (選用) 加上排程與 dedupe

**Files:**
- Modify: `src/downloadImage.ts`

此 task 為延伸功能, 預設不需做.

- [ ] **Step 1: 在 downloadImage 開頭檢查檔案是否已存在, 存在則跳過**

```typescript
import { access } from 'node:fs/promises';
// ...
try {
  await access(imagePath);
  console.log(`  skip (already downloaded): ${imagePath}`);
  return null;
} catch {
  // not exists, continue
}
```

- [ ] **Step 2: 設置 cron / Task Scheduler 每日執行**

例 (Linux crontab): `0 9 * * * cd /path/to/wallpaper && pnpm scrape >> scrape.log 2>&1`

例 (Windows Task Scheduler): 觸發程式 `pnpm`, 引數 `scrape`, 起始目錄專案根.

---

## AI Agent 執行注意事項

1. **依序執行 Task 1 → 8**, Task 9, 10 為選用.
2. **每個 Task 完成才進入下一個**, 失敗時不要跳過, 先修復.
3. **不可加入 `--no-verify` 跳過 commit hooks**.
4. **如連續 3 次嘗試同一 step 失敗**, 停下來重新評估 (例: 網站結構改變, 需重新抓 fixture).
5. **若網站結構變更導致 selector 失效**:
   - 重新 `curl` 該頁面更新 fixture
   - 調整 regex / selector
   - 補上新測試
6. **CDN imgproxy URL 含 hash 簽章**, 不可手動拼湊, 一律從 detail 頁解析得到.
7. **respect robots.txt 與 rate limiting**, 預設順序執行 (非平行), 每國一個 browser instance, 完成後關閉.
