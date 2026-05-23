## Context

`MainViewModel.SelectCountryAsync()` 目前以「單一 in-flight 載入」模型運作: 持有一個 `_activeCts`, 每次新呼叫先 `Cancel()` 舊的, 再開始新的. UI 端用單一 `LoadState State` 描述此唯一載入的 Loading / Loaded / Error. 啟動時 `InitializeAsync()` 解析國家清單後僅對 default 國家呼叫 `SelectCountryAsync()`, 其餘國家完全沒被觸碰, 直到使用者第一次點擊.

結果:
- 切換到未抓過的國家 → 必等網路.
- 切換中第二次切換 → 第一個國家的抓取被 cancel, cache 不會落地.
- 已開啟 app 半小時也沒人預熱其他國家的快取.

使用者希望: app 一打開, 11 個國家的桌布就開始下載; 同時若使用者已點到某國, 該國若已 ready 就立刻顯示, 否則顯示 spinner 等待**該國自己的**背景下載完成; 切換到別國 MUST NOT 拖累或取消正在背景跑的國家.

## Goals / Non-Goals

**Goals:**
- 啟動時 fan-out 對全部國家發起背景載入.
- 切換國家僅影響 UI 顯示的 `SelectedCountry`, 不影響其他國家的 in-flight 載入.
- 每國有自己的 `LoadState`, UI 同時可看到「哪些國家還在 loading」.
- 並行載入有 concurrency cap (預設 4), 避免被來源站 throttle.
- 重複觸發**同國**載入仍然 cancel 舊任務 (避免 race), 但這只影響該國.

**Non-Goals:**
- 不改 `BingFetcher` 對單國的 fetch/parse API (還是 4 步驟: DiscoverCountries → GetTodayDetailLink → FetchAndParseDetail → DownloadImageBytes).
- 不改 `WallpaperCache` 的目錄結構或 metadata schema.
- 不預抓「明日」桌布或多日歷史 (本次只關心今日載入並行化).
- 不在背景輪詢日期切換 (跨日重抓不在本次 scope).
- 不為每國配 retry / backoff (失敗即 error state, 使用者點擊重試, 與目前一致).

## Decisions

### 1. Per-country state, derived global state

新增 `CountryItem.LoadState` (Idle / Loading / Loaded / Error) 與 `CountryItem.ErrorMessage`. `MainViewModel.State` 與 `MainViewModel.ErrorMessage` 改為**鏡射** `SelectedCountry` 的對應 property — 當 `SelectedCountry` 改變或其 state property 改變時, ViewModel 透過 PropertyChanged 同步更新.

理由:
- XAML 已 binding `State` / `ErrorMessage`, 不必改 view 端 binding 路徑 (最小擾動).
- `CountryItem` 本身就是 UI 已 binding 的對象, 適合掛 per-country loading 顯示 (e.g. flag 上 spinner overlay).

**Alternative considered**: 直接讓 view binding 到 `SelectedCountry.LoadState` (移除 `MainViewModel.State`). 拒絕: 增加 binding 改動面與 null-safety 處理 (SelectedCountry 初期是 null).

### 2. Per-country cancellation, not global

把 `private CancellationTokenSource? _activeCts` 換成 `ConcurrentDictionary<string, CancellationTokenSource>` keyed by country code. `EnsureLoadStartedAsync(country)`:
1. 若該國 state 已 Loaded → 不做事.
2. 若該國已有進行中的 cts → 不重啟 (一個國家同時只一個).
3. 否則建立新 cts, 寫入 dict, 啟動 `LoadCountryAsync(country, ct)`.

`SelectCountryAsync(item)` 改名為 `SelectCountry(item)` 並只負責切換 `SelectedCountry`. 它**不再呼叫 cancel**, 只在「該國尚未 Loaded 且無 in-flight」時呼叫 `EnsureLoadStartedAsync()`. 切換到一個 loading 中的國家 → 只是顯示 spinner, 等同一個 task 完成.

理由: 滿足核心需求「切換 MUST NOT 取消其他國家背景載入」.

**Alternative considered**: 同國重複點擊也不取消舊任務. 拒絕: 邊緣 case 罕見, 但讓 cache 有可能寫入兩次, 不值得.

### 3. Concurrency cap via SemaphoreSlim

`MainViewModel` 持有 `SemaphoreSlim _httpGate = new(4, 4)`. `LoadCountryAsync()` 在進入網路階段 (`GetTodayDetailLinkAsync` 之前) `WaitAsync(ct)`, 退出時 `Release()`. **重要**: cache hit 路徑 MUST NOT 佔 semaphore, 否則 11 個快取命中還排隊毫無意義.

理由:
- 11 個國家一次發 ≥ 33 個 HTTP 請求 (1 detail link + 1 detail page + 1 image / 國) 對來源站不友善.
- 4 是經驗值, 接近瀏覽器同 host 並行上限.

**Alternative considered**: 用 `Parallel.ForEachAsync` + `MaxDegreeOfParallelism`. 拒絕: 我們的「啟動先 fan-out 全部 + 後續使用者點擊也加入排隊」是 long-lived 場景, 不是一批 work, semaphore 更直接.

### 4. Startup fan-out at end of InitializeAsync

`InitializeAsync()` 在解析 countries 與決定 default 之後:
- 設 `SelectedCountry = default` (純 UI, 同步).
- 對 `items` 中**每一國**呼叫 `EnsureLoadStartedAsync(c)` (含 default 自己), 全部不 await.

`InitializeAsync` 完成於「fan-out 已派發」, 不等待任何下載結束. UI 立即顯示國旗 row, default 國家對應的 spinner / cache image 由 binding 自然帶出.

### 5. UI thread safety

`CountryItem` 繼承 `ObservableObject`. 所有 state 變更 MUST 在 UI thread. `LoadCountryAsync` 內全程 `ConfigureAwait(true)` (沿用既有風格), 確保 PropertyChanged 在 UI thread fire. 不引入 `Dispatcher.Invoke`.

## Risks / Trade-offs

- **來源站 rate limit** → SemaphoreSlim cap = 4 + 既有 30s timeout. 若仍被擋, 後續可降到 2 或加 jitter.
- **記憶體**: 同時 in-flight 4 張下載 + 11 張 cache image (Freeze 後 immutable, GC 可回收非當前選中的). 1080p ~ 1 MB, 4K ~ 5 MB → 最壞 ~ 60 MB, 可接受.
- **同國重複切換的 cts 漏 dispose** → 在 `LoadCountryAsync` 的 `finally` 區塊內: 從 dict 移除自己 + Dispose. 用 `TryRemove(code, out var current); if (ReferenceEquals(current, cts)) ...` 防止覆蓋他人.
- **PropertyChanged 在背景 thread fire 觸發 XAML 例外** → `ConfigureAwait(true)` + 不在 `Task.Run` 內動 ViewModel state. 若 `BingFetcher` 內部把 continuation 切到 thread pool, 後續 await 回來時仍應在 SynchronizationContext (WPF) 上.
- **預設國家還在 loading 時 SelectedCountry 已被設** → OK, `State` 鏡射過去就是 Loading, 與目前行為等價.
- **「切換到 error 國家」要不要自動重試** → 不自動. `SelectCountry` 看到 state = Error 不動作; 由使用者再次點擊國旗時於 command handler 內手動 reset state 並重新 `EnsureLoadStartedAsync`. 此重試入口由 UI 既有的「點國旗 → Command」流程處理, 不另開 retry button.
