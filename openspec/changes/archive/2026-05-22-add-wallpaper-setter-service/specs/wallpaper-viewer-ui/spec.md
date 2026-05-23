## ADDED Requirements

### Requirement: Top navbar 提供「Set as desktop wallpaper」按鈕

Top navbar MUST 在 Settings 按鈕左側提供「Set as desktop wallpaper」按鈕. 按鈕 MUST 僅在當前圖片區已有 wallpaper 可顯示 (`MainViewModel.CurrentImage` 非 null) 時可見, 載入中 / 錯誤狀態 / 尚未選擇國家時 MUST 隱藏 (而非僅 disabled). 按鈕 MUST 採用與既有 navbar 按鈕一致的視覺樣式 (32x32, 透明背景, hover / pressed 反白, 18x18 icon).

#### Scenario: 應用程式啟動時尚未載入任何桌布

- **WHEN** 應用程式啟動完成且 `SelectedCountry.LoadState = Loading` (圖片區仍在 loading 動畫)
- **THEN** Set-as-desktop 按鈕 MUST 不可見

#### Scenario: 當前國家已 Loaded

- **WHEN** `SelectedCountry.LoadState = Loaded` 且 `MainViewModel.CurrentImage` 非 null
- **THEN** Set-as-desktop 按鈕 MUST 顯示在 navbar 右側 (位於 Settings 按鈕左側)
- **AND** 按鈕 MUST 可點擊
- **AND** 按鈕 tooltip MUST 為 "Set as desktop wallpaper"

#### Scenario: 當前國家為 Error

- **WHEN** `SelectedCountry.LoadState = Error`
- **THEN** Set-as-desktop 按鈕 MUST 不可見

#### Scenario: 切換到尚在背景載入的國家

- **WHEN** 使用者點擊一個 `LoadState = Loading` 的國旗, 使 `CurrentImage` 變為 null
- **THEN** Set-as-desktop 按鈕 MUST 立即變為不可見
- **AND** 該國載入完成後 (圖片可見) MUST 自動回復可見

### Requirement: 「Set as desktop wallpaper」按鈕呼叫 service 並回報錯誤

點擊 Set-as-desktop 按鈕 MUST 觸發 `MainViewModel.SetAsDesktopCommand`, 該 command MUST 將 `SelectedCountry.CachedImagePath` 傳入 `IWallpaperSetterService.SetWallpaperFromFile`. 系統 MUST 在 ViewModel 層攔截 `WallpaperSetterException`, 並以 modal `MessageBox` (icon = Warning, 標題 = "Set Wallpaper") 顯示包含 exception message 的錯誤. 成功路徑 MUST NOT 顯示對話框 (桌布變化即為使用者回饋). Command 執行 MUST NOT 修改 `LoadState`, `CurrentImage`, `CurrentWallpaper`, 也 MUST NOT 取消任何國家正在進行的背景載入.

#### Scenario: 成功套用桌布

- **WHEN** 使用者點擊 Set-as-desktop 按鈕且 `SetWallpaperFromFile` 順利返回 (無 exception)
- **THEN** Windows 桌布 MUST 切換為當前顯示的圖片
- **AND** UI MUST NOT 出現任何錯誤對話框
- **AND** 圖片區 MUST 維持顯示同一張圖片
- **AND** `SelectedCountry.LoadState` MUST 維持 `Loaded`

#### Scenario: Service 拋出 WallpaperSetterException

- **WHEN** 使用者點擊 Set-as-desktop 按鈕且 `SetWallpaperFromFile` 拋出 `WallpaperSetterException` (例如快取檔案被外部刪除導致 `FileNotFound`, 或 group policy 阻擋 registry 寫入導致 `RegistryWriteFailed`)
- **THEN** UI MUST 顯示 modal `MessageBox`, 標題為 "Set Wallpaper", icon 為 `MessageBoxImage.Warning`, 內文 MUST 包含原始 `Exception.Message`
- **AND** 圖片區 MUST 不被修改
- **AND** `SelectedCountry.LoadState` MUST 維持原本狀態

#### Scenario: 點擊時尚無快取路徑

- **WHEN** 在 race condition 下 `SelectedCountry.CachedImagePath` 為 null (例如按鈕剛變可見, 但 ViewModel state 尚未完全同步)
- **THEN** command MUST 靜默 no-op (不拋出 exception, 不顯示對話框, 不呼叫 service)

### Requirement: CountryItem 追蹤快取桌布絕對路徑

`CountryItem` MUST 暴露 `CachedImagePath` (型別 `string?`) observable property. `MainViewModel.LoadCountryAsync` MUST 在以下兩個成功路徑將絕對檔案路徑寫入該 property: (a) `WallpaperCache.TryLoadTodayAsync` 命中今日 cache 時, 使用 `CachedMetadata.ImagePath`; (b) `WallpaperCache.SaveAsync` 完成下載時, 使用其回傳之 `CachedMetadata.ImagePath`. 路徑 MUST 為絕對路徑且 MUST 對應磁碟上實際存在的 JPG 檔案 (寫入 property 的當下).

#### Scenario: Cache hit 時設定 path

- **WHEN** `LoadCountryAsync` 經由 `TryLoadTodayAsync` 命中今日快取
- **THEN** 對應 `CountryItem.CachedImagePath` MUST 為非 null 的絕對路徑
- **AND** 該路徑 MUST 等於 `metadata.ImagePath`
- **AND** 該檔案 MUST 存在於磁碟上 (cache 行為保證)

#### Scenario: 新下載完成時設定 path

- **WHEN** `LoadCountryAsync` 完成下載並呼叫 `WallpaperCache.SaveAsync(...)` 取得非 null 的 `CachedMetadata`
- **THEN** 對應 `CountryItem.CachedImagePath` MUST 等於 `metadata.ImagePath`

#### Scenario: SaveAsync 回傳 null (寫檔失敗)

- **WHEN** `WallpaperCache.SaveAsync` 回傳 null (例如磁碟寫入失敗)
- **THEN** `CountryItem.CachedImagePath` MUST 維持 null
- **AND** Set-as-desktop 按鈕在點擊時 MUST 依靜默 no-op 規則處理
