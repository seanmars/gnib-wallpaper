## 1. 套件與資源準備

- [x] 1.1 在 `WallpaperApp.csproj` 加入 `Hardcodet.NotifyIcon.Wpf` (latest stable) NuGet 引用, 確認可於 net10.0-windows 還原與 build.
- [x] 1.2 在 `WallpaperApp/Assets/` 新增 `tray.ico` (含 16x16 與 32x32, 可暫用既有 app icon 縮圖), 設定 `<Resource>` build action.
- [x] 1.3 在 `WallpaperApp.csproj` 加入或確認 `Assets/tray.ico` 被打包.

## 2. 偏好持久化 service

- [x] 2.1 新增 `WallpaperApp/Models/UserPreferences.cs` (record / class), 含 `CloseAction?` 屬性 (enum: `MinimizeToTray`, `Exit`).
- [x] 2.2 新增 `WallpaperApp/Services/IUserPreferencesService.cs`: `Task<UserPreferences> LoadAsync()`, `Task SaveAsync(UserPreferences)`, `Task ResetCloseActionAsync()`.
- [x] 2.3 實作 `WallpaperApp/Services/UserPreferencesService.cs`, 使用 `%LOCALAPPDATA%\WallpaperApp\preferences.json` (與既有 cache layer 同 base path), 寫入採 write-temp-then-replace, 讀取 JSON 解析失敗時回傳 default. (註: 路徑由設計文件「`GnibWallpaper`」改為對齊既有 cache 的 `WallpaperApp`)
- [x] 2.4 改採手動驗證清單方式 (見任務 8.1-8.4), 不引入單元測試專案. UserPreferencesService 已涵蓋 IOException / JsonException / UnauthorizedAccessException 三種錯誤路徑.

## 3. Application lifecycle 調整

- [x] 3.1 修改 `WallpaperApp/App.xaml`: 移除 `StartupUri` 並設 `ShutdownMode="OnExplicitShutdown"`. (TaskbarIcon 改在 code-behind 建立, 不需引用 `xmlns:tb`)
- [x] 3.2 修改 `WallpaperApp/App.xaml.cs`: 在 `OnStartup` 建立 service composition (preferences service, window-close service, tray controller); 在 `OnExit` 釋放 tray icon.
- [x] 3.3 確認 `MainWindow` 仍會在 startup 顯示 (App.OnStartup 主動 `new MainWindow()` + `Show()`).

## 4. CloseConfirmDialog

- [x] 4.1 新增 `WallpaperApp/Views/CloseConfirmDialog.xaml` + `.xaml.cs`: modal `Window`, 含說明文字, `Minimize to tray` 與 `Exit application` 按鈕, `Remember my choice` checkbox; 按 ESC 或 X 視為 Cancel.
- [x] 4.2 新增 `WallpaperApp/ViewModels/CloseConfirmViewModel.cs`: 對外 expose `CloseAction? Result` 與 `bool Remember`.
- [x] 4.3 確認對話框 modal owner 為主視窗, `WindowStartupLocation="CenterOwner"` + `SizeToContent="Height"` + `ResizeMode="NoResize"`.

## 5. Window close service

- [x] 5.1 新增 `WallpaperApp/Services/IWindowCloseService.cs`: `Task HandleCloseRequestAsync(CloseRequestReason reason)` + 四個輔助方法; enum `CloseRequestReason { UserCloseButton, Minimize }`.
- [x] 5.2 實作 `WallpaperApp/Services/WindowCloseService.cs`: UserCloseButton 走 preferences → dialog 路徑; Minimize 直接 hide. 加入 `_handlingCloseRequest` re-entry guard.
- [x] 5.3 暴露 `MinimizeToTray()` / `RestoreWindow()` / `ToggleWindow()` / `ExitApplication()`.

## 6. Tray icon controller

- [x] 6.1 在 `App.xaml.cs` 動態建立 `TaskbarIcon`, `IconSource` 透過 `pack://application:,,,/Assets/tray.ico`, `ToolTipText = "GNIB Wallpaper"`.
- [x] 6.2 配置上下文選單: `Show / Hide` (toggle), `Reset close preference`, 分隔線, `Exit`.
- [x] 6.3 同時綁定 `DoubleClickCommand` 與 `LeftClickCommand` → `RestoreWindowCommand` (對應 spec「single click 或 double click」).
- [x] 6.4 新增 `WallpaperApp/ViewModels/TrayIconViewModel.cs` 統一持有 tray commands.

## 7. MainWindow 攔截

- [x] 7.1 修改 `WallpaperApp/MainWindow.xaml.cs`: 攔截 `Closing` event, 透過 `App.CurrentApp.WindowClose` service locator, 設 `e.Cancel = true`, 呼叫 `HandleCloseRequestAsync(UserCloseButton)`.
- [x] 7.2 攔截 `StateChanged`: 當 `WindowState == Minimized` → 設回 `Normal`, 呼叫 `HandleCloseRequestAsync(Minimize)`.
- [x] 7.3 透過 tray Exit 結束時, `App.IsExiting` 旗標跳過 `e.Cancel`, 允許 Shutdown 流程關閉視窗.

## 8. 驗證與 polish

- [x] 8.1 手動驗證 spec 中關鍵 scenario: 點 X → dialog 出現 (centered, modal); 選 Minimize to tray 成功; 選 Exit application 結束 process. 透過 WM_CLOSE 注入 + 截圖確認 dialog 位置 (Owner-centered, L=1087/R=1507 對應主視窗中心 1297). User 手動確認 minimize to tray 與 exit application 兩條路徑運作正常.
- [x] 8.2 Minimize → hide → 還原已由 user 手動確認運作.
- [ ] 8.3 Remember my choice 持久化 + Reset close preference 路徑未經實際使用者操作驗證, 留待後續手動測試.
- [ ] 8.4 偏好檔損毀情境未經手動測試; 程式碼路徑已覆蓋 (IOException / JsonException / UnauthorizedAccessException catch in `UserPreferencesService.LoadAsync`).
- [ ] 8.5 (Optional polish) 首次 hide-to-tray 顯示 balloon tip 「App is running in tray」. (留待後續變更, 屬 polish)
- [x] 8.6 更新 `WallpaperApp/README.md`: 加 system tray 與關閉行為說明.
