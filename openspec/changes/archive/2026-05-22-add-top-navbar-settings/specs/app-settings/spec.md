## ADDED Requirements

### Requirement: Top navbar 結構與位置

MainWindow MUST 在最上方 (DockPanel.Dock="Top", 位於國旗 row 之上) 放置一個 navbar Border. navbar MUST 為單列, 高度自適 (約 32-48 px), MUST 橫跨整個視窗寬度. navbar MUST NOT 疊加在國旗 row 或圖片區之上.

#### Scenario: 預設視窗大小

- **WHEN** 視窗大小為預設 (1280x720)
- **THEN** navbar MUST 出現在視窗最頂端
- **AND** 國旗 row MUST 緊接 navbar 下方, 無重疊
- **AND** 圖片區 MUST 佔據國旗 row 之下的剩餘空間

#### Scenario: 視窗縮至最小

- **WHEN** 使用者將視窗縮至 MinWidth/MinHeight (600x400)
- **THEN** navbar MUST 仍完整可見, 高度不變
- **AND** navbar 上的控制項 MUST 不被裁切

### Requirement: Settings 按鈕位於 navbar 右側

navbar MUST 在最右側包含一個 settings icon button (gear 圖示). 此 button MUST 為唯一入口開啟 Settings 視窗. button MUST 顯示 tooltip "Settings". button MUST 有 hover 視覺反饋 (背景顏色變化).

#### Scenario: Hover 與點擊 settings button

- **WHEN** 使用者滑鼠 hover settings button
- **THEN** button 背景 MUST 變色 (相對於 navbar 背景的可見差異)
- **AND** tooltip "Settings" MUST 在標準延遲後出現

#### Scenario: 點擊 settings button

- **WHEN** 使用者點擊 settings button
- **THEN** Settings 視窗 MUST 出現為主視窗的 modal child (`Owner = MainWindow`)
- **AND** 主視窗 MUST 不可被點擊操作直到 Settings 視窗關閉

### Requirement: Settings 視窗骨架

Settings 視窗 MUST 為獨立 WPF `Window`, MUST 為 modal (透過 `ShowDialog`). 視窗 MUST 至少包含: 標題列 (顯示 "Settings"), 內容區, 與關閉控制 (右上角 X 或 ESC 鍵). 視窗 MUST 可調整大小但 MUST 有合理 MinWidth/MinHeight (e.g. 480x360).

#### Scenario: 按 ESC 關閉

- **WHEN** Settings 視窗開啟中且使用者按 ESC
- **THEN** Settings 視窗 MUST 關閉
- **AND** 主視窗 MUST 重新獲得焦點

#### Scenario: 點擊標題列 X 關閉

- **WHEN** 使用者點擊 Settings 視窗右上角 X
- **THEN** Settings 視窗 MUST 關閉, MUST NOT 影響主視窗任何狀態

### Requirement: Default country 設定區段

Settings 視窗 MUST 包含一個 "Default country" 區段, 列出所有從 fetcher 探索到的國家 (含國旗與名稱). 使用者 MUST 能選擇任一國家為預設值, 並 MUST 看到目前選擇的視覺指示 (e.g. radio 選中, 框線, 或反白). 區段 MUST 顯示一行說明文字告知「此設定僅影響下次啟動」.

#### Scenario: 開啟 Settings 看到完整國家列表

- **WHEN** 使用者點 settings button 開啟 Settings 視窗
- **AND** fetcher 已回傳 11 個國家
- **THEN** Default country 區段 MUST 列出全部 11 個國家
- **AND** 國旗顯示順序 MUST 與 navbar 下方的國旗 row 相同

#### Scenario: 偏好為 null 時的視覺狀態

- **WHEN** 使用者開啟 Settings 視窗且 `preferences.json` 無 `defaultCountryCode` 欄位 (或為 null)
- **THEN** Default country 區段 MUST 顯示 `us` 為已選中狀態 (即實際 fallback 值)

#### Scenario: 偏好已設定為 jp 時的視覺狀態

- **WHEN** 使用者開啟 Settings 視窗且 `defaultCountryCode = "jp"`
- **THEN** Default country 區段 MUST 顯示 `jp` 為已選中狀態

### Requirement: 選擇 default country 立即持久化, 不切換目前桌布

當使用者於 Settings 點選某國家為 default country, 系統 MUST 立即將該選擇寫入 `preferences.json` 的 `defaultCountryCode` 欄位. 系統 MUST NOT 變更主視窗目前正在顯示的桌布, MUST NOT 改變主視窗目前選中的國旗.

#### Scenario: 選擇與目前不同的國家

- **WHEN** 主視窗目前顯示 jp 桌布, jp 國旗為選中狀態
- **AND** 使用者於 Settings 選 fr 為 default country
- **THEN** `preferences.json` 的 `defaultCountryCode` MUST 變為 `"fr"`
- **AND** 主視窗 MUST 繼續顯示 jp 桌布, jp 國旗 MUST 仍為選中狀態
- **AND** Settings 視窗 Default country 區段 MUST 顯示 fr 為已選中

#### Scenario: 寫入失敗

- **WHEN** 使用者選擇 default country 但磁碟 IO 失敗
- **THEN** 系統 MUST NOT crash
- **AND** Settings UI MUST 維持原狀 (不假裝寫入成功)

### Requirement: Default country 偏好欄位

`UserPreferences` 模型 MUST 新增 `DefaultCountryCode: string?` 屬性, 序列化為 `preferences.json` 中的 `defaultCountryCode` 欄位. 值 MUST 為 2 字母 lowercase 國家代碼 (e.g. `"jp"`) 或 null. 舊版 `preferences.json` 缺此欄位時, 反序列化結果 MUST 為 null, MUST NOT 拋例外.

#### Scenario: 全新使用者

- **WHEN** 應用程式首次執行, `preferences.json` 不存在
- **THEN** 系統 MUST 視為 `DefaultCountryCode = null`
- **AND** MUST NOT 在尚未需要時建立 `preferences.json`

#### Scenario: 舊版偏好檔僅含 closeAction

- **WHEN** `preferences.json` 內容為 `{ "closeAction": "MinimizeToTray" }`
- **THEN** 反序列化 MUST 成功
- **AND** `DefaultCountryCode` MUST 為 null
- **AND** `CloseAction` MUST 為 `MinimizeToTray`

#### Scenario: 寫入後欄位並存

- **WHEN** 使用者設過 default country = `"jp"` 且偏好 `closeAction = "Exit"`
- **THEN** `preferences.json` MUST 同時含 `defaultCountryCode: "jp"` 與 `closeAction: "Exit"`
