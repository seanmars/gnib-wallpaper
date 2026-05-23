## Why

目前 `MainViewModel` 一次只載入一個國家的桌布, 切換國家時會 cancel 前一個載入 (`_activeCts.Cancel()`), 切換後使用者必須等待新國家從零開始 fetch + download. 結果是 (1) 每次切換都要等網路, (2) 已快取以外的國家永遠不會被預熱, (3) 全部時段 UI 都被單一 `LoadState` 卡住. 使用者期望開啟 app 後就背景把所有國家拉好, 切換時要嘛瞬間命中 cache, 要嘛只需等對應國家自己的背景下載完成, 而切換動作本身 MUST NOT 中斷其他國家的背景載入.

## What Changes

- **BREAKING**: 啟動流程改為發現國家後立即 fan-out 同時觸發所有國家的背景載入, 而非僅載入 default 國家.
- **BREAKING**: 國家切換 MUST NOT cancel 其他國家進行中的背景載入. 原本的單一 `_activeCts` 改為「每國一個 task / cancellation」, 且僅在重複觸發**同國**載入時才取消舊任務.
- `CountryItem` 新增 per-country `LoadState` (Idle / Loading / Loaded / Error) 與 `ErrorMessage`, 讓國旗 row 可視覺化每國的載入進度.
- `MainViewModel.State` 改為**反映 `SelectedCountry` 的 per-country state** (而非全域 state), 確保切換到已 loaded 國家立即顯示桌布, 切換到 loading 中國家顯示 spinner, 切換到 error 國家顯示錯誤.
- 啟動時的 default 國家選擇邏輯保留, 但只負責「先選哪面國旗」, **不**特別優先載入該國 (所有國家平行啟動載入).
- 並行載入 MUST 設上限 (concurrency cap) 以避免一次發 11+ 個 HTTP 請求拖垮 Bing/anerg 站. 預設 cap = 4.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `wallpaper-viewer-ui`: 國旗 row 與圖片區的 loading/error 行為改為 per-country state 驅動; 切換國家 MUST NOT 取消其他國家的背景載入.
- `wallpaper-fetcher`: 新增「背景並行載入所有國家」與「並行 concurrency cap」需求 (fetcher 與 cache 的單國行為不變, 但 caller 對其的呼叫模式從 sequential 改為 parallel-with-cap).

## Impact

- `WallpaperApp/ViewModels/MainViewModel.cs`: 重寫載入流程; 移除單一 `_activeCts`, 改為 `Dictionary<string, CancellationTokenSource>` (或等效) 與 per-country task tracking; `State` 變為 derived property.
- `WallpaperApp/ViewModels/CountryItem.cs`: 新增 `LoadState`, `ErrorMessage`, 並讓 `MainViewModel.State` / `MainViewModel.ErrorMessage` 隨 `SelectedCountry` 同步.
- `WallpaperApp/Views/MainWindow.xaml`: 國旗按鈕 binding 新增 per-country loading 指示 (e.g. spinner overlay 或 dim flag).
- 不變動: `BingFetcher`, `WallpaperCache`, `FlagCache` 的 public API; `preferences.json` schema; cache 目錄結構.
- 風險: 並行 HTTP 可能觸發來源站 rate limit → 由 concurrency cap 控制; per-country state 的 PropertyChanged 必須在 UI thread → ViewModel 須用 `Dispatcher` 或 `ConfigureAwait(true)`.
