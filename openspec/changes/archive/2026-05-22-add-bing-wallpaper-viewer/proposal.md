## Why

使用者希望每天都能看到 Bing 各國今日桌布 (UHD 4K), 並能用滑鼠在不同國家之間快速切換. 現有的 `plan.md` 設計了 Node.js 抓取器, 但 `WallpaperApp/` 是 WPF (.NET 10) 專案, 兩者無法直接共用程式碼. 需要一個整合在 WPF 內的方案: 啟動即看到美國 (US) 今日桌布, 點上方國旗列即可秒切其他國家, 圖片自動快取避免重複下載.

## What Changes

- 在 `WallpaperApp/` 內以純 C# 實作 Bing 桌布抓取邏輯 (用 `HttpClient` + `AngleSharp` 取代 plan.md 的 Playwright)
- 加入國家發現 (homepage parse) + 今日 detail 連結擷取 + 4K URL 解析三個 service
- MainWindow 改為兩列版面: 上排國旗 row (圖片外圍, 不壓圖), 下方填滿 4K 桌布圖
- 加入載入動畫 (ProgressRing / spinner) 顯示於圖片區域中央, 抓取期間覆蓋
- 加入本地檔案系統 cache: `%LOCALAPPDATA%/WallpaperApp/cache/<country_code>/` 內每國僅保留最新一張 jpg + metadata json, 舊圖在新圖落地後刪除
- 預設啟動國家為 `us`, 切換國家後若 cache 為今日則直接顯示, 否則重新抓取
- 加入 ViewModel (CommunityToolkit.Mvvm 已內建) 處理狀態: `Loading`, `Loaded`, `Error`

## Capabilities

### New Capabilities

- `wallpaper-fetcher`: 從 bingwallpaper.anerg.com 探索國家清單、解析今日 detail 頁、取得 4K/2K/HD 下載 URL 與 metadata 的純 C# 抓取邏輯
- `wallpaper-cache`: 將圖片與 metadata 寫入本地檔案系統並依國家保留最新一張的快取機制
- `wallpaper-viewer-ui`: WPF MainWindow 的兩列版面 (國旗 row + 圖片區), 含載入動畫、錯誤狀態、預設 US、點旗切換行為

### Modified Capabilities

(無, 此為新專案首次加入功能)

## Impact

- **新增 NuGet 套件**: `AngleSharp` (HTML 解析)
- **修改檔案**: `WallpaperApp/MainWindow.xaml`, `MainWindow.xaml.cs`, `WallpaperApp.csproj`
- **新增程式碼**: `Services/` (BingFetcher, WallpaperCache), `ViewModels/MainViewModel`, `Models/` (Country, Wallpaper), `Converters/` (BoolToVisibility 等), 國旗資源 (內嵌或從網路載入)
- **外部依賴**: 需網路連線到 `bingwallpaper.anerg.com` 與其 imgproxy CDN
- **檔案系統**: 寫入 `%LOCALAPPDATA%/WallpaperApp/cache/`, 每國最多保留 2 個檔案 (jpg + json)
- **非影響**: plan.md 內的 Node.js scraper 設計不會被實作, 但保留作為網站結構備忘
