# wallpaper-fetcher Specification

## Purpose
TBD - created by archiving change add-bing-wallpaper-viewer. Update Purpose after archive.
## Requirements
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

### Requirement: 背景並行載入 concurrency cap

當 caller (e.g. `MainViewModel`) 對多個國家同時觸發載入時, 系統 MUST 限制同時進行**網路 IO 階段** (含 `GetTodayDetailLinkAsync`, `FetchAndParseDetailAsync`, `DownloadImageBytesAsync`) 的國家數上限. 預設上限 MUST 為 4. 上限機制 MUST 以 `SemaphoreSlim` (或等效非 busy-wait 機制) 實作於呼叫端 (`MainViewModel`), MUST NOT 在 `BingFetcher` 內部全域 throttle (`BingFetcher` 保留為 stateless service).

#### Scenario: 11 國同時 fan-out

- **WHEN** `MainViewModel.InitializeAsync` 對 11 國 fan-out 背景載入且所有國家皆 cache miss
- **THEN** 任一時刻同時進行網路階段的國家數 MUST ≤ 4
- **AND** 其餘國家 MUST 在 `SemaphoreSlim.WaitAsync` 上排隊
- **AND** 任一國家完成或失敗時 MUST `Release()` semaphore 讓下一國進入

#### Scenario: Cache 命中不佔 concurrency cap

- **WHEN** us 於背景載入時先檢查 `WallpaperCache.TryLoadTodayAsync` 並命中
- **THEN** us MUST NOT 取得 semaphore (cache 命中路徑 MUST 在 `WaitAsync` 之前)
- **AND** 其他 4 國 MUST 可繼續同時佔用 semaphore 不被 cache hit 阻塞

### Requirement: 單國背景載入失敗隔離

任一國家在背景載入中拋出 exception 或 timeout MUST NOT 影響其他國家. 失敗國家 MUST 釋放其 semaphore slot, 將自身 state 標為 Error (含可讀錯誤訊息), 並從 in-flight 追蹤結構中移除. 其他國家的 in-flight 任務 MUST 持續執行.

#### Scenario: 一國 timeout, 其他國家不受影響

- **WHEN** fr 在 `DownloadImageBytesAsync` 階段 timeout 拋 `TaskCanceledException`
- **THEN** fr LoadState MUST = Error, ErrorMessage MUST 含可讀說明
- **AND** semaphore 計數 MUST 恢復至允許其他國家進入
- **AND** us, jp, de 等其他 in-flight 任務 MUST 不被 cancel

#### Scenario: 多國同時失敗

- **WHEN** 網路斷線導致 us, jp, de 同時失敗
- **THEN** 三國 MUST 各自 Error, 各自 Release semaphore
- **AND** 後續排隊國家 MUST 仍能進入網路階段並各自獨立決定 success / fail

### Requirement: 同國重複觸發載入的去重

當同一國家已有 in-flight 載入任務時, 對該國再次發起 `EnsureLoadStartedAsync` (或等效 entry point) MUST NOT 啟動第二個並行任務. 例外: 該國 LoadState = Error 且使用者明確重試時 MUST 允許重啟. Loaded 狀態的國家不必重新載入直到下一次跨日.

#### Scenario: Loading 中重複觸發

- **WHEN** us LoadState = Loading 且 caller 再次呼叫 `EnsureLoadStartedAsync(us)`
- **THEN** 系統 MUST NOT 啟動第二個 us 載入任務
- **AND** 既有 in-flight us 任務 MUST 不受影響

#### Scenario: Error 後重試

- **WHEN** fr LoadState = Error 且使用者點擊 fr 國旗觸發重試
- **THEN** 系統 MUST 將 fr LoadState 重置為 Loading
- **AND** MUST 啟動新的 fr 載入任務 (取得 semaphore 後執行)

