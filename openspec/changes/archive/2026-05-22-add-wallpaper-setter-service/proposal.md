## Why

WallpaperApp 目前只能瀏覽與快取 Bing 桌布, 沒有任何將圖片套用為 Windows 桌面的能力. 缺少這個能力使得「看到喜歡的桌布」與「實際把它設為桌布」之間需要使用者手動操作 (右鍵 -> Set as desktop background), 這是這款 App 預期使用者旅程中最自然的下一步. 此 change 補上 service 層的核心能力, 為後續 UI 整合 (例如 main view 的 "Set as wallpaper" 按鈕, 或 system tray 的快捷操作) 鋪路.

## What Changes

- 新增 `IWallpaperSetterService` 與其 Windows 實作 `WallpaperSetterService`, 接受本地圖片檔案路徑並透過 Win32 `SystemParametersInfo` (SPI_SETDESKWALLPAPER) 將該圖片套用為目前使用者的桌面.
- 套用時 MUST 固定為 Fill style: 寫入 `HKCU\Control Panel\Desktop` 的 `WallpaperStyle = "10"`, `TileWallpaper = "0"`, 之後再呼叫 `SystemParametersInfo` 觸發即時刷新.
- 僅接受副檔名為 `.bmp`, `.jpg`, `.jpeg`, `.png` 的本地檔案. 其他副檔名 (例如 `.webp`, `.gif`, `.heic`) 與不存在的路徑 MUST 拋出明確的 exception, 不進行格式轉換.
- 在 `App.xaml.cs` 註冊 `IWallpaperSetterService` 為 App-scoped singleton, 對齊 `Preferences` / `WindowClose` 既有模式.
- 在 `MainWindow` top navbar 新增「Set as desktop wallpaper」按鈕 (Settings 左側), 點擊 MUST 將當前選中國家的快取桌布套用為 Windows 桌面. 按鈕 MUST 僅在當前已有可顯示的桌布時可見.
- `CountryItem` 新增 `CachedImagePath` 屬性, 由 `MainViewModel.LoadCountryAsync` 在 cache hit / 新下載完成時填入, 提供按鈕命令所需的絕對檔案路徑.

## Capabilities

### New Capabilities
- `wallpaper-setter`: 提供將本地圖片檔案套用為 Windows 桌面的 service-level capability, 涵蓋輸入驗證, Win32 互通, 與 registry 樣式設定.

### Modified Capabilities
- `wallpaper-viewer-ui`: 新增 navbar 上的 Set-as-desktop 按鈕與對應 `SetAsDesktop` command, 並將 `CountryItem.CachedImagePath` 提升為 spec 層級的 observable behavior (供按鈕命令使用).

## Impact

- **新增程式碼**: `WallpaperApp/Services/IWallpaperSetterService.cs`, `WallpaperApp/Services/WallpaperSetterService.cs`, `WallpaperApp/Services/WallpaperSetterException.cs`, `WallpaperApp/Services/WallpaperSetterErrorKind.cs`.
- **修改程式碼**: `WallpaperApp/App.xaml.cs` (服務註冊), `WallpaperApp/ViewModels/CountryItem.cs` (新增 `CachedImagePath`), `WallpaperApp/ViewModels/MainViewModel.cs` (注入 service, 設定 path, 新增 `SetAsDesktopCommand`), `WallpaperApp/MainWindow.xaml` (新增 navbar 按鈕).
- **平台相依**: 此 capability 為 Windows-only, 透過 `user32.dll` 的 `SystemParametersInfo` 與 `HKEY_CURRENT_USER` registry. App 本身已是 WPF Windows app, 不增加新平台限制.
- **無 breaking change**: 既有 capability 行為不變, 既有 API surface 不變; `MainViewModel` 與 `CountryItem` 為新增屬性 / 參數 (`MainViewModel` ctor 多一個必要參數, 同時 parameterless ctor 仍可用於 XAML designer).
- **未涵蓋**: 多螢幕個別桌布, 鎖定畫面, 自動回復原桌布等延伸功能.
