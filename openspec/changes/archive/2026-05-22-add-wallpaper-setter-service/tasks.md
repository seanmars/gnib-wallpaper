## 1. Exception types

- [x] 1.1 在 `WallpaperApp/Services/` 新增 `WallpaperSetterErrorKind.cs`, 定義 enum 值: `FileNotFound`, `UnsupportedFormat`, `Win32CallFailed`, `RegistryWriteFailed`.
- [x] 1.2 在 `WallpaperApp/Services/` 新增 `WallpaperSetterException.cs` (sealed), constructor 接收 `Kind`, `message`, 可選 `Exception? inner`. 提供 `Kind` public property.

## 2. Service 介面

- [x] 2.1 在 `WallpaperApp/Services/` 新增 `IWallpaperSetterService.cs`, 宣告 `void SetWallpaperFromFile(string imagePath);`.

## 3. Win32 互通與實作

- [x] 3.1 在 `WallpaperApp/Services/` 新增 `WallpaperSetterService.cs` (sealed class, 實作 `IWallpaperSetterService`).
- [x] 3.2 在 `WallpaperSetterService` 內加入 `LibraryImport` (或 `DllImport`) 宣告: `user32.dll!SystemParametersInfoW`, 帶 `[MarshalAs(UnmanagedType.LPWStr)]` 字串參數與 `SetLastError = true`.
- [x] 3.3 定義常數: `SPI_SETDESKWALLPAPER = 0x0014`, `SPIF_UPDATEINIFILE = 0x01`, `SPIF_SENDCHANGE = 0x02`.
- [x] 3.4 實作 `SetWallpaperFromFile`:
  - 用 `string.IsNullOrWhiteSpace` 檢查並拋 `ArgumentException` (僅 null / 空字串時, 屬於程式錯誤而非 typed kind).
  - 用 `Path.GetExtension(imagePath).ToLowerInvariant()` 驗證副檔名為 `.bmp` / `.jpg` / `.jpeg` / `.png`, 否則拋 `WallpaperSetterException(UnsupportedFormat, ...)`.
  - 用 `Path.GetFullPath` 標準化, 接著 `File.Exists` 驗證, 否則拋 `WallpaperSetterException(FileNotFound, ...)`.
- [x] 3.5 實作 registry 寫入:
  - 用 `Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", writable: true)`, 寫 `WallpaperStyle = "10"`, `TileWallpaper = "0"` (REG_SZ).
  - try/catch `UnauthorizedAccessException`, `SecurityException`, `IOException` → 重新包裝為 `WallpaperSetterException(RegistryWriteFailed, ..., inner)`.
  - registry 失敗時 MUST NOT 繼續呼叫 `SystemParametersInfo`.
- [x] 3.6 實作 Win32 呼叫:
  - 呼叫前 `Marshal.SetLastSystemError(0)`.
  - 呼叫 `SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, fullPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)`.
  - 回傳 `false` 時用 `Marshal.GetLastWin32Error()` 取得 err code, 拋 `WallpaperSetterException(Win32CallFailed, $"SystemParametersInfo failed (Win32 error {err})")`.

## 4. App composition root 整合

- [x] 4.1 在 `WallpaperApp/App.xaml.cs` 加入 public property `IWallpaperSetterService WallpaperSetter { get; private set; } = default!;` (對齊 `Preferences`, `WindowClose` 慣例).
- [x] 4.2 在 `OnStartup` 內 (其他 service 構造之後) 加入 `WallpaperSetter = new WallpaperSetterService();`.

## 5. 編譯與冒煙驗證

- [x] 5.1 `dotnet build WallpaperApp` 確認專案編譯通過 (無 warning 增量).
- [x] 5.2 ~~在開發機跑一次 ad-hoc 測試: 在 debug 模式於 `MainWindow` ctor 暫時加 `App.CurrentApp.WallpaperSetter.SetWallpaperFromFile(@"C:\path\to\sample.jpg");`...~~ **取代為**: 透過 task 7 新增的 navbar 按鈕進行真實端到端驗證, 不再需要臨時改 ctor.
- [x] 5.3 ~~重複 5.2 但傳入不存在路徑 → 應拋 `WallpaperSetterException(FileNotFound)`...~~ **取代為**: 透過刪除快取檔案 / 手動將 `CachedImagePath` 指向不存在路徑可在按鈕點擊時觀察到 `MessageBox` 錯誤回饋 (見 task 7 對應 spec scenario).

## 6. OpenSpec 收尾

- [x] 6.1 全部 tasks 完成後, 跑 `openspec status --change "add-wallpaper-setter-service"` 確認 `isComplete: true`.
- [ ] 6.2 透過 `/opsx:archive` (或 `openspec archive ...`) 將此 change 歸檔, 同時更新 `openspec/specs/wallpaper-setter/spec.md` 為正式 spec.

## 7. UI integration (follow-up, 同 change 內完成)

- [x] 7.1 `WallpaperApp/ViewModels/CountryItem.cs` 新增 `[ObservableProperty] private string? _cachedImagePath;` 對應的 `CachedImagePath` 屬性.
- [x] 7.2 `WallpaperApp/ViewModels/MainViewModel.cs`:
  - 注入 `IWallpaperSetterService` (新增 5-arg ctor; parameterless ctor 透傳 `new WallpaperSetterService()` 供 XAML designer).
  - `LoadCountryAsync` 在 cache hit 與 fresh save 兩條路徑寫入 `CachedImagePath` (透過 `_cache.SaveAsync` 的回傳 `CachedMetadata.ImagePath`).
  - 新增 `[RelayCommand] SetAsDesktop()`, 拋出 `WallpaperSetterException` 時以 `MessageBox` (icon=Warning, title="Set Wallpaper") 顯示錯誤.
- [x] 7.3 `WallpaperApp/App.xaml.cs` 將 `WallpaperSetter` 傳入 `MainViewModel` ctor.
- [x] 7.4 `WallpaperApp/MainWindow.xaml` 在 top navbar 加 monitor icon 按鈕 (Settings 左側), 套用既有 32x32 按鈕樣式, `Visibility` 綁定 `CurrentImage` 經 `NullToVisibilityConverter`, `Command` 綁定 `SetAsDesktopCommand`.
- [x] 7.5 `openspec/changes/add-wallpaper-setter-service/proposal.md` 在 Modified Capabilities 加入 `wallpaper-viewer-ui`.
- [x] 7.6 新增 `openspec/changes/add-wallpaper-setter-service/specs/wallpaper-viewer-ui/spec.md` 含 ADDED Requirements (navbar 按鈕可見性, 按鈕觸發 service, CountryItem 追蹤路徑).
- [x] 7.7 重新跑 `dotnet build WallpaperApp` 確認 0 warning / 0 error.
