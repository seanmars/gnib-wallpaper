## Context

WallpaperApp 目前載入流程: app 啟動 → `MainViewModel.InitializeAsync` 對所有探索到的國家 fan-out 背景載入 (concurrency cap 4) → 每國分別經 `WallpaperCache.TryLoadTodayAsync` (cache hit) 或 `BingFetcher` (cache miss) 取得當日桌布. 載入完成後沒有任何自我刷新機制. 使用者跨日後若 app 仍開著, 即便 Bing 已換新桌布, 本地 cache 仍會被 `TryLoadTodayAsync` 判定為「今日資料」 (因為 `fetched_date == today UTC`), 不會主動重抓.

但實際上「換日跨界」與「特定國家當日桌布何時上線」不見得對齊使用者所在時區的午夜. 例如 us 桌布可能在 UTC 早上才上線, 此時 cache 內可能還是昨日資料 (`fetched_date` 也是昨日), 等使用者重啟才會抓到. 此外, 即便已是今日 UTC, anerg 站偶爾會在當天稍晚才更新某些國家的圖片, 需要週期性 re-check.

新的 scheduler 將週期性 re-check 指定國家: 比對 `GetTodayDetailLinkAsync` 回傳的 slug 與 cache metadata 的 slug. 若不同則重抓並更新 cache. 若相同則為 no-op.

## Goals / Non-Goals

**Goals:**
- 提供 `IWallpaperRefreshScheduler` 服務, 可接受國家代碼陣列, 預設為使用者 default country.
- App lifecycle 期間 (含 minimized to tray) 持續週期性檢查, 間隔可設.
- 命中新桌布時 silent 寫入 cache, MainViewModel 訂閱事件後可選擇性更新顯示.
- 與既有 fetcher / cache 流程整合, 不破壞 stateless `BingFetcher` 設計.
- 使用者可從 Settings 啟停與調整間隔.

**Non-Goals:**
- 不做桌面 toast / balloon 通知 (silent only).
- 不做自動換桌布 (使用者仍需手動觸發 `WallpaperSetterService`).
- 不做跨 process / app 關閉後仍執行的 background task (使用者完全關閉 app 後排程器隨之終止).
- 不重新設計既有 `BingFetcher` 或 `WallpaperCache`.
- 不引入新 NuGet 依賴.

## Decisions

### 1. 使用 `PeriodicTimer` 而非 `Timer` 或 `Task.Delay` loop

**選擇**: `System.Threading.PeriodicTimer` (.NET 6+).
**理由**:
- 與 async/await 自然整合 (`WaitForNextTickAsync`), 可在 tick handler 內 `await` 並讓 cancellation token 中止.
- 不會像 `System.Threading.Timer` 那樣在前一次 tick 還沒結束時就觸發下一次 (避免 reentrancy 問題).
- 不像手寫 `Task.Delay` loop 需要處理 drift 與 cancellation.

**替代方案**:
- `System.Timers.Timer`: 觸發在 thread pool, 處理 reentrancy 需自行加鎖, 不自然支援 async.
- `Task.Delay` while-loop: 會 drift 而且 cancel 行為要手寫.

### 2. Refresh check 以 slug 比對, 而非 date

**選擇**: 用 `BingFetcher.GetTodayDetailLinkAsync` 取得當前 slug, 與 cache metadata 的 `slug` 欄位比對, 不同才重抓.
**理由**:
- Bing 偶爾在一天內更新同國家的桌布 (or 各國時區差異), 單靠 `fetched_date == today` 無法察覺.
- Slug 是 anerg 站 URL 一部分, 內容變更幾乎一定換 slug.
- 比對 slug 只需 fetch 一個 HTML page (短文字), 比直接 fetch detail + 圖片便宜很多.

**替代方案**:
- 用 ETag / If-Modified-Since: anerg 站沒提供穩定 ETag, 不可靠.
- 直接 fetch detail page + 比對 download URL: 比 slug 多一次 HTTP request, 浪費頻寬.

### 3. Scheduler 由 caller 端 (App.xaml.cs / MainViewModel) 注入並啟動, scheduler 本身無 DI container 依賴

**選擇**: scheduler 接受 `BingFetcher`, `WallpaperCache`, `IUserPreferencesService` 作為 constructor 參數. `App.xaml.cs` 負責建立 + 啟動 + 在 `OnExit` 停止.
**理由**:
- 既有專案沒有 DI container (App.xaml.cs 手動 wire-up). 維持一致風格.
- Scheduler 只需要少量依賴, 不需要 service locator.

### 4. Concurrency 與既有 `MainViewModel` semaphore 隔離

**選擇**: scheduler 內部對 refresh 的「網路 IO 階段」自帶 `SemaphoreSlim(maxConcurrency: 2)`. 不共用 `MainViewModel` 的 semaphore.
**理由**:
- Scheduler 通常只 refresh 一個國家 (default country), 但介面允許多國; 隔離可避免初始載入與 refresh 互相 contention.
- 上限 2 (而非 4) 是因為 refresh 是低優先, 不該與 user-initiated 載入競爭.

**替代方案**:
- 重用 `MainViewModel.LoadSemaphore`: 違反單一職責, 且 scheduler 不該依賴 ViewModel.

### 5. UI 更新透過事件 (`event EventHandler<WallpaperRefreshedEventArgs>`) 通知 MainViewModel

**選擇**: scheduler 暴露 `WallpaperRefreshed` 事件 (包含 country code 與更新後的 `Wallpaper` 物件). MainViewModel 訂閱, 若該國正是目前顯示, 切到該國的 `CountryItem` 內部 wallpaper.
**理由**:
- 避免 scheduler 反向依賴 ViewModel.
- 即便事件無人訂閱, cache 仍正常寫入 (scheduler 不會被綁死於 UI).

### 6. 偏好欄位範圍與驗證

**選擇**:
- `AutoRefreshEnabled: bool` 預設 `true`.
- `AutoRefreshIntervalMinutes: int` 預設 `60`, 合法範圍 `[5, 1440]`. 反序列化遇到超界值 clamp 而非拋例外.

**理由**:
- 下限 5 分鐘避免使用者誤設過短頻繁 hit anerg 站.
- 上限 1440 (一天) 覆蓋實際需求.
- Clamp-on-read 避免舊偏好檔被手動編輯後損壞 app.

### 7. Interval 變更時的 timer 重啟策略

**選擇**: 偏好變更 (`AutoRefreshIntervalMinutes` 改值) 時, scheduler `Restart()`: 取消舊 `PeriodicTimer`, 以新間隔啟新 timer. 不等舊 tick 跑完.
**理由**:
- `PeriodicTimer.Period` 在 .NET 8+ 可動態調整, 但專案 target 為 .NET 9; 簡單起見直接重建 timer.
- 使用者調整 interval 通常希望立即生效.

## Risks / Trade-offs

- **Risk**: 過短 interval (e.g., 5 分鐘) 多國同時開啟可能對 anerg 站造成連續流量. → **Mitigation**: scheduler 內部 concurrency cap = 2; 預設 interval 60 分鐘; UI 下限 5 分鐘.
- **Risk**: `PeriodicTimer` tick 期間若使用者連續多次改 interval, 可能造成 race (新舊 timer 同時跑一段). → **Mitigation**: `Restart()` 內部用 lock 序列化 timer 切換, 舊 timer cancellation token 一旦 cancel 不再 enqueue new tick.
- **Risk**: cache 寫入失敗導致下次 refresh 又重抓相同 slug. → **Mitigation**: 與既有 cache 失敗策略一致 (log warning, in-memory 仍 valid). 重抓成本可接受, 不會 crash.
- **Trade-off**: 不通知使用者 "新桌布來了" → 使用者可能不知道桌布已更新. 為了符合「silent」需求, 接受此 trade-off; 之後可疊加 toast 通知作為新 capability.
- **Trade-off**: 排程器在 system tray 仍跑 → 微量 CPU/network 使用. 預設 60 分鐘間隔, 影響可忽略.

## Migration Plan

- 既有使用者 `preferences.json` 缺新欄位 → 反序列化用預設值 (`enabled = true, interval = 60`) → 首次啟動即會開始 refresh, 不需手動遷移.
- 沒有需要刪除或重命名的舊欄位.
- Rollback: 將 scheduler 從 `App.xaml.cs` startup 拿掉, 移除新 source files, preferences 多出來的兩個欄位無害可保留.

## Open Questions

- 是否需要在 UI 上提示「上次成功 refresh 時間」? 目前 design 不做, 若使用者要求再加 (屬於後續 capability).
- 跨日時是否要強制 refresh 一輪 (即便還沒到 tick)? 目前 design 不做, 因為 tick 間隔上限 1440 分鐘 = 一天, 至多延遲一個 tick. 若體感不佳可考慮疊加「midnight UTC 強制 tick」 feature.
