## ADDED Requirements

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
