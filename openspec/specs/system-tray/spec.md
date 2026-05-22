# system-tray Specification

## Purpose
TBD - created by archiving change add-system-tray-minimize. Update Purpose after archive.
## Requirements
### Requirement: Tray icon 常駐

應用程式啟動完成後 MUST 在 Windows 通知區域 (system tray) 顯示一個 tray icon, 並 MUST 保持顯示直到使用者明確 Exit. Tray icon MUST 在主視窗隱藏或顯示時皆持續存在.

#### Scenario: 應用程式啟動

- **WHEN** 使用者首次執行應用程式 (process 啟動且 `App.OnStartup` 完成)
- **THEN** Windows 通知區域 MUST 出現 app 的 tray icon
- **AND** 滑鼠 hover tray icon MUST 顯示 tooltip 含應用程式名稱 (e.g. "GNIB Wallpaper")

#### Scenario: 主視窗隱藏

- **WHEN** 主視窗被 minimize 或關閉至 tray (走 `HandleCloseRequest` 之 `MinimizeToTray` 分支)
- **THEN** Tray icon MUST 持續可見

### Requirement: Tray icon 上下文選單

對 tray icon 按下右鍵 MUST 顯示上下文選單, 內容 MUST 至少包含以下項目, 順序由上至下:

1. `Show / Hide` — toggle 主視窗顯示狀態.
2. `Reset close preference` — 清除已記住的關閉偏好 (使下次點 X 時重新彈出對話框).
3. 分隔線.
4. `Exit` — 結束應用程式 (`Application.Current.Shutdown()`).

#### Scenario: 右鍵 tray icon

- **WHEN** 使用者對 tray icon 按下滑鼠右鍵
- **THEN** Windows 上下文選單 MUST 出現, 含 `Show / Hide`, `Reset close preference`, 分隔線, `Exit` 四項
- **AND** 點擊任何項目以外的區域 MUST 收起選單而不執行任何動作

#### Scenario: 點選 Exit

- **WHEN** 使用者點選選單中的 `Exit`
- **THEN** Tray icon MUST 消失
- **AND** Process MUST 在 < 2 秒內結束 (`Application.Current.Shutdown()` 完成)

#### Scenario: 點選 Reset close preference

- **WHEN** 使用者先前曾勾選「Remember my choice」並選擇 `Minimize to tray` 或 `Exit`
- **AND** 使用者點選 tray 選單的 `Reset close preference`
- **THEN** 偏好檔 (`preferences.json`) 中的 `closeAction` MUST 被清除 (設為 null 或欄位移除)
- **AND** 下一次使用者點擊主視窗 X 按鈕 MUST 重新彈出對話框

### Requirement: 點擊 tray icon 還原主視窗

使用者點擊 (single click 或 double click) tray icon 時, 主視窗 MUST 從隱藏狀態還原並取得 foreground focus. 若主視窗已顯示, MUST 將其帶到最前.

#### Scenario: 主視窗隱藏中, 使用者 double click tray icon

- **WHEN** 主視窗目前 `Visibility = Collapsed` (已 hide-to-tray)
- **AND** 使用者 double click tray icon
- **THEN** 主視窗 MUST 變為可見 (`Visibility = Visible`, `WindowState = Normal`)
- **AND** 主視窗 MUST 被帶到所有視窗最前 (foreground) 並取得 focus

#### Scenario: 主視窗已顯示, 使用者 double click tray icon

- **WHEN** 主視窗已顯示但被其他視窗覆蓋
- **AND** 使用者 double click tray icon
- **THEN** 主視窗 MUST 被帶到最前並取得 focus
- **AND** 主視窗 `WindowState` MUST NOT 改變 (e.g. 若原本 Maximized 保持 Maximized)

### Requirement: 主視窗 minimize 隱藏至 tray

當使用者透過主視窗右上角 minimize 按鈕 (或 `WindowState = Minimized`) 最小化視窗時, 主視窗 MUST 被隱藏至 tray, 工作列 (taskbar) MUST NOT 保留該視窗的 entry. 此行為 MUST NOT 觸發關閉確認對話框.

#### Scenario: 使用者點擊 minimize 按鈕

- **WHEN** 使用者點擊主視窗右上角 minimize 按鈕
- **THEN** 主視窗 MUST 變為 `Visibility = Collapsed` (從 taskbar 消失)
- **AND** Tray icon MUST 仍可見
- **AND** MUST NOT 出現關閉確認對話框

### Requirement: 關閉確認對話框

使用者點擊主視窗右上角 close 按鈕 (X) 時, 若偏好檔尚未記錄選擇, 系統 MUST 彈出對話框. 對話框 MUST 包含: 簡短說明文字, 「Minimize to tray」按鈕, 「Exit application」按鈕, 與「Remember my choice」checkbox (預設未勾選). 對話框 MUST 為 modal (相對於主視窗).

#### Scenario: 偏好為空時點 X

- **WHEN** 偏好檔不存在或 `closeAction = null`
- **AND** 使用者點擊主視窗 X 按鈕
- **THEN** Modal 對話框 MUST 出現, 含上述四個元件
- **AND** 主視窗 MUST NOT 被關閉或隱藏直到使用者選擇

#### Scenario: 選擇 Minimize to tray

- **WHEN** 使用者於對話框點擊「Minimize to tray」
- **THEN** 對話框 MUST 關閉
- **AND** 主視窗 MUST 變為隱藏 (`Visibility = Collapsed`, 從 taskbar 消失)
- **AND** Tray icon MUST 仍可見
- **AND** Process MUST NOT 結束

#### Scenario: 選擇 Exit application

- **WHEN** 使用者於對話框點擊「Exit application」
- **THEN** 對話框 MUST 關閉
- **AND** Tray icon MUST 消失
- **AND** Process MUST 結束 (`Application.Current.Shutdown()`)

#### Scenario: 勾選 Remember my choice

- **WHEN** 使用者勾選「Remember my choice」並選擇某動作 (Minimize to tray 或 Exit)
- **THEN** 偏好檔 MUST 寫入 `closeAction` 對應值 ("MinimizeToTray" 或 "Exit")
- **AND** 下一次使用者點擊 X 按鈕 MUST 直接執行該動作而 MUST NOT 再彈對話框

#### Scenario: 已記住偏好為 MinimizeToTray

- **WHEN** 偏好檔 `closeAction = "MinimizeToTray"`
- **AND** 使用者點擊主視窗 X 按鈕
- **THEN** 主視窗 MUST 直接隱藏至 tray, MUST NOT 彈對話框

#### Scenario: 已記住偏好為 Exit

- **WHEN** 偏好檔 `closeAction = "Exit"`
- **AND** 使用者點擊主視窗 X 按鈕
- **THEN** 應用程式 MUST 直接結束, MUST NOT 彈對話框

#### Scenario: 對話框關閉 (按 ESC 或對話框 X)

- **WHEN** 使用者按 ESC 或點擊對話框右上角 X 取消對話框
- **THEN** 對話框 MUST 關閉
- **AND** 主視窗 MUST 保持原狀 (不關閉, 不隱藏)
- **AND** 偏好檔 MUST NOT 被修改

### Requirement: 偏好持久化

關閉偏好 MUST 儲存於 `%LOCALAPPDATA%\\GnibWallpaper\\preferences.json` (或本專案 cache layer 採用的同等 user-local base path), JSON 結構 MUST 至少含 `closeAction` 欄位, 值為 `"MinimizeToTray"` | `"Exit"` | `null`. 寫入 MUST atomic (write-temp-then-rename 或同等), 讀取錯誤 MUST 視為偏好不存在.

#### Scenario: 首次寫入偏好

- **WHEN** 應用程式首次寫入 `closeAction = "MinimizeToTray"`
- **THEN** `preferences.json` MUST 出現於 `%LOCALAPPDATA%\\GnibWallpaper\\` 下
- **AND** 檔案內容 MUST 為有效 JSON 含 `"closeAction": "MinimizeToTray"`

#### Scenario: 偏好檔損毀

- **WHEN** `preferences.json` 存在但內容非有效 JSON
- **THEN** 應用程式 MUST 視為偏好不存在 (closeAction = null)
- **AND** MUST NOT 因此 crash
- **AND** 下一次寫入 MUST 覆蓋損毀內容

### Requirement: 應用程式 lifecycle

`Application.ShutdownMode` MUST 設為 `OnExplicitShutdown`. 應用程式 MUST 只在 tray Exit, 對話框 Exit, 或系統強制結束時才結束 process. 隱藏主視窗 MUST NOT 觸發 process 結束.

#### Scenario: 主視窗隱藏

- **WHEN** 主視窗從顯示狀態進入隱藏狀態 (任何來源)
- **THEN** Process MUST 持續執行
- **AND** Tray icon MUST 仍存在

