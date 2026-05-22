## ADDED Requirements

### Requirement: MainWindow 關閉與最小化委派至 system-tray

MainWindow MUST NOT 直接處理關閉或最小化的最終行為 (隱藏 / 結束 process). 該邏輯 MUST 委派給 [`system-tray`](../system-tray/spec.md) capability. MainWindow 的責任 MUST 限於攔截 `Closing` event 與 `StateChanged` event 並轉交 service 處理.

#### Scenario: 使用者點擊 close 按鈕 (X)

- **WHEN** 使用者點擊主視窗右上角 X 按鈕
- **THEN** MainWindow MUST 在 `OnClosing` 中設 `e.Cancel = true`
- **AND** MUST 呼叫 system-tray capability 提供的 `HandleCloseRequest` (reason = UserCloseButton)
- **AND** 後續關閉 / 隱藏行為 MUST 完全由 system-tray capability 決定

#### Scenario: 使用者點擊 minimize 按鈕

- **WHEN** 使用者點擊主視窗右上角 minimize 按鈕 (`WindowState` 變為 `Minimized`)
- **THEN** MainWindow MUST 攔截 `StateChanged` 並呼叫 system-tray capability 的 `HandleCloseRequest` (reason = Minimize)
- **AND** MUST 將 `WindowState` 設回 `Normal` 以避免 taskbar 出現最小化縮圖
- **AND** 後續 hide-to-tray 行為 MUST 由 system-tray capability 處理

#### Scenario: 應用程式經由 tray Exit 結束

- **WHEN** 使用者透過 tray 上下文選單選擇 Exit
- **THEN** MainWindow 的 `Closing` event MUST 允許關閉 (`e.Cancel = false`)
- **AND** MUST 在關閉前完成必要 cleanup (e.g. unsubscribe ViewModel events)

### Requirement: MainWindow 隱藏期間 ViewModel 持續運作

當 MainWindow 被隱藏至 tray 時, `MainViewModel` 與其使用的 `BingFetcher` / `WallpaperCache` / `FlagCache` 等 service MUST 持續運作, MUST NOT 被釋放. 使用者從 tray 還原視窗時 MUST 看到隱藏前的 state (e.g. 已選擇的國家, 已載入的桌布).

#### Scenario: 隱藏後還原, 狀態保留

- **WHEN** 使用者已選擇 jp 國旗並看到 jp 桌布
- **AND** 使用者透過 X 按鈕並選擇 Minimize to tray 隱藏主視窗
- **AND** 5 秒後從 tray double click 還原視窗
- **THEN** 主視窗 MUST 直接顯示先前的 jp 桌布
- **AND** MUST NOT 重新載入或進入 loading 狀態 (除非當下需重抓今日新桌布)
- **AND** jp 國旗按鈕 MUST 仍呈現「已選中」狀態
