## ADDED Requirements

### Requirement: 探索國家清單

系統 SHALL 從 `https://bingwallpaper.anerg.com/` 首頁的 HTML 中解析出所有支援的國家代碼與顯示名稱. 系統 MUST 忽略非國家路徑 (e.g. `/archive`, `/detail`, `/about`).

#### Scenario: 成功解析國家清單

- **WHEN** 系統呼叫 `BingFetcher.DiscoverCountriesAsync()` 且首頁回傳 200
- **THEN** 回傳值 MUST 為 `IReadOnlyList<Country>` 且包含至少 11 個項目 (au, ca, cn, de, es, fr, it, jp, nz, uk, us)
- **AND** 每個 `Country` MUST 有非空的 `Code` (2 字母 lowercase) 與 `Name`

#### Scenario: 首頁 HTTP 失敗

- **WHEN** 首頁回傳非 2xx 或網路異常
- **THEN** 系統 MUST 拋出包含 status code 或 inner exception 的 `FetcherException`

### Requirement: 取得單一國家今日 detail 連結

對於指定的國家代碼, 系統 SHALL 從 `/{code}` 頁面擷取第一個 `/detail/{code}/{slug}` 連結, 該連結 MUST 視為今日桌布的 detail 頁.

#### Scenario: 國家頁含今日桌布

- **WHEN** 系統呼叫 `BingFetcher.GetTodayDetailLinkAsync(country)` 且該國有今日桌布
- **THEN** 回傳值 MUST 為非 null 的 `DetailLink`, 其 `DetailUrl` MUST 符合 `https://bingwallpaper.anerg.com/detail/{code}/{slug}` 格式
- **AND** `Slug` MUST 為非空字串

#### Scenario: 國家頁無 detail 連結

- **WHEN** 國家頁 HTML 中找不到任何 `/detail/{code}/` 連結
- **THEN** 回傳值 MUST 為 null (而非拋例外), 讓上層可決定如何處理

### Requirement: 解析 detail 頁的下載 URL 與 metadata

系統 SHALL 從 detail 頁的 HTML 中擷取至多三種解析度的下載 URL (4K, 2K, 1080p) 與 metadata (title, copyright). 系統 MUST NOT 手動拼湊 imgproxy URL (因含 hash 簽章).

#### Scenario: 解析完整的 detail 頁

- **WHEN** 系統呼叫 `BingFetcher.FetchAndParseDetailAsync(link)` 且 detail 頁含全部三種解析度
- **THEN** 回傳的 `Wallpaper.DownloadUrls` MUST 同時包含 `uhd_4k` (含 `w:3840`), `qhd_2k` (含 `w:2560`), `fhd_1080` (含 `w:1920`)
- **AND** `Title` MUST 為非空字串
- **AND** `Copyright` MUST 含 `©` 符號 (若 alt 文字格式正常)

#### Scenario: detail 頁僅有部分解析度

- **WHEN** detail 頁缺少 4K URL 但有 2K
- **THEN** `DownloadUrls.uhd_4k` MUST 為 null, `DownloadUrls.qhd_2k` MUST 為非 null
- **AND** 上層 caller MUST 能依此 fallback 至 2K

### Requirement: 解析度優先順序

當下載桌布時系統 SHALL 依序嘗試 4K → 2K → 1080p, 選擇第一個非 null 的 URL.

#### Scenario: 4K 可用

- **WHEN** Wallpaper 物件三種解析度皆有
- **THEN** 系統 MUST 選擇 `uhd_4k` 並回報 `DownloadedResolution = "uhd_4k"`

#### Scenario: 僅有 1080p

- **WHEN** Wallpaper 物件僅 `fhd_1080` 非 null
- **THEN** 系統 MUST 選擇 `fhd_1080` 並回報 `DownloadedResolution = "fhd_1080"`

### Requirement: HTTP 請求設定

所有 HTTP 請求 MUST 設定 `User-Agent` 為 `Mozilla/5.0 (compatible; WallpaperApp/0.1)` 且 timeout 不超過 30 秒.

#### Scenario: 預設 timeout

- **WHEN** 任一 HTTP 請求超過 30 秒未回應
- **THEN** 系統 MUST 取消該請求並拋出可被 UI 捕捉的 timeout exception
