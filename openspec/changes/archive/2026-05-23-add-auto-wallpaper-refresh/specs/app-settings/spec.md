## ADDED Requirements

### Requirement: Auto-refresh 設定區段

Settings 視窗 MUST 包含一個 "Auto-refresh" 區段, 位於 "Default country" 區段之下. 區段 MUST 包含: (1) 啟用 toggle (CheckBox 或 ToggleSwitch) 標示 "Enable auto-refresh"; (2) 間隔分鐘數輸入控件 (NumericUpDown 或 TextBox + spinner) 標示 "Check every (minutes)", 範圍 5-1440, 預設 60; (3) 一行說明文字 "Periodically checks for new wallpapers for your default country." 間隔輸入 MUST 在 toggle 為 disabled 時顯示為 grayed out (`IsEnabled = false`).

#### Scenario: 開啟 Settings 看到 Auto-refresh 區段

- **WHEN** 使用者點 settings button 開啟 Settings 視窗
- **THEN** Auto-refresh 區段 MUST 出現在 Default country 區段之下
- **AND** toggle 與間隔輸入 MUST 顯示當前偏好值

#### Scenario: 偏好 enabled = true, interval = 60 的視覺狀態

- **WHEN** 使用者開啟 Settings 視窗且偏好為 `autoRefreshEnabled = true, autoRefreshIntervalMinutes = 60`
- **THEN** toggle MUST 為 checked 狀態
- **AND** 間隔輸入 MUST 顯示 `60`
- **AND** 間隔輸入 MUST 為可編輯 (`IsEnabled = true`)

#### Scenario: 偏好 enabled = false 的視覺狀態

- **WHEN** 使用者開啟 Settings 視窗且偏好為 `autoRefreshEnabled = false`
- **THEN** toggle MUST 為 unchecked
- **AND** 間隔輸入 MUST 為 grayed out (`IsEnabled = false`)
- **AND** 間隔輸入仍 MUST 顯示當前 interval 值 (不清零)

### Requirement: Auto-refresh 偏好變更立即持久化

當使用者於 Settings 切換 enable toggle 或變更 interval 值, 系統 MUST 立即將變更寫入 `preferences.json` (與 default country 一致的「立即寫入」行為). 變更 MUST NOT 等待視窗關閉. 寫入失敗時 MUST NOT crash, MUST 維持 UI 原狀.

#### Scenario: 切換 enable

- **WHEN** Settings 視窗開啟, 使用者將 enable toggle 從 ON 切到 OFF
- **THEN** `preferences.json` 的 `autoRefreshEnabled` MUST 立即變為 `false`
- **AND** scheduler MUST 在偏好寫入後被 `Stop()`

#### Scenario: 變更 interval

- **WHEN** Settings 視窗開啟, 使用者將 interval 從 60 改為 30 (合法範圍內)
- **THEN** `preferences.json` 的 `autoRefreshIntervalMinutes` MUST 立即變為 `30`
- **AND** scheduler MUST 以新間隔重啟 timer

#### Scenario: 寫入失敗

- **WHEN** 使用者切換 enable 但磁碟 IO 失敗
- **THEN** 系統 MUST NOT crash
- **AND** Settings UI MUST 維持原狀 (不假裝寫入成功)

### Requirement: Interval 輸入範圍驗證

間隔輸入 MUST 限制使用者輸入於 `[5, 1440]` 範圍內. 超出範圍的輸入 MUST 被 clamp 至最近界線值且 UI MUST 顯示 clamp 後的數值. 非數字輸入 MUST 被拒絕 (還原為前一個合法值).

#### Scenario: 輸入超過上限

- **WHEN** 使用者於 interval 輸入 `3000`
- **THEN** 控件 MUST 顯示 `1440` (clamp)
- **AND** `preferences.json` MUST 寫入 `1440`

#### Scenario: 輸入低於下限

- **WHEN** 使用者於 interval 輸入 `1`
- **THEN** 控件 MUST 顯示 `5` (clamp)

#### Scenario: 輸入非數字

- **WHEN** 使用者貼上字串 `"abc"` 到 interval 輸入
- **THEN** 控件 MUST 還原為前一個合法值
- **AND** `preferences.json` MUST NOT 被修改

### Requirement: Auto-refresh 偏好欄位

`UserPreferences` 模型 MUST 新增兩個屬性: `AutoRefreshEnabled: bool` (序列化欄位 `autoRefreshEnabled`, 預設 true) 與 `AutoRefreshIntervalMinutes: int` (序列化欄位 `autoRefreshIntervalMinutes`, 預設 60, clamp 範圍 [5, 1440]). 舊版 `preferences.json` 缺欄位時 MUST 退回預設值, MUST NOT 拋例外. 反序列化遇到超界 interval 值時 MUST clamp 而非拋例外.

#### Scenario: 全新使用者首次啟動

- **WHEN** 應用程式首次執行, `preferences.json` 不存在
- **THEN** 系統 MUST 視為 `AutoRefreshEnabled = true`, `AutoRefreshIntervalMinutes = 60`
- **AND** MUST NOT 在尚未需要時建立 `preferences.json`

#### Scenario: 舊版偏好檔僅含 default country 與 closeAction

- **WHEN** `preferences.json` 內容為 `{ "defaultCountryCode": "jp", "closeAction": "Exit" }`
- **THEN** 反序列化 MUST 成功
- **AND** `AutoRefreshEnabled` MUST 為 true (預設)
- **AND** `AutoRefreshIntervalMinutes` MUST 為 60 (預設)

#### Scenario: 偏好檔含超界 interval

- **WHEN** `preferences.json` 含 `"autoRefreshIntervalMinutes": 99999`
- **THEN** 反序列化 MUST 成功
- **AND** `AutoRefreshIntervalMinutes` MUST 被 clamp 為 `1440`

#### Scenario: 寫入後三欄位並存

- **WHEN** 使用者設過 `defaultCountryCode = "jp"`, `closeAction = "Exit"`, `autoRefreshEnabled = false`, `autoRefreshIntervalMinutes = 120`
- **THEN** `preferences.json` MUST 同時含四個欄位且值正確
