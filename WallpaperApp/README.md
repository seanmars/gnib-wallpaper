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

- 啟動後自動載入美國 (US) 今日桌布
- 點上方國旗列任一國家即切換為該國今日桌布
- 已快取的國家會立即顯示, 未快取者會顯示載入動畫

## Cache 位置

`%LOCALAPPDATA%\WallpaperApp\cache\<country_code>\`

每國僅保留最新一張 jpg + 對應 metadata json. 新圖落地時自動刪除同國舊圖.

## 架構

- `Services/BingFetcher.cs` — HTTP + AngleSharp 解析 bingwallpaper.anerg.com
- `Services/WallpaperCache.cs` — 本地檔案系統 cache
- `ViewModels/MainViewModel.cs` — MVVM (CommunityToolkit.Mvvm), 狀態機 + 取消邏輯
- `Converters/` — XAML value converters (國旗 emoji, 狀態 visibility, 相等判斷)
- `MainWindow.xaml` — `DockPanel` 兩列版面 (國旗 row + 圖片區)
