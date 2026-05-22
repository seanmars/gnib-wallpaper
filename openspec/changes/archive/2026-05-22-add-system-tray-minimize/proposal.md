## Why

桌布應用程式為長時間在背景執行的工具型 app, 使用者通常希望它能常駐而非每次按下 close button 就完全結束. 目前點擊右上角 X 會直接關閉 process, 無法快速回到視窗也無法持續接收後續桌布更新. 加入 Windows system tray 可常駐並讓使用者在關閉時自行決定行為.

## What Changes

- 新增 Windows system tray icon, 應用程式啟動後常駐於通知區域.
- Tray icon MUST 提供右鍵選單: 「Show / Hide」「Exit」.
- 點擊 (single click 或 double click) tray icon MUST 還原主視窗到前景.
- 點擊主視窗右上角 close button (X) 時 MUST 彈出對話框詢問使用者: 「Minimize to tray」或「Exit application」, 並提供「Remember my choice」勾選保存至 settings.
- 視窗從工作列最小化 (minimize button) 時 MUST 隱藏至 tray (taskbar 不再保留 entry), 與點擊 X 選擇 minimize 行為一致.
- 應用程式關閉行為 MUST 由 tray「Exit」或對話框中「Exit」明確觸發, 才會結束 process.

## Capabilities

### New Capabilities
- `system-tray`: Windows system tray icon, tray menu, 視窗 minimize/restore 至 tray, 以及關閉時的使用者選擇對話框與偏好記憶.

### Modified Capabilities
- `wallpaper-viewer-ui`: MainWindow 的 close 與 minimize 行為改變, 由立即關閉改為交由 system-tray capability 處理.

## Impact

- `WallpaperApp/WallpaperApp.csproj`: 加入 system tray 依賴 (建議 `Hardcodet.NotifyIcon.Wpf` NuGet package 以維持 WPF/XAML 與 MVVM 風格一致).
- `WallpaperApp/App.xaml` 與 `App.xaml.cs`: tray icon 生命週期管理 (建立於 startup, 釋放於 exit), 並改 `ShutdownMode` 至 `OnExplicitShutdown` 以避免主視窗隱藏時自動結束 process.
- `WallpaperApp/MainWindow.xaml.cs` 與 `MainWindow.xaml`: 攔截 `Closing` event, 顯示確認對話框, 並處理 minimize-to-tray.
- 新增 ViewModel / Service: 例如 `TrayIconViewModel` 與 `IUserPreferences` (儲存「Remember my choice」), 偏好持久化可採用 application settings 或 JSON 檔.
- 新增資源: tray icon `.ico` 檔案.
- 不影響 wallpaper fetcher / cache 邏輯.
