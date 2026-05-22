## Context

`WallpaperApp/` 為一個 .NET 10 WPF 空殼專案 (僅含預設 MainWindow + `CommunityToolkit.Mvvm` 8.4.2). plan.md 已研究過 bingwallpaper.anerg.com 的 HTML 結構, 確認:

- Homepage 包含所有國家連結 `<a href="/{code}">`
- `/{code}` 頁面第一個 `/detail/{code}/{slug}` 即今日桌布
- Detail 頁含 4K (`w:3840`), 2K (`w:2560`), 1080p (`w:1920`) 的 imgproxy URL (含 hash 簽章, 不可手動拼湊)
- 圖片 alt 文字為 `<title> (© <author>/<source>)` 格式

plan.md 用 Playwright 是過度設計 — 該站靜態回應 HTML, 用 `HttpClient` + HTML parser 即可. 採純 C# 整合後, 不需額外 Node.js runtime, 單一 exe 即可發布.

CommunityToolkit.Mvvm 提供 `[ObservableProperty]` / `[RelayCommand]` source generators, 配合 MVVM 模式可大幅減少 boilerplate.

## Goals / Non-Goals

**Goals:**
- 啟動 < 2 秒看到 US 今日桌布 (cache hit 情況)
- 點國旗 < 200 ms 顯示已 cache 國家, 未 cache 則 < 5 秒抓取完成
- 圖片區域填滿可用空間, 國旗 row 不壓圖
- 抓取失敗有清楚錯誤訊息, 不 crash
- 單一 WPF exe 不依賴外部 runtime/Node

**Non-Goals:**
- 不做歷史桌布瀏覽 (僅今日)
- 不做自動設為桌面背景 (僅顯示)
- 不做使用者偏好持久化 (e.g. 記住上次選的國家) — 啟動恆為 US
- 不做平行抓取所有國家預熱 cache
- 不做 i18n (UI 文字維持英文/中文混合即可)

## Decisions

### Decision 1: HTML 解析庫選 AngleSharp 而非 HtmlAgilityPack

**選 AngleSharp**, 因為:
- 支援 CSS selectors (`document.QuerySelector("a[href^='/detail/']")`), 比 XPath/regex 直覺
- 持續維護, .NET 6+ 親和
- HtmlAgilityPack 雖更輕量但 selector 語法弱

替代方案: 純 regex (如 plan.md 風格). 拒絕理由: detail 頁解析有多個元素需互動 (alt 文字 + multiple URL), CSS selector 比 regex 易讀且易測.

### Decision 2: HttpClient 使用 `IHttpClientFactory` 模式 (但簡化為單例)

WPF 沒有內建 DI, 此專案規模也不需引入 `Microsoft.Extensions.DependencyInjection`. 直接建立一個 `static readonly HttpClient` 單例放在 `BingFetcher` 中, 設定 `User-Agent` header 與 30 秒 timeout.

替代方案: 每次 `using var http = new HttpClient()`. 拒絕理由: socket exhaustion 已是 .NET 反模式.

### Decision 3: Cache 目錄位於 `%LOCALAPPDATA%/WallpaperApp/cache/`

`Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` + `"WallpaperApp/cache"`. 每國一個子資料夾, 每國僅保留最新一張 (檔名含日期, 寫新檔後刪除同國其他舊檔).

判斷 "是否為今日已抓" 的條件: cache 內存在 `metadata.json` 且其 `fetched_date` 等於今日 (UTC date). 若是則直接讀本地, 否則重新抓.

替代方案:
- 用程式目錄 (`AppContext.BaseDirectory`): 拒絕, 應用程式目錄通常不可寫
- 用記憶體 cache: 拒絕, 重啟即失效, 不符合 "下載後 cache" 需求

### Decision 4: 國旗以 emoji 顯示 (region indicator symbols)

每個 ISO 3166-1 alpha-2 碼可用兩個 region indicator 字元組合成國旗 emoji (e.g. `us` → 🇺🇸). 用 `TextBlock` 大字級顯示即可, 不需打包 PNG/SVG 資源.

替代方案:
- 內嵌 PNG: 拒絕, 增加 binary 體積與資產管理
- 從 flagcdn.com 載入 SVG: 拒絕, 多一個外部依賴

**注意**: WPF 預設字型在 Windows 10/11 對國旗 emoji 渲染為彩色需用 `Segoe UI Emoji` font family. 若該字型不可用 fallback 顯示文字 code (e.g. "US").

### Decision 5: UI 狀態以單一 enum + ObservableProperty 表達

```csharp
public enum LoadState { Loading, Loaded, Error }
[ObservableProperty] private LoadState _state;
```

XAML 用 `BooleanToVisibilityConverter` + `Style` Triggers 切換 ProgressRing / Image / 錯誤訊息的可見性.

替代方案: 三個 bool. 拒絕理由: 三個 bool 可能彼此衝突 (e.g. Loading + Error 同時 true), enum 強制互斥.

### Decision 6: 抓取流程簡化為 2 個 HTTP 請求 (非 3 個)

plan.md 是 3 個請求 (homepage → country page → detail page). 國家清單可在首次啟動時抓一次並 cache 24 小時 (記憶體即可), 之後切換國家僅需 2 個請求: country page + detail page.

但初版實作可允許每次切換都打 country + detail 兩個請求 (~1 秒), 國家清單仍每次重抓也可接受. 列為日後優化點而非 v1 必做.

### Decision 7: 版面結構 — DockPanel 而非 Grid

```xml
<DockPanel>
  <Border DockPanel.Dock="Top">  <!-- 國旗 row, 高度自適 -->
    <ItemsControl ItemsSource="{Binding Countries}" .../>
  </Border>
  <Grid>  <!-- 填滿剩餘空間 -->
    <Image Source="{Binding CurrentImage}" Stretch="UniformToFill"/>
    <ProgressBar Visibility="{Binding IsLoading, Converter=...}"/>
  </Grid>
</DockPanel>
```

`DockPanel.Dock="Top"` 確保國旗 row 在圖片**外圍**而非疊加, 直接滿足 "不要壓在圖片上面" 的需求.

## Risks / Trade-offs

[**網站結構變更**] → CSS selector 失效時錯誤訊息明確, 使用者可在 issue 回報. 不寫複雜的 fallback selector, 直接 fail-fast.

[**imgproxy URL hash 過期**] → CDN URL 含簽章, 可能有時效. 若 cache 內 URL 已失效, 重抓 detail 頁取得新 URL. 此情境僅影響重試, 不影響首次抓取.

[**4K 圖片較大 (5-10 MB)**] → 首次抓取無 cache 時延遲可達 3-5 秒. Mitigation: 顯示明確 loading 動畫 + 進度文字 (e.g. "Downloading 4K wallpaper..."). 接受此延遲.

[**emoji 國旗在舊版 Windows 顯示異常**] → fallback 為文字 code. 不投入額外資源解決邊緣案例.

[**Segoe UI Emoji 字型不支援所有國旗**] → 已知支援 ISO 3166-1 兩字母全部組合. 不是風險, 是已知行為.

[**多執行緒競爭**] → 切換國家時若使用者快速連點, 可能同時觸發多個抓取. Mitigation: ViewModel 內保留 `CancellationTokenSource`, 新請求取消舊請求. RelayCommand 的 `CanExecute` 在 Loading 時 disable 國旗按鈕 (但允許切換到 cache 命中的國家).

[**Cache 寫入失敗 (磁碟滿)**] → 圖片可顯示但無 cache. 不應因 cache 失敗導致功能不可用. Try/catch 包覆 cache 寫入, log warn 即可.

## Migration Plan

無既有功能, 純新增. 部署即 `dotnet build && dotnet run`. 無 rollback 必要 (改回 MainWindow 空 Grid 即可).

## Open Questions

1. 是否需要 retry 機制? — 初版採單次失敗即顯示錯誤, 使用者手動點旗重試. 若實測失敗率高再加.
2. 國家順序? — 採網站首頁原始順序 (預期為字母序), 不額外排序. US 預設選中.
3. 圖片 stretch 模式? — `UniformToFill` 會裁切, `Uniform` 會留黑邊. 預設 `Uniform` (顯示完整桌布), 日後可加切換按鈕.
