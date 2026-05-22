## Context

GNIB Wallpaper 是一個 WPF (.NET 10, Windows) 桌面應用程式, 使用 CommunityToolkit.Mvvm 進行 MVVM. 目前 `MainWindow` 啟動後直接顯示視窗, 按下右上角 close 按鈕 (X) 會結束整個 process. 對於每日抓取桌布的工具型 app, 使用者通常希望讓它持續在背景常駐, 並能快速從 system tray 還原.

本次設計目標是加入 Windows system tray 與「關閉時詢問」流程, 並讓 minimize 行為一致地隱藏至 tray. 沒有現存的 tray 相關程式碼, 需引入第三方套件或改用 WindowsForms 互通.

## Goals / Non-Goals

**Goals:**

- 應用程式啟動後在通知區域顯示 tray icon, 程式整個生命週期都常駐 (直到使用者明確 Exit).
- 攔截主視窗 `Closing` event, 提供「Minimize to tray / Exit」對話框, 並可選擇「Remember my choice」記住偏好.
- Minimize 按鈕也走相同的 hide-to-tray 流程, 工作列 (taskbar) entry 在隱藏期間消失.
- 點擊 tray icon (single 或 double click) 還原主視窗到前景, 並從 tray 上下文選單提供 Show / Hide / Exit.
- 偏好設定 (remembered choice) 持久化到 user 層級的設定檔, 應用程式重啟後仍生效.

**Non-Goals:**

- 不做跨平台 tray (macOS, Linux). 本 app 為 net10.0-windows.
- 不做 tray icon 動態圖示變化 (e.g. 抓取中閃爍).
- 不重新設計 wallpaper fetcher / cache. 本變更只影響視窗生命週期.
- 不加入「啟動時隱藏到 tray」「開機自動啟動」等延伸功能, 留待未來變更.

## Decisions

### Decision 1: 採用 `Hardcodet.NotifyIcon.Wpf` 套件實作 tray

選擇原因:
- WPF / XAML 原生風格, 可在 XAML 宣告 `<tb:TaskbarIcon>` 並 binding ViewModel command, 與專案現有 MVVM 風格一致.
- 維護良好, NuGet 下載量高, 支援 .NET 10.
- 內建 balloon tip, 上下文選單, single/double click event.

替代方案:
- `System.Windows.Forms.NotifyIcon`: 需在 csproj 加 `UseWindowsForms` true, 需自行寫 code-behind 管理. 風格與 WPF binding 不一致, MVVM 整合較繁瑣.
- 自行 P/Invoke Shell_NotifyIcon: 完全自寫, 維護成本高, 無顯著好處.

選 `Hardcodet.NotifyIcon.Wpf` 一致性最高且實作成本最低.

### Decision 2: `ShutdownMode` 改為 `OnExplicitShutdown`

WPF 預設 `ShutdownMode = OnLastWindowClose`. 一旦主視窗 Hide (`Visibility = Collapsed` 或 `Hide()`), 雖不會立即結束, 但若日後加入次要視窗則行為易出錯. 改為 `OnExplicitShutdown` 後, 整個 app 的 lifecycle 由我們在 `App.xaml.cs` 與 tray「Exit」明確控制, 行為可預期.

替代方案: 維持 `OnLastWindowClose` 並用 `Window.Hide()` 而非 close, 也可行, 但若未來新增 SettingsWindow 等次要視窗, 容易因關閉次要視窗導致整個 app 結束. `OnExplicitShutdown` 更安全.

### Decision 3: 對話框使用自製 WPF 對話框, 非 `MessageBox.Show`

「Remember my choice」需要 checkbox, 標準 `MessageBox.Show` 無法支援. 採用自製 `CloseConfirmDialog : Window`, 兩個按鈕 (Minimize to tray / Exit) 加 checkbox. ViewModel-first 設計, 結果以 enum 回傳 (`CloseAction.MinimizeToTray | Exit | Cancel`).

### Decision 4: 偏好儲存採用 JSON 檔案, 不引入 `System.Configuration`

儲存位置: `%LOCALAPPDATA%\\GnibWallpaper\\preferences.json`, 與 wallpaper cache 同一個 base dir 慣例 (`WallpaperCache` 目前以 `LocalApplicationData` 為基底, 可重用該 path helper).

格式:
```json
{
  "closeAction": "MinimizeToTray" | "Exit" | null
}
```

`null` 或不存在 → 每次彈出對話框. 已選擇 → 直接執行對應動作.

替代方案: `Application Settings (settings.settings)` 較重且難測試, JSON 簡單可控且與 cache layer 一致.

### Decision 5: Close 與 Minimize 走統一路徑

`MainWindow.OnClosing` 與 `MainWindow.OnStateChanged` (當 `WindowState = Minimized`) 都走 `IWindowCloseService.HandleCloseRequestAsync(reason)`:

- `reason = UserCloseButton` → 若有 remembered choice 直接執行, 否則彈出對話框.
- `reason = Minimize` → 一律 hide-to-tray (不彈對話框, 因為 minimize 動作本身已表達意圖).

統一 service 介面易於測試與 mock.

### Decision 6: Tray icon 資源

新增 `WallpaperApp/Assets/tray.ico` (多解析度 ico, 至少 16x16 與 32x32). 由 `App.xaml` 中的 `<tb:TaskbarIcon IconSource="/Assets/tray.ico" />` 載入. 暫時可重用既有 app icon 縮圖, 後續再優化.

## Risks / Trade-offs

- **Risk: Hardcodet.NotifyIcon.Wpf 與 net10.0 相容性**
  → Mitigation: 安裝後第一個 task 驗證 build + run; 若不相容退回 `System.Windows.Forms.NotifyIcon` (Decision 1 的替代方案), 影響範圍只在 tray 載入層, ViewModel 不變.

- **Risk: 使用者勾選「Remember my choice → Exit」後再也回不到對話框, 將以為 tray 壞掉**
  → Mitigation: tray 選單提供「Reset close preference」項, 或在 README 中說明. Tasks 列出.

- **Risk: `ShutdownMode = OnExplicitShutdown` 若忘記在 tray Exit handler 呼叫 `Application.Current.Shutdown()`, 程式無法結束**
  → Mitigation: tray Exit command 統一走 `IApplicationLifecycle.ExitAsync()`, 並在 `OnExit` log/釋放資源. 加 integration smoke test.

- **Risk: Hide 到 tray 時 wallpaper fetcher 仍在跑 background task, 若主視窗為其資料 host 可能洩漏**
  → Mitigation: 既有 fetcher / cache 為 service, 不綁定 window lifecycle, hide 不影響. 此風險目前低.

- **Trade-off: 不支援 startup-to-tray (常見功能但 non-goal)**
  → 接受. 未來變更可加 `--minimized` CLI flag 或 settings 選項.

## Migration Plan

無資料遷移. Rollout 為單一 release, 第一次啟動偏好檔不存在則行為等同預設 (每次彈框). Rollback 為移除 `Hardcodet.NotifyIcon.Wpf` 引用並還原 `MainWindow.xaml.cs` 攔截, 偏好 JSON 留存無害.

## Open Questions

- Tray icon 是否需要支援 dark / light theme 自動切換? 預設不做, 待設計確認.
- 「Remember my choice」是否提供 reset 入口在 tray 選單? 預設提供 (Decision 5 risk mitigation), tasks 中確認.
- Minimize 走 hide-to-tray 是否要顯示 balloon tip「App is running in tray」首次提示? 建議僅第一次出現, 但屬於 polish, tasks 中標 optional.
