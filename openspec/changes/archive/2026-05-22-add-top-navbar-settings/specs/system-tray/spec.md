## MODIFIED Requirements

### Requirement: 偏好持久化

使用者偏好 (含關閉行為與 default country) MUST 儲存於 `%LOCALAPPDATA%\\WallpaperApp\\preferences.json` (或本專案 cache layer 採用的同等 user-local base path). JSON 結構 MUST 至少含以下欄位:

- `closeAction`: `"MinimizeToTray"` | `"Exit"` | `null`
- `defaultCountryCode`: 2 字母 lowercase 國家代碼字串 (e.g. `"jp"`) 或 `null` (或欄位省略)

寫入 MUST atomic (write-temp-then-rename 或同等). 讀取錯誤 (檔案不存在 / JSON 損毀 / IO 異常) MUST 視為偏好不存在 (所有欄位回到預設 null). 反序列化 MUST 對缺欄位寬容 (舊版只含 `closeAction` 的檔案 MUST 可正常載入).

#### Scenario: 首次寫入關閉偏好

- **WHEN** 應用程式首次寫入 `closeAction = "MinimizeToTray"` (使用者尚未設定 default country)
- **THEN** `preferences.json` MUST 出現於 `%LOCALAPPDATA%\\WallpaperApp\\` 下
- **AND** 檔案內容 MUST 為有效 JSON 含 `"closeAction": "MinimizeToTray"`
- **AND** `defaultCountryCode` 欄位 MUST 為 null 或省略

#### Scenario: 首次寫入 default country 偏好

- **WHEN** 應用程式首次寫入 `defaultCountryCode = "jp"` (使用者尚未設定 close action)
- **THEN** `preferences.json` MUST 出現於 `%LOCALAPPDATA%\\WallpaperApp\\` 下
- **AND** 檔案內容 MUST 為有效 JSON 含 `"defaultCountryCode": "jp"`
- **AND** `closeAction` 欄位 MUST 為 null 或省略

#### Scenario: 兩欄位並存

- **WHEN** 使用者已設 `closeAction = "Exit"` 與 `defaultCountryCode = "fr"`
- **THEN** `preferences.json` MUST 同時含兩欄位且值正確

#### Scenario: 偏好檔損毀

- **WHEN** `preferences.json` 存在但內容非有效 JSON
- **THEN** 應用程式 MUST 視為偏好不存在 (`closeAction = null`, `defaultCountryCode = null`)
- **AND** MUST NOT 因此 crash
- **AND** 下一次寫入 MUST 覆蓋損毀內容

#### Scenario: 舊版偏好檔僅含 closeAction

- **WHEN** `preferences.json` 內容為 `{ "closeAction": "MinimizeToTray" }` (例如升級前留下的檔案)
- **THEN** 反序列化 MUST 成功
- **AND** `CloseAction` MUST 為 `MinimizeToTray`
- **AND** `DefaultCountryCode` MUST 為 null
- **AND** 後續寫入 default country MUST 在不破壞 `closeAction` 的前提下新增欄位
