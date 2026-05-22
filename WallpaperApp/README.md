# WallpaperApp

WPF 桌面應用程式, 顯示 Bing 各國今日桌布 (UHD 4K).

## 需求

- Windows 10/11
- .NET 10 SDK
- 網路連線 (首次抓取)

## 執行

```powershell
dotnet run --project WallpaperApp
```

或於 Visual Studio / Rider 開啟 `gnib.slnx` 直接執行 `WallpaperApp`.

## 操作

- 啟動後自動載入預設國家今日桌布. 預設國家為使用者於 Settings 設定的值, 若未設定則 fallback 至美國 (US).
- 點上方國旗列任一國家即切換為該國今日桌布.
- 已快取的國家會立即顯示, 未快取者會顯示載入動畫.

## Top navbar 與 Settings

- 主視窗最上方為 top navbar, 右側 gear 按鈕為 Settings 入口.
- 點 gear 開啟 Settings 視窗 (modal). 第一個區段為 "Default country": 選擇要在下次啟動時自動載入的國家.
- 選擇 default country 後設定立即持久化, 但 **不** 立刻切換目前顯示的桌布, 僅影響下次啟動.

## System tray (背景常駐)

- 啟動後在通知區域顯示 tray icon, 程式整個生命週期常駐.
- 點擊主視窗右上角 X 會彈出對話框: `Minimize to tray` 或 `Exit application`, 可勾選 `Remember my choice` 記住偏好.
- 點擊 minimize 按鈕一律隱藏至 tray, 不再保留 taskbar entry.
- Tray icon 單擊或雙擊還原主視窗.
- Tray icon 右鍵選單: `Show / Hide` / `Reset close preference` / `Exit`. 已記住偏好後可由 `Reset close preference` 清除以重新出現對話框.

## 偏好與 Cache 位置

- 桌布 cache: `%LOCALAPPDATA%\WallpaperApp\cache\<country_code>\`. 每國僅保留最新一張 jpg + 對應 metadata json.
- 使用者偏好: `%LOCALAPPDATA%\WallpaperApp\preferences.json`. JSON 欄位:
  - `closeAction`: `"MinimizeToTray" | "Exit" | null` — 點主視窗 X 時的記憶行為.
  - `defaultCountryCode`: 2 字母小寫國家代碼 (e.g. `"jp"`) 或 `null` — 啟動時要預設載入的國家.
  - 反序列化為 case-insensitive, 舊版 PascalCase 欄位 (e.g. `"CloseAction"`) 仍可正常載入.
- 檔案損毀或缺欄位視為偏好不存在 (不會 crash).

## 架構

- `Services/BingFetcher.cs` — HTTP + AngleSharp 解析 bingwallpaper.anerg.com
- `Services/WallpaperCache.cs` — 本地檔案系統 cache
- `Services/UserPreferencesService.cs` — 關閉偏好持久化 (write-temp-then-replace)
- `Services/WindowCloseService.cs` — 主視窗 close / minimize / restore / exit 統一入口
- `ViewModels/MainViewModel.cs` — MVVM (CommunityToolkit.Mvvm), 狀態機 + 取消邏輯
- `ViewModels/TrayIconViewModel.cs` — tray 選單 commands
- `ViewModels/CloseConfirmViewModel.cs` — 關閉對話框 state
- `Views/CloseConfirmDialog.xaml` — modal 關閉確認對話框
- `Converters/` — XAML value converters (國旗 emoji, 狀態 visibility, 相等判斷)
- `MainWindow.xaml` — `DockPanel` 兩列版面 (國旗 row + 圖片區)
- `App.xaml.cs` — composition root (`ShutdownMode=OnExplicitShutdown`, tray icon 生命週期)
