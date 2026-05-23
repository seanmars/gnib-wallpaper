## ADDED Requirements

### Requirement: 以本地圖片檔案套用為桌布

系統 SHALL 提供 `IWallpaperSetterService.SetWallpaperFromFile(string imagePath)`, 將指定的本地圖片設定為當前使用者 (`HKEY_CURRENT_USER`) 的 Windows 桌布. 套用 MUST 透過 Win32 `SystemParametersInfo` 並傳入 `SPI_SETDESKWALLPAPER (0x0014)`, `fuWinIni = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE (0x03)`. 系統 MUST 在呼叫 `SystemParametersInfo` 之前先寫入 registry 將 wallpaper style 固定為 Fill.

#### Scenario: 套用有效的 JPG 桌布

- **WHEN** caller 呼叫 `SetWallpaperFromFile("C:\\Users\\u\\cache\\jp.jpg")` 且該檔案存在且為合法 JPG
- **THEN** 方法 MUST 正常返回 (無 exception)
- **AND** `HKEY_CURRENT_USER\Control Panel\Desktop\WallpaperStyle` MUST 為 `"10"`
- **AND** `HKEY_CURRENT_USER\Control Panel\Desktop\TileWallpaper` MUST 為 `"0"`
- **AND** 桌面 MUST 在無需登出 / 手動 refresh 的情況下立即顯示為新桌布

#### Scenario: 套用有效的 PNG 桌布

- **WHEN** caller 呼叫 `SetWallpaperFromFile("D:\\images\\photo.png")` 且該檔案存在
- **THEN** 方法 MUST 正常返回
- **AND** 桌面 MUST 立即套用該 PNG 為桌布

### Requirement: 副檔名白名單

系統 MUST 僅接受副檔名為 `.bmp`, `.jpg`, `.jpeg`, `.png` 的圖片 (大小寫不敏感). 任何其他副檔名 (例如 `.webp`, `.gif`, `.heic`, `.tiff`, 無副檔名) MUST 拋出 `WallpaperSetterException` 且 `Kind = UnsupportedFormat`. 系統 MUST NOT 進行格式轉換.

#### Scenario: 傳入 webp 檔案

- **WHEN** caller 呼叫 `SetWallpaperFromFile("C:\\img\\sample.webp")` 即使該檔案存在
- **THEN** 方法 MUST 拋出 `WallpaperSetterException`
- **AND** `Kind` MUST 為 `UnsupportedFormat`
- **AND** registry 與桌布 MUST NOT 被修改

#### Scenario: 副檔名大小寫不敏感

- **WHEN** caller 呼叫 `SetWallpaperFromFile("C:\\img\\sample.JPG")` 或 `.Jpeg` 或 `.PNG`
- **THEN** 方法 MUST 視為合法副檔名並繼續處理 (不拋 `UnsupportedFormat`)

#### Scenario: 無副檔名

- **WHEN** caller 呼叫 `SetWallpaperFromFile("C:\\img\\sample")` 即使內容為合法 JPG
- **THEN** 方法 MUST 拋出 `WallpaperSetterException` 且 `Kind = UnsupportedFormat`

### Requirement: 路徑存在性驗證

系統 MUST 在執行任何 registry 寫入或 Win32 呼叫**之前**, 驗證 `imagePath` 對應的檔案存在. 不存在的路徑 (含拼錯 / 已刪除 / 不存在的磁碟) MUST 拋出 `WallpaperSetterException` 且 `Kind = FileNotFound`. 系統 MUST 將相對路徑經由 `Path.GetFullPath` 標準化為絕對路徑後再傳入 `SystemParametersInfo`.

#### Scenario: 路徑不存在

- **WHEN** caller 呼叫 `SetWallpaperFromFile("C:\\nope\\does-not-exist.jpg")`
- **THEN** 方法 MUST 拋出 `WallpaperSetterException`
- **AND** `Kind` MUST 為 `FileNotFound`
- **AND** registry 與桌布 MUST NOT 被修改

#### Scenario: 相對路徑被標準化

- **WHEN** caller 從 working directory `C:\app` 呼叫 `SetWallpaperFromFile(".\\cache\\us.jpg")` 且 `C:\app\cache\us.jpg` 存在
- **THEN** 方法 MUST 將路徑標準化為 `C:\app\cache\us.jpg` 並成功套用

### Requirement: Win32 呼叫失敗 surface 為 typed exception

當 `SystemParametersInfo` 回傳 `false` 時, 系統 MUST 透過 `Marshal.GetLastWin32Error()` 取得 error code, 並拋出 `WallpaperSetterException` 且 `Kind = Win32CallFailed`, exception message MUST 包含該 error code (或文字描述). 系統 MUST 在呼叫之前清空 last error (`Marshal.SetLastSystemError(0)`), 以避免回報殘留 error.

#### Scenario: SystemParametersInfo 回傳 false

- **WHEN** OS 因為任何原因 (例如圖片損毀) 使 `SystemParametersInfo` 回傳 `false` 且 last error = `ERROR_FILE_NOT_FOUND (2)`
- **THEN** 方法 MUST 拋出 `WallpaperSetterException`
- **AND** `Kind` MUST 為 `Win32CallFailed`
- **AND** exception message MUST 包含 `2` 或對應描述

### Requirement: Registry 寫入失敗 surface 為 typed exception

當寫入 `HKEY_CURRENT_USER\Control Panel\Desktop\WallpaperStyle` 或 `TileWallpaper` 因 group policy / 權限不足而失敗時, 系統 MUST 拋出 `WallpaperSetterException` 且 `Kind = RegistryWriteFailed`, inner exception MUST 為原始 `UnauthorizedAccessException` (或對應 exception). 系統 MUST NOT 在 registry 失敗後仍嘗試呼叫 `SystemParametersInfo`.

#### Scenario: Registry 被 group policy 鎖

- **WHEN** 環境政策阻擋 `Control Panel\Desktop` 寫入, 寫 `WallpaperStyle` 時拋出 `UnauthorizedAccessException`
- **THEN** 方法 MUST 重新包裝為 `WallpaperSetterException` 且 `Kind = RegistryWriteFailed`
- **AND** `InnerException` MUST 為原本的 `UnauthorizedAccessException`
- **AND** 桌布 MUST NOT 被修改 (`SystemParametersInfo` MUST NOT 被呼叫)

### Requirement: Service 註冊於 App composition root

`App.xaml.cs::OnStartup` MUST 構造 `WallpaperSetterService` 並指派給 `App.WallpaperSetter` (型別為 `IWallpaperSetterService`) property, 與既有 `Preferences`, `WindowClose` 相同模式. 此 property MUST 在 `OnStartup` 完成後對所有 ViewModel / service 可見.

#### Scenario: App 啟動後取得 service

- **WHEN** `App.OnStartup` 完成
- **THEN** `App.CurrentApp.WallpaperSetter` MUST 為非 null
- **AND** 型別 MUST 為 `IWallpaperSetterService`
- **AND** 呼叫 `SetWallpaperFromFile` MUST 可運作 (不拋 `NullReferenceException`)
