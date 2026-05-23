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

點擊任一國旗 MUST 觸發顯示該國今日桌布. 系統 MUST 在 cache 命中時即時切換 (無 loading), 若該國背景載入仍在進行則顯示 loading 動畫直到該國自身完成. 切換國家 MUST NOT 取消任何**其他**國家正在進行的背景載入. 重複點擊**同一**國家 MAY 取消並重啟該國載入 (僅當該國 state = Error 時).

#### Scenario: 點擊已快取國家

- **WHEN** 使用者點擊 jp 國旗且 jp cache 為今日
- **THEN** 圖片區 MUST 在 < 200 ms 內切換為日本桌布
- **AND** loading 動畫 MUST NOT 出現

#### Scenario: 點擊背景載入中的國家

- **WHEN** 使用者點擊 fr 國旗且 fr 在背景仍 Loading
- **THEN** 圖片區 MUST 顯示 loading 動畫
- **AND** fr 背景載入 MUST 繼續, MUST NOT 被重啟
- **AND** fr 完成後圖片區 MUST 自動切換為法國桌布

#### Scenario: 切換中其他國家持續載入

- **WHEN** us 與 jp 與 de 同時在背景載入
- **AND** 使用者於 us 載入中點擊 jp
- **THEN** us 與 de 的背景載入 MUST 持續不被 cancel
- **AND** SelectedCountry MUST 變為 jp 且圖片區顯示 jp 對應的 LoadState (Loading / Loaded)

#### Scenario: 點擊 Error 狀態國家重試

- **WHEN** fr LoadState = Error 且使用者再次點擊 fr 國旗
- **THEN** fr LoadState MUST 重置為 Loading 並重新發起背景載入
- **AND** 其他國家的 in-flight 任務 MUST 不受影響

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

當 `SelectedCountry.LoadState = Loading` 時, MUST 在圖片區中央顯示載入動畫 (ProgressBar 或 ProgressRing). 動畫 MUST 在 `SelectedCountry.LoadState` 轉為 Loaded 或 Error 時消失. 動畫的顯示 MUST 僅取決於目前選中國家的 state, 與其他國家的 LoadState 無關.

#### Scenario: 首次啟動 (預設國家 cache miss)

- **WHEN** 應用程式啟動且預設國家 cache 為空
- **THEN** 主視窗顯示後 MUST 在圖片區立即顯示 loading 動畫
- **AND** 動畫 MUST 在預設國家自身載入完成時消失

#### Scenario: 切換到背景仍 Loading 的國家

- **WHEN** 使用者點擊一個 `LoadState = Loading` 的國家
- **THEN** 圖片區 MUST 立即顯示 loading 動畫 (無 HTTP 重新發起)

#### Scenario: 切換到已 Loaded 國家

- **WHEN** 使用者點擊一個 `LoadState = Loaded` 的國家
- **THEN** 圖片區 MUST 直接顯示該國桌布
- **AND** loading 動畫 MUST NOT 出現

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

### Requirement: 每國載入狀態獨立追蹤

`CountryItem` MUST 暴露 per-country 的 `LoadState` (Loading / Loaded / Error) 與對應的 `ErrorMessage`. 國旗 row 中的每個按鈕 MUST 能以視覺方式 (e.g. spinner overlay, error tint) 反映該國當下的 LoadState. 同一時刻多個國家 MUST 可同時處於 Loading 狀態.

#### Scenario: 啟動時所有國旗同時 loading

- **WHEN** 應用程式啟動完成且所有國家 cache 皆為空
- **THEN** 國旗 row 上的 11 個按鈕 MUST 全部呈現 Loading 視覺狀態
- **AND** 不需使用者操作即可逐一變為 Loaded 視覺狀態

#### Scenario: 部分國家已 cache

- **WHEN** 啟動時 jp 與 us 為今日 cache 命中, 其餘為 cache miss
- **THEN** jp 與 us 國旗按鈕 MUST 直接顯示為 Loaded 狀態 (無 spinner)
- **AND** 其餘 9 個按鈕 MUST 顯示 Loading 狀態直到各自完成

#### Scenario: 單一國家載入失敗

- **WHEN** fr 在背景載入時網路 timeout
- **THEN** fr 國旗按鈕 MUST 視覺上呈現 Error 狀態 (e.g. 紅色邊框或 icon)
- **AND** 其他國家的 LoadState MUST 不受影響

### Requirement: 啟動時對所有國家 fan-out 載入

應用程式啟動且 `BingFetcher.DiscoverCountriesAsync()` 完成後, 系統 MUST 對清單內**每一個**國家發起背景今日桌布載入. fan-out MUST 在 `MainViewModel.InitializeAsync` 內完成派發 (即所有 EnsureLoadStarted 呼叫已執行), MUST NOT 等待任何下載完成才回傳. 預設選中國家的判定邏輯不變, 但該國 MUST NOT 享有比其他國家更高的載入優先權.

#### Scenario: 11 國全部 fan-out

- **WHEN** fetcher 回傳 11 國且 `InitializeAsync` 即將回傳
- **THEN** 11 國 MUST 全部已透過 `EnsureLoadStartedAsync` (或等效機制) 派發背景載入任務
- **AND** `InitializeAsync` MUST 在 fan-out 派發後立即回傳, MUST NOT await 任一國家的網路階段

#### Scenario: 啟動後即可看到國旗 row

- **WHEN** `InitializeAsync` 回傳完畢且尚無任何國家完成載入
- **THEN** UI MUST 已顯示 11 個國旗按鈕 (即使 FlagImage 仍在載入)
- **AND** 預設國家對應的圖片區 MUST 顯示 Loading 動畫

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

