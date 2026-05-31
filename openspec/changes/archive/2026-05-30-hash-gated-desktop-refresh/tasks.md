## 1. 桌布套用狀態持久化

- [x] 1.1 新增 `DesktopState` model (含 `appliedImageHash: string?`, 選用 `appliedCountryCode: string?`), 對應 `desktop-state.json`
- [x] 1.2 新增讀寫 helper (例如 `DesktopStateStore`): `LoadAsync` 失敗 (檔不存在/損毀/JSON 解析失敗) 回傳空狀態不拋例外; `SaveAsync` 以 atomic write (temp + replace) 寫入, 路徑用 `HomeDir.GetPath("desktop-state.json")`
- [x] 1.3 確認 hash 不存入 `UserPreferences`/`preferences.json`

## 2. Image hash 計算

- [x] 2.1 新增 hash helper: 以 `SHA256` 對 image bytes 計算, 輸出 lowercase hex string
- [x] 2.2 提供從 cache 圖片檔讀 bytes 並算 hash 的路徑 (重用 `WallpaperCache.TryLoadTodayAsync` 或直接讀 metadata.ImagePath)

## 3. Scheduler: 每個 tick 更新所有國家

- [x] 3.1 `WallpaperRefreshScheduler` 建構子注入 `IWallpaperSetterService` 與 desktop state store; 更新 DI 註冊 (App.xaml.cs)
- [x] 3.2 修改 tick (`RunLoopAsync`): tick 開頭 `DiscoverCountriesAsync` 一次, 取得全部國家代碼, 對所有國家 refresh (取代 `RefreshAsync(null)` 的單國路徑)
- [x] 3.3 (效率) 將 discover 取得的 `Country` 串給各國 refresh, 避免 `RefreshOneAsync`/`ResolveCountryAsync` per-country 重複 discover
- [x] 3.4 確認 slug gate、並行上限 (`SemaphoreSlim` ≤ 2)、失敗隔離、`WallpaperRefreshed` 事件行為皆維持不變

## 4. Scheduler: 桌布 reconcile (hash gate)

- [x] 4.1 在 tick 所有國家 refresh 完成後, 新增 `ReconcileDesktopAsync`: 解析 default country (未設定用 `"us"`)
- [x] 4.2 讀取 default country 當前 cache 圖片; 無圖片則 no-op (不動桌布、不寫 hash)
- [x] 4.3 計算圖片 hash, 與 state 內 last-applied hash 比較; 相同則 no-op
- [x] 4.4 hash 不同 (或無 last-applied): 呼叫 `SetWallpaperFromFile` 套用; 成功後才寫入新 hash 到 state
- [x] 4.5 `SetWallpaperFromFile` 拋 `WallpaperSetterException` 時 catch + log warning, 不中斷排程, 不更新 hash (下次 tick 重試)
- [x] 4.6 確認 reconcile 獨立於 slug gate (即使 default country 本輪未換圖也會執行比對)

## 5. 驗證

- [x] 5.1 `dotnet build` 通過, 無新增警告
- [x] 5.2 手動/單元驗證: hash 變化才套用桌布; 相同不套用; 切換 default country 後正確重新套用
- [x] 5.3 驗證 state 檔損毀時不拋例外且能自我修復 (重新套用一次)
- [x] 5.4 驗證背景 tick 不阻塞 UI, 套用桌布失敗不中斷排程
