## 1. Preferences schema 擴充

- [x] 1.1 在 `WallpaperApp/Models/UserPreferences.cs` 新增 `public string? DefaultCountryCode { get; set; }` 屬性 (JSON 預設 camelCase → `defaultCountryCode`)
- [x] 1.2 在 `IUserPreferencesService` 新增 `Task SetDefaultCountryAsync(string code, CancellationToken ct = default)` 介面方法 (read-modify-write 風格, 與 `ResetCloseActionAsync` 對齊)
- [x] 1.3 在 `UserPreferencesService` 實作 `SetDefaultCountryAsync` (load → set → save, 利用既有 `_gate` SemaphoreSlim 與 atomic temp-then-rename). 同步加上 `PropertyNamingPolicy=CamelCase` + `PropertyNameCaseInsensitive=true` 以對齊 spec 對 `closeAction` / `defaultCountryCode` 欄位的 camelCase 要求, 並讓舊有 PascalCase 檔案仍可載入.
- [x] 1.4 手動驗證: 啟動程式 (不調整任何 UI), 確認舊版只含 `closeAction` 的 `preferences.json` 仍可正常載入 (寫死一個檔案測試)

## 2. App-settings ViewModel + View 骨架

- [x] 2.1 新增 `WallpaperApp/ViewModels/SettingsViewModel.cs` (`ObservableObject`), 建構式注入 `IUserPreferencesService` 與目前的 `IReadOnlyList<CountryItem>`
- [x] 2.2 SettingsViewModel 暴露 `Countries: IReadOnlyList<SettingsCountryItem>` 與 `SelectedDefault: SettingsCountryItem?`. `SettingsCountryItem` 含 `Code / Name / FlagImage / IsDefault` 屬性 (沿用 `CountryItem` 的 flag 圖片避免重抓)
- [x] 2.3 SettingsViewModel 暴露 `[RelayCommand] SelectDefaultAsync(SettingsCountryItem)` — 寫入 preference, 更新各 item 的 `IsDefault` 視覺狀態, 不關閉視窗, 不切換主視窗
- [x] 2.4 SettingsViewModel 初始化時讀取 `preferences.LoadAsync()`, 將對應 code 的 item 設為 `IsDefault = true` (若 `DefaultCountryCode` 為 null, fallback 至 `us` item 顯示為選中, 對應 spec scenario)
- [x] 2.5 新增 `WallpaperApp/Views/SettingsWindow.xaml` (+ `.xaml.cs`): `Window` MinWidth=480, MinHeight=360, Title="Settings", 內含 "Default country" 區段 (`ItemsControl` 列出國家, 每項顯示國旗 + 名稱 + selected indicator), 與一行說明 "Applies on next startup"
- [x] 2.6 確保 ESC 與標題列 X 關閉 Settings 視窗, 不影響主視窗狀態 (PreviewKeyDown 處理 ESC; X 為 WPF Window 標題列預設行為)

## 3. Top navbar 與 settings 入口

- [x] 3.1 在 `MainWindow.xaml` 新增 `Border DockPanel.Dock="Top"` 作為 navbar, 放在現有國旗 row Border 之**上** (DockPanel 中先宣告的 Top child 排序在上). 樣式: 高度約 36-40 px, Background `#1A1A1A`, BorderBrush `#333333`, BorderThickness="0,0,0,1"
- [x] 3.2 在 navbar 內以 `DockPanel` 或 `Grid` 將 gear button 置於右側 (`HorizontalAlignment="Right"`). button 為純 icon 按鈕, Width/Height 約 32, ToolTip="Settings", Cursor="Hand"
- [x] 3.3 將 gear 圖示以 XAML `Path Data="..."` (24x24 viewbox 標準齒輪) 繪製; Stroke/Fill 採 `#DDDDDD`, hover 時背景 `#2A2A2A` (套用 `ControlTemplate.Triggers`)
- [x] 3.4 在 `MainViewModel` 新增 `[RelayCommand] private void OpenSettings()` — 建立 `SettingsViewModel(_preferences, Countries.ToList())`, 建立 `SettingsWindow { DataContext = vm, Owner = Application.Current.MainWindow }` 並 `ShowDialog`
- [x] 3.5 navbar gear button `Command="{Binding OpenSettingsCommand}"` 綁定到 `MainViewModel`
- [x] 3.6 `App.xaml.cs` 既有 `Preferences` accessor 沿用; 為了讓 MainViewModel 拿到同一個 service 實例 (避免兩個 SemaphoreSlim), 改為在 `App.OnStartup` 顯式建立 `MainViewModel` 並設為 `mainWindow.DataContext`, 同步從 XAML 移除 `<Window.DataContext><vm:MainViewModel/>` 以避免重複建構

## 4. 啟動時讀取 default country preference

- [x] 4.1 修改 `MainViewModel` 建構式以接受 `IUserPreferencesService` (新 overload, 預設 ctor 自帶 `new UserPreferencesService()` 以保留設計時 / 後備支援)
- [x] 4.2 修改 `InitializeAsync`: discover countries 後, `var prefs = await _preferences.LoadAsync()` → 用 `prefs.DefaultCountryCode` 套用 Decision 6 的 fallback 鏈 (使用者偏好 → us → first)
- [x] 4.3 移除或標記過時 `DefaultCountryCode = "us"` 常數 (已改名為 `FallbackCountryCode` 以明示其角色)
- [x] 4.4 確認 InitializeAsync 對 `_preferences.LoadAsync()` 例外的處理仍合理 (既有 service 內部已 catch IO/JSON 例外回傳 default; ViewModel 端不需重複處理)

## 5. 整合測試 (手動)

- [x] 5.1 啟動 (preferences.json 不存在): MUST 出現 navbar, 預設載入 us, us 國旗選中
- [x] 5.2 點 navbar gear: Settings 視窗 MUST 出現, 列出 11 國, us MUST 顯示為已選中
- [x] 5.3 選 jp 為 default country: `preferences.json` MUST 立即寫入 `defaultCountryCode: "jp"`, 主視窗 MUST 不切換桌布 (仍顯示原國家)
- [x] 5.4 關閉 Settings, 完整關閉應用程式 (tray exit), 重啟: MUST 直接載入 jp 桌布, jp 國旗 MUST 選中
- [x] 5.5 手動將 `preferences.json` 內的 `defaultCountryCode` 改為不存在的 `"xx"`, 重啟: MUST fallback 至 us, MUST NOT crash, 檔案內的 `"xx"` MUST NOT 被自動清空
- [x] 5.6 手動將 `preferences.json` 內容寫為非 JSON (e.g. `not json`), 重啟: MUST 視為偏好不存在, MUST 載入 us, MUST NOT crash. 之後在 Settings 選 fr → 檔案 MUST 被新內容覆蓋
- [x] 5.7 視窗縮至 MinWidth/MinHeight (600x400): navbar 與國旗 row MUST 仍完整可見, 圖片區依比例縮小

## 6. 文件與收尾

- [x] 6.1 更新 `WallpaperApp/README.md` 「操作」段落, 描述 navbar / gear button / Settings 視窗, 與 default country 偏好「下次啟動才生效」的行為
- [x] 6.2 更新 `WallpaperApp/README.md` 「偏好與 Cache 位置」段落, `preferences.json` schema 新增 `defaultCountryCode` 欄位說明, 並附註 case-insensitive 反序列化
- [x] 6.3 執行 `dotnet build WallpaperApp/WallpaperApp.csproj` (0 warnings, 0 errors)
- [x] 6.4 執行 `openspec validate add-top-navbar-settings --strict` (Change 'add-top-navbar-settings' is valid)
