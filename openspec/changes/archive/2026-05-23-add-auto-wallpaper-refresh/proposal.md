## Why

目前使用者必須手動切換國旗或重啟 app 才能取得當日最新桌布. Bing 在每天當地時間更新桌布, 若使用者整天都開著 app, 過了當日換日點之後不會自動察覺到有新桌布. 我們希望以背景輪詢方式週期性檢查特定國家是否有新的當日桌布, 命中時自動更新 cache, 讓使用者下一次切到該國時看到的就是最新內容.

## What Changes

- 新增 `WallpaperRefreshScheduler` 服務, 啟動後以 `PeriodicTimer` 每隔 N 分鐘 tick 一次, 觸發一輪 refresh check.
- 新增 `IWallpaperRefreshScheduler.RefreshAsync(IReadOnlyList<string>? countryCodes = null, CancellationToken ct = default)` 入口: 接受國家代碼陣列, 對每個國家呼叫 `BingFetcher.GetTodayDetailLinkAsync` → 若 slug 與目前 cache 不同則重抓 detail + 下載圖片 + 寫入 cache.
- 當 `countryCodes` 為 null 或空陣列時, MUST fallback 至使用者偏好的 default country (若 default 也未設定, 則用 fallback `us`).
- 排程器 lifecycle: app 啟動且偏好 `autoRefreshEnabled = true` 時啟動 timer, 偏好變為 false 或 app 關閉時停止. 最小化到 system tray 時 MUST 繼續執行.
- 命中新桌布時 MUST silent refresh cache (不彈通知, 不自動換桌面). 若使用者目前正在檢視該國, MainViewModel SHALL 訂閱事件並更新顯示.
- Settings 視窗新增「Auto-refresh」區段: 啟用 toggle (預設 ON), 間隔分鐘數輸入 (預設 60, 範圍 5-1440). 變更立即寫入 `preferences.json`.
- `UserPreferences` 模型新增 `AutoRefreshEnabled: bool` 與 `AutoRefreshIntervalMinutes: int` 兩個欄位, 舊 `preferences.json` 缺欄位時 MUST 退回預設值.

## Capabilities

### New Capabilities
- `wallpaper-refresh-scheduler`: 週期性檢查指定國家是否有新桌布, 命中時 silent 更新 cache 與 in-memory state.

### Modified Capabilities
- `app-settings`: Settings 視窗新增 auto-refresh 區段; `UserPreferences` 新增兩個欄位.

## Impact

- 新增 source files: `Services/IWallpaperRefreshScheduler.cs`, `Services/WallpaperRefreshScheduler.cs`, 對應的 unit test (若 test project 存在).
- 修改: `Models/UserPreferences.cs` (新欄位), `Services/UserPreferencesService.cs` (序列化), `ViewModels/SettingsViewModel.cs` (UI binding), `Views/SettingsWindow.xaml` (新區段), `ViewModels/MainViewModel.cs` (訂閱 refresh 事件), `App.xaml.cs` (DI registration + scheduler lifecycle).
- 不影響: 既有 `BingFetcher` (stateless), `WallpaperCache` (重用既有 `SaveAsync` 與 cache miss 流程), `WallpaperSetterService`, system tray.
- 無新增 NuGet dependency. 使用 .NET 內建 `PeriodicTimer`.
