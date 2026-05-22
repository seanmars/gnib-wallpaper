## 1. 專案準備

- [x] 1.1 在 `WallpaperApp/WallpaperApp.csproj` 加入 `AngleSharp` PackageReference (最新穩定版)
- [x] 1.2 執行 `dotnet restore` 確認套件還原無錯誤

## 2. Models

- [x] 2.1 建立 `WallpaperApp/Models/Country.cs` (record: `Code`, `Name`)
- [x] 2.2 建立 `WallpaperApp/Models/DetailLink.cs` (record: `Country`, `Slug`, `DetailUrl`)
- [x] 2.3 建立 `WallpaperApp/Models/Wallpaper.cs` (含 `DownloadUrls` dictionary: 4K/2K/HD)
- [x] 2.4 建立 `WallpaperApp/Models/CachedMetadata.cs` (對應 cache json 結構)
- [x] 2.5 建立 `WallpaperApp/Models/LoadState.cs` (enum: Loading, Loaded, Error)

## 3. Fetcher Service

- [x] 3.1 建立 `WallpaperApp/Services/FetcherException.cs` (自訂 exception)
- [x] 3.2 建立 `WallpaperApp/Services/BingFetcher.cs` 框架 (含 `static readonly HttpClient`, User-Agent, 30s timeout)
- [x] 3.3 實作 `DiscoverCountriesAsync()` — 用 AngleSharp 解析首頁 `<a href="/{code}">`, 過濾非國家路徑
- [x] 3.4 實作 `GetTodayDetailLinkAsync(country)` — 解析國家頁第一個 `/detail/{code}/` 連結
- [x] 3.5 實作 `FetchAndParseDetailAsync(link)` — 解析三種解析度 URL (`w:3840`, `w:2560`, `w:1920`) 與 title/copyright
- [x] 3.6 加入 `PickBestResolution(wallpaper)` helper (4K → 2K → 1080p fallback)
- [x] 3.7 加入 HTTP 失敗統一包成 `FetcherException` 的錯誤處理

## 4. Cache Service

- [x] 4.1 建立 `WallpaperApp/Services/WallpaperCache.cs`
- [x] 4.2 實作 `GetCacheRoot()` — `%LOCALAPPDATA%/WallpaperApp/cache/`
- [x] 4.3 實作 `TryLoadTodayAsync(countryCode)` — 檢查 metadata.fetched_date 是否為今日 UTC, 回傳 (BitmapImage, Wallpaper)? 或 null
- [x] 4.4 實作 `SaveAsync(wallpaper, imageBytes, resolution)` — 寫 jpg + json
- [x] 4.5 實作 "每國保留最新一張": 寫入新檔後刪除同國資料夾內所有其他 jpg/json
- [x] 4.6 寫入失敗 (IOException) 時 try/catch + log, 不向外拋

## 5. Image Downloader

- [x] 5.1 在 `BingFetcher` 加入 `DownloadImageBytesAsync(url, ct)` — 用 HttpClient 下載並回傳 `byte[]`
- [x] 5.2 支援 `CancellationToken` 取消

## 6. ViewModels

- [x] 6.1 建立 `WallpaperApp/ViewModels/MainViewModel.cs` 繼承 `ObservableObject`
- [x] 6.2 加入 `[ObservableProperty]`: `Countries`, `SelectedCountry`, `CurrentImage` (BitmapImage), `CurrentWallpaper` (Wallpaper?), `State` (LoadState), `ErrorMessage`
- [x] 6.3 加入 `[RelayCommand] SelectCountryAsync(Country)` 處理切換國家
- [x] 6.4 加入 `CancellationTokenSource` 欄位, 新請求取消舊請求
- [x] 6.5 載入流程: cache 命中 → 直接設 CurrentImage; cache miss → State=Loading → fetch → save → 設 CurrentImage → State=Loaded
- [x] 6.6 例外捕捉設 State=Error + ErrorMessage, 不 crash
- [x] 6.7 建構式內 `InitializeAsync()` 呼叫: 探索國家清單 → SelectedCountry = us → SelectCountryAsync(us)

## 7. Views (XAML)

- [x] 7.1 修改 `App.xaml` — 加入全域 resources: `BooleanToVisibilityConverter`, `LoadStateToVisibilityConverter` (自訂)
- [x] 7.2 建立 `WallpaperApp/Converters/LoadStateToVisibilityConverter.cs` (參數判斷顯示哪一狀態)
- [x] 7.3 建立 `WallpaperApp/Converters/CountryCodeToFlagEmojiConverter.cs` — `us` → `🇺🇸` (region indicator symbols)
- [x] 7.4 改寫 `MainWindow.xaml`: 使用 `DockPanel`, 上方 `Border` (DockPanel.Dock=Top) 含 `ScrollViewer` + `ItemsControl` (橫向 StackPanel) 渲染國旗按鈕; 下方 `Grid` 含 `Image` + `ProgressBar` + 錯誤 `TextBlock`
- [x] 7.5 國旗按鈕 ItemTemplate: `Button` 內含 `TextBlock` (顯示 emoji), Command 綁定 `SelectCountryCommand`, CommandParameter 綁定當前 Country
- [x] 7.6 設定 `Image.Stretch="Uniform"`, `Source="{Binding CurrentImage}"`
- [x] 7.7 `ProgressBar` IsIndeterminate=True, Visibility 綁 State==Loading
- [x] 7.8 錯誤 TextBlock Visibility 綁 State==Error, Text 綁 ErrorMessage
- [x] 7.9 SelectedCountry 對應的國旗按鈕加上視覺選中樣式 (e.g. 框線) — 用 `EqualityToBoolConverter` MultiBinding 比對當前 Country 與 ItemsControl.DataContext.SelectedCountry, 套上藍色框線
- [x] 7.10 修改 `MainWindow.xaml.cs` — DataContext = new MainViewModel() (透過 `<Window.DataContext>` XAML 宣告, 由 Loaded 事件觸發 `InitializeAsync()`)
- [x] 7.11 確認 `Window.Title` 顯示 `Bing Wallpaper - {SelectedCountry.Name}` (binding) — 進階為 `WindowTitle` ObservableProperty, 含國家與圖名

## 8. 整合驗證

- [x] 8.1 `dotnet build` 通過無 warning (可接受 nullable warnings 已處理) — 結果: 0 warning, 0 error
- [x] 8.2 `dotnet run` 啟動: 視窗開啟即顯示 loading, 數秒後顯示美國今日桌布
- [x] 8.3 手動測試: 點 jp 國旗 → 顯示 loading → 切換為日本桌布; 再點 us → < 200 ms 顯示美國 (cache 命中)
- [x] 8.4 手動測試: 拔網路後重啟 → 顯示錯誤訊息且不 crash
- [x] 8.5 手動測試: 縮小視窗 → 國旗 row 不壓圖, 圖片區依比例縮放
- [x] 8.6 確認 cache 目錄 `%LOCALAPPDATA%/WallpaperApp/cache/us/` 內僅 1 jpg + 1 json
- [x] 8.7 手動測試: 連續快速點不同國旗 → 最終結果為最後一次點擊的國家

## 9. 收尾

- [x] 9.1 更新或新增 `WallpaperApp/README.md` 簡述使用方式 (繁體中文, 簡潔)
- [x] 9.2 確認 `.gitignore` 已忽略 `bin/`, `obj/` (已存在於專案)
- [x] 9.3 執行 `openspec archive add-bing-wallpaper-viewer` (待全部任務完成後)
