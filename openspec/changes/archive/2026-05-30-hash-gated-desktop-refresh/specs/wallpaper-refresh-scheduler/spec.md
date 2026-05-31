## MODIFIED Requirements

### Requirement: 週期性 tick 行為

排程器在 `Start()` 後 MUST 使用 `PeriodicTimer` 以使用者偏好的 `AutoRefreshIntervalMinutes` 為間隔週期觸發內部 refresh. 第一次 tick MUST 發生於 `Start()` 後一個完整 interval 之後 (而非立即). 每個 tick MUST 對**所有** `DiscoverCountriesAsync` 回傳的國家各執行一次 refresh check (而非只對 default country); tick MUST 在所有國家檢查完成 (成功或失敗) 後, 再執行一次「桌布 reconcile」(見「桌布自動套用 gated by image hash」需求). `Stop()` 或 `Dispose()` MUST 取消 `PeriodicTimer` 與所有 in-flight refresh 工作.

#### Scenario: Start 後等待第一個 interval

- **WHEN** 使用者偏好 `AutoRefreshIntervalMinutes = 60` 且 scheduler 在時間 T 呼叫 `Start()`
- **THEN** 第一次 refresh MUST 約於 T + 60 分鐘觸發, MUST NOT 在 T 立即觸發

#### Scenario: 每個 tick 更新所有國家

- **WHEN** `DiscoverCountriesAsync` 回傳 11 國且 scheduler 觸發一次 tick
- **THEN** scheduler MUST 對這 11 國各執行一次 refresh check
- **AND** slug gate MUST 維持 (slug 未變的國家 MUST NOT 下載)
- **AND** 並行上限 MUST 維持 (同時網路階段國家數 ≤ 2)

#### Scenario: tick 結束後執行桌布 reconcile

- **WHEN** scheduler 完成一輪所有國家的 refresh check
- **THEN** scheduler MUST 接著對 default country 執行一次桌布 reconcile (不論該國本輪 slug 是否變化)

#### Scenario: Stop 終止 in-flight refresh

- **WHEN** scheduler 正在執行一輪 refresh 且 caller 呼叫 `Stop()`
- **THEN** in-flight 的 fetcher HTTP 請求 MUST 透過 `CancellationToken` 被取消
- **AND** scheduler `Started` 狀態 MUST 變為 false

#### Scenario: Interval 變更時 timer 重啟

- **WHEN** scheduler 正在執行且使用者於 Settings 將 interval 從 60 改為 30
- **THEN** scheduler MUST 收到偏好變更通知並重啟 timer
- **AND** 下一次 tick MUST 約於變更時間 + 30 分鐘觸發

## ADDED Requirements

### Requirement: 桌布自動套用 gated by image hash

每個 tick 在所有國家 refresh 完成後, scheduler MUST 對 `UserPreferences.DefaultCountryCode` 所指國家 (若未設定則用硬 fallback `"us"`) 執行桌布 reconcile: (1) 讀取該國當前 cache 的圖片 bytes; (2) 以 `SHA256` 計算 image hash (lowercase hex string); (3) 與持久化的「上次套用桌布的 hash」比較; (4) 若不同 (或從未套用過) 則透過 `IWallpaperSetterService.SetWallpaperFromFile` 套用該圖為桌布, 套用成功後 MUST 寫入新 hash; (5) 若相同則 MUST NOT 套用桌布. 桌布 reconcile MUST 獨立於 slug gate (即使該國本輪 slug 未變、未下載新圖, 仍 MUST 執行比對).

#### Scenario: default country 圖片 hash 改變, 套用桌布

- **WHEN** default country 為 `jp`, 其 cache 當前圖片 hash 與 last-applied hash 不同
- **THEN** scheduler MUST 呼叫 `SetWallpaperFromFile` 套用 jp 圖為桌布
- **AND** MUST 將 last-applied hash 更新為 jp 圖片的新 hash

#### Scenario: default country 圖片 hash 未變, 不動桌布

- **WHEN** default country 為 `jp`, 其 cache 當前圖片 hash 與 last-applied hash 相同
- **THEN** scheduler MUST NOT 呼叫 `SetWallpaperFromFile`
- **AND** last-applied hash MUST 維持不變

#### Scenario: 首次套用 (無 last-applied hash)

- **WHEN** last-applied hash 不存在 (首次執行或 state 遺失) 且 default country 有 cache 圖片
- **THEN** scheduler MUST 套用該圖為桌布並寫入 hash

#### Scenario: 切換 default country 後重新套用

- **WHEN** 上次套用的是 `us` 圖 (last-applied hash = us 圖 hash), 使用者將 default country 改為 `jp`, 下一個 tick 後 jp 已有 cache 圖片
- **THEN** scheduler MUST 偵測到 jp 圖 hash != last-applied hash 並套用 jp 圖為桌布
- **AND** MUST 將 last-applied hash 更新為 jp 圖 hash

#### Scenario: default country 當前無 cache 圖片

- **WHEN** default country 的 cache 資料夾不存在或無今日圖片
- **THEN** 桌布 reconcile MUST 為 no-op (不動桌布、不寫 hash、不拋例外)

#### Scenario: 套用桌布失敗

- **WHEN** `SetWallpaperFromFile` 拋出 `WallpaperSetterException`
- **THEN** scheduler MUST catch 並 log warning, MUST NOT 中斷排程
- **AND** last-applied hash MUST NOT 被更新 (下一個 tick MUST 重試套用)

### Requirement: 上次套用桌布 hash 的持久化

scheduler MUST 將「上次套用桌布的 image hash」持久化為**單一值** (非 per-country), 存於獨立 state 檔 (例如 `HomeDir.GetPath("desktop-state.json")`), MUST NOT 存入 `UserPreferences`. 讀取 state 失敗 (檔不存在、損毀、JSON 解析失敗) MUST 視為「無 last-applied hash」, MUST NOT 拋例外. 寫入 hash MUST 在 `SetWallpaperFromFile` 成功之後才進行.

#### Scenario: hash 持久化跨 app 重啟

- **WHEN** scheduler 套用桌布並寫入 hash, app 重啟後再次 tick 且 default country 圖片未變
- **THEN** scheduler MUST 從 state 檔讀回相同 hash 並判定 no-op, MUST NOT 重複套用桌布

#### Scenario: state 檔損毀

- **WHEN** `desktop-state.json` 內容損毀無法解析
- **THEN** scheduler MUST 視為無 last-applied hash, MUST NOT 拋例外
- **AND** 下一次桌布 reconcile MUST 重新套用一次桌布並寫入新 hash

#### Scenario: hash 不存入 preferences

- **WHEN** scheduler 寫入 last-applied hash
- **THEN** `UserPreferences` / `preferences.json` MUST NOT 因此被修改
