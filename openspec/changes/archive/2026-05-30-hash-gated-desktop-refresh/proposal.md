## Why

目前自動 refresh 計時器到時只更新使用者偏好的單一 default country (`RefreshAsync(null)` fallback 至一國), 其他國家的 cache 會逐漸過期; 而且計時器路徑從不主動套用桌布, 桌布只能靠手動按鈕更新. 我們希望計時器能把所有國家的 cache 一次更新, 並在 default country 的圖片真的有變化時自動把桌布換成新圖, 沒變化時則完全不動桌布以避免不必要的閃爍與寫入.

## What Changes

- 週期性 tick 從「只 refresh 一國」改為「refresh 所有 discovered countries」(slug gate 維持, 只有真的換圖的國家才下載).
- 新增桌布自動套用: 每個 tick 在 refresh 完成後, 對 default country 當前 cache 圖片計算 hash, 與上次套用桌布的 hash 比較.
  - hash 不同 (或從未套用過) -> 透過 `IWallpaperSetterService` 套用該圖為桌布, 並記錄新 hash.
  - hash 相同 -> 完全不動桌布.
- 新增持久化「上次套用桌布的 hash」狀態 (單一值, 非 per-country), 使切換 default country 時也能正確重新套用桌布.
- `WallpaperRefreshScheduler` 注入 `IWallpaperSetterService` 以在背景套用桌布.

## Capabilities

### New Capabilities
<!-- 無新 capability; 全部行為屬於既有 scheduler. -->

### Modified Capabilities
- `wallpaper-refresh-scheduler`: 週期 tick 改為更新全部國家; 新增 default country 圖片 hash 比對與桌布自動套用 (hash 變化才套用), 以及上次套用 hash 的持久化狀態.

## Impact

- 程式碼: `WallpaperRefreshScheduler` (tick 解析所有國家 + 桌布 reconcile 邏輯), 新增 hash 計算與套用狀態持久化 (新 state 檔, 例如 `HomeDir.GetPath("desktop-state.json")`); 注入 `IWallpaperSetterService`.
- 既有行為保留: slug gate (cache 效率)、refresh 失敗隔離、並行上限、`WallpaperRefreshed` 事件、啟停由偏好驅動.
- 無 breaking change; 桌布自動套用為新增行為, 手動 `SetAsDesktop` 不受影響.
- 依賴: 沿用既有 `IWallpaperSetterService` 與 `WallpaperCache`; 新增 .NET 內建 `SHA256` 計算 image hash.
