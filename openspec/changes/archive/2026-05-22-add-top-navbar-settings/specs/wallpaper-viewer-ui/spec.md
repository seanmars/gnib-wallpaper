## MODIFIED Requirements

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
