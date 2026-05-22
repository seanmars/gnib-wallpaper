# wallpaper-viewer-ui Specification

## Purpose
TBD - created by archiving change add-bing-wallpaper-viewer. Update Purpose after archive.
## Requirements
### Requirement: 兩列版面結構

MainWindow MUST 採用上下三列版面: 第 1 列為 top navbar, 第 2 列為國旗 row, 第 3 列為桌布圖片區. 三列 MUST 由上至下垂直堆疊, MUST NOT 互相疊加. navbar 與國旗 row MUST 位於圖片區之**外圍** (上方).

#### Scenario: 視窗一般大小

- **WHEN** 視窗大小為預設 (e.g. 1280x800)
- **THEN** top navbar MUST 出現在視窗最頂端, 高度自適 (約 32-48 px)
- **AND** 國旗 row MUST 緊接 navbar 下方, 高度自適 (約 48-64 px)
- **AND** 圖片區 MUST 佔據國旗 row 之下的全部剩餘空間
- **AND** 三列之間 MUST 無重疊

#### Scenario: 視窗縮小

- **WHEN** 使用者將視窗縮至最小 (e.g. 600x400)
- **THEN** top navbar 與國旗 row 仍 MUST 完整可見且不壓圖
- **AND** 圖片區依比例縮小

### Requirement: 國旗 row 顯示所有國家

國旗 row MUST 以單一橫向列 (single horizontal row) 顯示所有從 fetcher 探索到的國家. 每個國家 MUST 以該國國旗圖示表示 (emoji 或圖片). 顯示順序 MUST 為從 fetcher 取得的順序.

#### Scenario: 11 國全顯示

- **WHEN** fetcher 回傳 11 個國家
- **THEN** 國旗 row MUST 渲染 11 個國旗按鈕, 全部可見且可點擊
- **AND** 按鈕排列 MUST 為單一橫向列 (非換行多列)

#### Scenario: 視窗過窄

- **WHEN** 視窗寬度不足以容納所有國旗
- **THEN** 國旗 row MUST 加上水平捲軸, 而非折行

### Requirement: 點擊國旗切換國家

點擊任一國旗 MUST 觸發載入該國今日桌布. 系統 MUST 在 cache 命中時即時切換 (無 loading), cache miss 時顯示載入動畫並於完成後切換.

#### Scenario: 點擊已快取國家

- **WHEN** 使用者點擊 jp 國旗且 jp cache 為今日
- **THEN** 圖片區 MUST 在 < 200 ms 內切換為日本桌布
- **AND** loading 動畫 MUST NOT 出現

#### Scenario: 點擊未快取國家

- **WHEN** 使用者點擊 fr 國旗且 fr cache 為空
- **THEN** 圖片區 MUST 立即顯示 loading 動畫
- **AND** 載入完成後 MUST 切換為法國桌布, loading 動畫 MUST 消失

#### Scenario: 切換中重複點擊

- **WHEN** us 正在抓取中, 使用者連續快速點擊 jp 後 de
- **THEN** 系統 MUST 取消 jp 抓取轉而抓 de (或反之依時序), 不可同時保留多個未取消的請求
- **AND** 最終顯示的結果 MUST 對應最後點擊的國家

### Requirement: 預設選中美國

應用程式啟動時 MUST 依下列優先序決定要載入的國家: (1) 使用者偏好 `defaultCountryCode` 對應且仍存在於 fetcher 回傳清單的國家; (2) `us` (若 `us` 在清單中); (3) 清單第一個項目. 對應國家 MUST 自動載入今日桌布, 對應國旗按鈕 MUST 視覺上呈現 "已選中" 狀態. 使用者不需做任何操作即可看到 (或正在載入) 該國今日桌布.

#### Scenario: 全新安裝啟動

- **WHEN** 應用程式啟動完成且 `preferences.json` 不存在 (或 `defaultCountryCode` 為 null)
- **THEN** 系統 MUST 自動觸發 us 桌布載入
- **AND** us 國旗按鈕 MUST 視覺上呈現 "已選中" 狀態

#### Scenario: 使用者偏好為 jp 且 jp 在清單

- **WHEN** 應用程式啟動完成且 `defaultCountryCode = "jp"`
- **AND** fetcher 回傳的清單包含 jp
- **THEN** 系統 MUST 自動觸發 jp 桌布載入 (而非 us)
- **AND** jp 國旗按鈕 MUST 視覺上呈現 "已選中" 狀態

#### Scenario: 使用者偏好為 xx (不在 fetcher 清單)

- **WHEN** 應用程式啟動完成且 `defaultCountryCode = "xx"`
- **AND** fetcher 回傳的清單不含 xx
- **THEN** 系統 MUST fallback 至 us, 自動觸發 us 桌布載入
- **AND** MUST NOT 將 `preferences.json` 的 `defaultCountryCode` 自動清空

### Requirement: Loading 動畫

當系統正在抓取或下載圖片時, MUST 在圖片區中央顯示載入動畫 (ProgressBar 或 ProgressRing). 動畫 MUST 持續顯示直到圖片載入完成或失敗.

#### Scenario: 首次啟動

- **WHEN** 應用程式啟動且 us cache 為空
- **THEN** 主視窗顯示後 MUST 在圖片區立即顯示 loading 動畫
- **AND** 動畫 MUST 在 us 桌布載入完成時消失

#### Scenario: 切換到未快取國家

- **WHEN** 使用者點擊未快取國家
- **THEN** 圖片區 MUST 立刻 (在發起 HTTP 請求前) 顯示 loading 動畫

### Requirement: 錯誤狀態顯示

當抓取或下載失敗時, 系統 MUST 在圖片區顯示可讀的錯誤訊息 (e.g. "Failed to load wallpaper for {country}: {reason}"). 使用者 MUST 能透過再次點擊國旗重試.

#### Scenario: 網路失敗

- **WHEN** 抓取 jp 時網路 timeout
- **THEN** 圖片區 MUST 顯示錯誤訊息含國家名稱與失敗原因
- **AND** loading 動畫 MUST 消失
- **AND** 使用者點擊任一國旗 MUST 可重新嘗試

### Requirement: 圖片填滿並維持比例

桌布圖片 MUST 以 `Uniform` stretch 模式顯示, 確保完整圖片可見且維持原比例 (可能上下/左右留邊).

#### Scenario: 4K 桌布顯示於 1280x720 視窗

- **WHEN** 4K 桌布 (16:9) 顯示於 1280x720 視窗
- **THEN** 圖片 MUST 等比縮放至填滿視窗
- **AND** MUST NOT 被裁切

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

