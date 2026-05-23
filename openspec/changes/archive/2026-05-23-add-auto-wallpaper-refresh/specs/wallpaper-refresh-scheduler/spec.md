## ADDED Requirements

### Requirement: Refresh 入口接受國家代碼陣列

`IWallpaperRefreshScheduler` MUST 提供 `RefreshAsync(IReadOnlyList<string>? countryCodes, CancellationToken ct)` 方法. 當 `countryCodes` 為 null 或空陣列時 MUST fallback 至使用者偏好的 default country (若 default 也未設定則用 `"us"`). 國家代碼 MUST 為 2 字母 lowercase. 無效代碼 (長度不符或非 lowercase) MUST 被略過且 log warning, MUST NOT 拋出 exception.

#### Scenario: 明確傳入多國代碼

- **WHEN** caller 呼叫 `RefreshAsync(new[] { "us", "jp", "fr" }, ct)`
- **THEN** scheduler MUST 對這三國各執行一次 refresh check
- **AND** MUST 在所有國家檢查完成 (成功或失敗) 後才完成 returned Task

#### Scenario: 傳入 null fallback 至 default country

- **WHEN** 使用者偏好 `defaultCountryCode = "jp"` 且 caller 呼叫 `RefreshAsync(null, ct)`
- **THEN** scheduler MUST 只對 jp 執行 refresh check

#### Scenario: 傳入空陣列且偏好未設定 default

- **WHEN** `defaultCountryCode` 為 null 且 caller 呼叫 `RefreshAsync(Array.Empty<string>(), ct)`
- **THEN** scheduler MUST 對 `us` (硬 fallback) 執行 refresh check

#### Scenario: 含無效代碼

- **WHEN** caller 傳入 `new[] { "us", "USA", "j" }`
- **THEN** scheduler MUST 對 `us` 執行 refresh check
- **AND** MUST 略過 `"USA"` 與 `"j"` 並 log warning
- **AND** MUST NOT 拋出 exception

### Requirement: 週期性 tick 行為

排程器在 `Start()` 後 MUST 使用 `PeriodicTimer` 以使用者偏好的 `AutoRefreshIntervalMinutes` 為間隔週期觸發內部 refresh. 第一次 tick MUST 發生於 `Start()` 後一個完整 interval 之後 (而非立即). `Stop()` 或 `Dispose()` MUST 取消 `PeriodicTimer` 與所有 in-flight refresh 工作.

#### Scenario: Start 後等待第一個 interval

- **WHEN** 使用者偏好 `AutoRefreshIntervalMinutes = 60` 且 scheduler 在時間 T 呼叫 `Start()`
- **THEN** 第一次 refresh MUST 約於 T + 60 分鐘觸發, MUST NOT 在 T 立即觸發

#### Scenario: Stop 終止 in-flight refresh

- **WHEN** scheduler 正在執行一輪 refresh 且 caller 呼叫 `Stop()`
- **THEN** in-flight 的 fetcher HTTP 請求 MUST 透過 `CancellationToken` 被取消
- **AND** scheduler `Started` 狀態 MUST 變為 false

#### Scenario: Interval 變更時 timer 重啟

- **WHEN** scheduler 正在執行且使用者於 Settings 將 interval 從 60 改為 30
- **THEN** scheduler MUST 收到偏好變更通知並重啟 timer
- **AND** 下一次 tick MUST 約於變更時間 + 30 分鐘觸發

### Requirement: 單國 refresh 邏輯

對單一國家執行 refresh 時, scheduler MUST 依序: (1) 呼叫 `BingFetcher.GetTodayDetailLinkAsync(country)` 取得當前 slug; (2) 讀取該國 cache 內 metadata 的 slug; (3) 若兩者相同則為 no-op; (4) 若不同 (或 cache 為空) 則呼叫 `FetchAndParseDetailAsync` + 下載圖片 + 透過 `WallpaperCache.SaveAsync` 寫入新檔; (5) 觸發 `WallpaperRefreshed` 事件.

#### Scenario: Slug 相同, no-op

- **WHEN** us cache 內 metadata `slug = "2026-05-23-rocky-mountains"` 且 fetcher 回傳 `slug = "2026-05-23-rocky-mountains"`
- **THEN** scheduler MUST NOT 下載圖片
- **AND** MUST NOT 觸發 `WallpaperRefreshed` 事件
- **AND** cache 檔案 MUST 維持不變

#### Scenario: Slug 不同, 更新 cache

- **WHEN** us cache 內 metadata `slug = "old-slug"` 且 fetcher 回傳 `slug = "new-slug"`
- **THEN** scheduler MUST 下載並寫入新圖片 + metadata
- **AND** MUST 觸發 `WallpaperRefreshed(country: "us", wallpaper: <new>)` 事件
- **AND** 舊 jpg/json MUST 被 `WallpaperCache.SaveAsync` 流程清除 (依既有 cache 規則)

#### Scenario: Cache 為空 (首次 refresh 但 main load 尚未跑到)

- **WHEN** fr cache 資料夾不存在且 scheduler 對 fr 執行 refresh
- **THEN** scheduler MUST 將 fetcher 回傳結果 fully 寫入 cache (視同 cache miss 路徑)

#### Scenario: fetcher 回傳 null detail link

- **WHEN** `GetTodayDetailLinkAsync("de")` 回傳 null (該國今日無桌布)
- **THEN** scheduler MUST NOT 動 cache
- **AND** MUST NOT 觸發事件
- **AND** MUST log warning 但 MUST NOT 拋例外

### Requirement: Refresh 失敗隔離

任一國家 refresh 失敗 (HTTP, timeout, parse exception, cache 寫入失敗) MUST NOT 影響同輪其他國家. 失敗 MUST 被 log, scheduler 排程 MUST 不被中斷, 下一次 tick MUST 照常觸發.

#### Scenario: 多國 refresh, 其中一國 timeout

- **WHEN** scheduler 對 `["us", "jp"]` refresh, jp 在 `FetchAndParseDetailAsync` timeout
- **THEN** us refresh MUST 依其結果完成 (no-op 或更新)
- **AND** jp 失敗 MUST 被 log
- **AND** scheduler `Started` 狀態 MUST 維持為 true
- **AND** 下一次 tick MUST 在預定時間觸發

### Requirement: Refresh 並行上限

當 scheduler 對多國執行 refresh 時, 同時進行**網路 IO 階段** (即 `GetTodayDetailLinkAsync`, `FetchAndParseDetailAsync`, `DownloadImageBytesAsync`) 的國家數 MUST ≤ 2. 並行控制 MUST 以 `SemaphoreSlim` 實作於 scheduler 內部, MUST NOT 共用 `MainViewModel` 的 semaphore.

#### Scenario: 4 國同時 refresh

- **WHEN** scheduler 對 `["us", "jp", "fr", "de"]` 觸發 refresh, 全部 cache miss
- **THEN** 任一時刻同時在網路階段的國家數 MUST ≤ 2
- **AND** 其餘國家 MUST 排隊等候 semaphore release

### Requirement: WallpaperRefreshed 事件

scheduler MUST 暴露 `event EventHandler<WallpaperRefreshedEventArgs> WallpaperRefreshed`. `WallpaperRefreshedEventArgs` MUST 至少含 `CountryCode: string` 與 `Wallpaper: Wallpaper`. 事件 MUST 在 cache 寫入成功後 (但 in-memory wallpaper 可用時) 觸發. 若無 subscriber, scheduler 行為 MUST 不受影響. 事件 invocation MUST 容忍 subscriber 拋 exception (catch + log, 不中斷其他 subscriber).

#### Scenario: 無 subscriber

- **WHEN** scheduler 完成一次成功 refresh 且 `WallpaperRefreshed` 無任何訂閱者
- **THEN** scheduler MUST 正常結束該輪 refresh, MUST NOT 拋出 NullReferenceException

#### Scenario: subscriber 拋例外

- **WHEN** MainViewModel 訂閱 `WallpaperRefreshed`, handler 拋出 InvalidOperationException
- **THEN** scheduler MUST 捕獲該例外並 log warning
- **AND** 後續 refresh 排程 MUST 維持運作

### Requirement: Scheduler 啟停由偏好驅動

App 啟動時 scheduler MUST 讀取 `UserPreferences.AutoRefreshEnabled`: 為 true 則自動 `Start()`, 為 false 則保持 stopped. 使用者於 Settings 切換 enabled MUST 在偏好寫入後立即啟動或停止 scheduler.

#### Scenario: 偏好啟用時自動啟動

- **WHEN** app 啟動且 `preferences.json` 含 `autoRefreshEnabled = true`
- **THEN** scheduler MUST 在 App startup 完成後自動 `Start()`

#### Scenario: 使用者於 Settings 關閉

- **WHEN** scheduler 正在執行且使用者於 Settings 將 `autoRefreshEnabled` 切為 false
- **THEN** 偏好 MUST 立即寫入
- **AND** scheduler MUST `Stop()` 終止 timer 與 in-flight refresh

#### Scenario: 使用者於 Settings 啟用

- **WHEN** scheduler 為 stopped 且使用者於 Settings 將 `autoRefreshEnabled` 切為 true
- **THEN** scheduler MUST `Start()` 開始週期 tick

### Requirement: 最小化到 system tray 不影響 scheduler

當主視窗最小化或關閉到 system tray, scheduler MUST 繼續執行週期 tick. 僅當使用者明確「Exit」app 結束 process 時 scheduler MUST `Stop()` 並釋放 timer.

#### Scenario: 最小化到 tray

- **WHEN** 主視窗從 visible 切到 `Hide()` (tray 模式)
- **THEN** scheduler `Started` 狀態 MUST 維持為 true
- **AND** 下一次預定 tick MUST 照常觸發

#### Scenario: 使用者選 Exit

- **WHEN** 使用者透過 tray menu 點擊 Exit, app 進入 `OnExit` 流程
- **THEN** scheduler `Dispose()` MUST 被呼叫
- **AND** in-flight refresh MUST 被 cancel
