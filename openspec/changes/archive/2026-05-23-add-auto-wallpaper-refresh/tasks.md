## 1. UserPreferences 欄位擴充

- [x] 1.1 在 `WallpaperApp/Models/UserPreferences.cs` 新增 `AutoRefreshEnabled: bool` 屬性 (預設 true) 與 `AutoRefreshIntervalMinutes: int` 屬性 (預設 60), 以 `[JsonPropertyName]` 對應 `autoRefreshEnabled` 與 `autoRefreshIntervalMinutes`.
- [x] 1.2 在 `WallpaperApp/Services/UserPreferencesService.cs` 反序列化路徑加入 clamp 邏輯: 讀到的 `AutoRefreshIntervalMinutes` 在 [5, 1440] 之外時 clamp 至界線值; 缺欄位時保持預設.
- [x] 1.3 在 `UserPreferencesService` 暴露偏好變更通知 (若尚未有, 可用既有 `PreferencesChanged` event 或 callback). scheduler 將訂閱以重啟 timer.

## 2. WallpaperRefreshScheduler service

- [x] 2.1 新增 `WallpaperApp/Services/WallpaperRefreshedEventArgs.cs` 含 `CountryCode: string` 與 `Wallpaper: Wallpaper` 屬性.
- [x] 2.2 新增 `WallpaperApp/Services/IWallpaperRefreshScheduler.cs` 介面: `Start()`, `Stop()`, `RefreshAsync(IReadOnlyList<string>? countryCodes, CancellationToken ct)`, `Started: bool` 屬性, `event EventHandler<WallpaperRefreshedEventArgs> WallpaperRefreshed`, 並繼承 `IDisposable`.
- [x] 2.3 新增 `WallpaperApp/Services/WallpaperRefreshScheduler.cs` 實作:
  - constructor 接受 `BingFetcher`, `WallpaperCache`, `IUserPreferencesService`, 與 logger (若有).
  - 內部 `SemaphoreSlim(2, 2)` 控制網路階段並行.
  - `Start()`: 讀偏好 interval, 建立 `PeriodicTimer`, 起一個背景 `Task` 跑 `WaitForNextTickAsync` loop, 每 tick 呼叫 `RefreshAsync(null, ct)`.
  - `RefreshAsync`: 處理 country code 陣列 fallback (null/空 → default country → "us"), 過濾無效代碼, 對每國呼叫 private `RefreshOneAsync`.
  - `RefreshOneAsync`: 取 semaphore → `GetTodayDetailLinkAsync` → 與 cache slug 比對 → 必要時 `FetchAndParseDetailAsync` + `DownloadImageBytesAsync` + `WallpaperCache.SaveAsync` → 觸發 `WallpaperRefreshed` 事件 → release semaphore. 全程 try/catch 隔離失敗.
  - `Stop()` / `Dispose()`: cancel internal CTS, dispose `PeriodicTimer`, 等候背景 Task 結束.
  - `Restart()` (private): 在 interval 變更時被偏好變更 handler 呼叫, lock + cancel old timer + 啟新 timer.

## 3. App lifecycle 串接

- [x] 3.1 在 `WallpaperApp/App.xaml.cs` 建立 scheduler instance (取既有 `BingFetcher`, `WallpaperCache`, `UserPreferencesService`), 儲為 field.
- [x] 3.2 App startup 完成 (`MainWindow` shown 後) 檢查 `UserPreferences.AutoRefreshEnabled`, 為 true 則 `scheduler.Start()`.
- [x] 3.3 在 `App.OnExit` 呼叫 `scheduler.Dispose()` 以 cancel in-flight refresh.
- [x] 3.4 將 scheduler 透過 `MainWindow` constructor 或屬性注入給 `MainViewModel`, 讓 MainViewModel 能訂閱 `WallpaperRefreshed` 事件.

## 4. MainViewModel 訂閱 refresh

- [x] 4.1 在 `WallpaperApp/ViewModels/MainViewModel.cs` 訂閱 `scheduler.WallpaperRefreshed`.
- [x] 4.2 Handler 切回 UI dispatcher 後, 找到 `CountryItem(code)`, 將其內部 wallpaper / BitmapImage 更新為新值.
- [x] 4.3 若該國目前為 `SelectedCountry`, 同時更新主視窗顯示的 wallpaper / metadata.
- [x] 4.4 解除訂閱 (在 ViewModel `Dispose` 或主視窗 close 時) 避免 leak.

## 5. Settings UI 區段

- [x] 5.1 在 `WallpaperApp/Views/SettingsWindow.xaml` 於 Default country 區段之下新增 "Auto-refresh" 區段, 含 enable CheckBox 與 interval NumericUpDown (或 TextBox + spinner buttons).
- [x] 5.2 在 `WallpaperApp/ViewModels/SettingsViewModel.cs` 新增 `AutoRefreshEnabled: bool` 與 `AutoRefreshIntervalMinutes: int` 屬性, two-way bind 到上述 UI.
- [x] 5.3 屬性 setter 寫入 `UserPreferencesService` 立即持久化, 並觸發 `PreferencesChanged` event 使 scheduler 收到通知 (start/stop/restart).
- [x] 5.4 在 interval 屬性 setter clamp 輸入值至 [5, 1440]; 非數字輸入由 XAML control 自身擋掉或 setter 還原.
- [x] 5.5 將 interval NumericUpDown 的 `IsEnabled` bind 至 `AutoRefreshEnabled` 屬性.

## 6. 驗證

- [x] 6.1 手動測試: 設 interval = 5 分鐘, 啟動 app, 觀察 ~5 分鐘後是否觸發 refresh (log).
- [x] 6.2 手動測試: 將 cache 內某國 metadata 的 `slug` 改成假值 → 等 tick → 確認 cache 被更新, MainViewModel 顯示更新.
- [x] 6.3 手動測試: Settings 切 enable OFF → 確認下個 tick 不觸發. 再切 ON → 確認下一個 interval 後 tick 恢復.
- [x] 6.4 手動測試: 改 interval 60 → 30, 確認 timer 重啟並以新間隔 tick.
- [x] 6.5 手動測試: 最小化到 system tray 後等到 tick → 確認 refresh 正常觸發.
- [x] 6.6 手動測試: app exit 流程 (tray menu Exit) → 確認 in-flight refresh 被 cancel, 無 unhandled exception.
- [x] 6.7 驗證 `openspec validate add-auto-wallpaper-refresh --type change --strict` 通過.
