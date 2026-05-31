## Context

`WallpaperRefreshScheduler` 是純後端服務, 以 `PeriodicTimer` 週期觸發 refresh. 現況:

- 每個 tick 呼叫 `RefreshAsync(null)`, 經 `ResolveCountriesAsync` fallback 為**單一** default country (或硬 fallback `"us"`), 因此只有一國 cache 會被更新.
- `RefreshOneAsync` 內有 **slug gate**: 比對 fetcher 當前 slug 與 cache metadata 的 slug, 相同則 no-op 不下載.
- 桌布只能由 UI 的手動 `SetAsDesktop` command 套用 (`IWallpaperSetterService.SetWallpaperFromFile`); 計時器路徑**從不**主動套用桌布.
- `DiscoverCountriesAsync` 目前回傳硬編碼國家清單 (無網路、無快取), 11 國.

使用者已確認: (1) 以 `UserPreferences.DefaultCountryCode` 作為決定桌布的國家; (2) hash 變化時自動套用桌布.

## Goals / Non-Goals

**Goals:**
- 計時器 tick 更新**所有** discovered countries 的 cache (slug gate 保留, 只下載真的換圖的國家).
- tick 結束後, 對 default country 當前 cache 圖片計算 hash, 與「上次套用桌布的 hash」比較; 不同才套用桌布並記錄新 hash, 相同則不動桌布.
- 切換 default country 時也能正確重新套用桌布.

**Non-Goals:**
- 不改動 UI 手動 `SetAsDesktop` 行為.
- 不為每一國都套用桌布 (桌布只反映 default country).
- 不改 `DiscoverCountriesAsync` 的國家來源 (仍用既有硬編碼清單).
- 不引入新的第三方依賴 (hash 用 .NET 內建 `SHA256`).

## Decisions

### Decision 1: 兩個 gate 分屬不同層, 桌布 reconcile 獨立於 slug gate

slug gate 回答「這國要不要重新下載?」(cache 關注, 全部國家); hash gate 回答「要不要動桌布?」(桌布關注, 僅 default country). 兩者多數時候一致 (slug 變 -> bytes 變), 若把桌布套用塞在 slug 的 early-return **之後**, hash 幾乎永遠不會觸發 -> 形同 dead code.

因此每個 tick 拆成兩個獨立步驟:
1. **Refresh all caches** (slug-gated, 需求 1): 對所有國家跑既有 `RefreshOneAsync`.
2. **Reconcile desktop** (hash-gated, 需求 2/3): 不論步驟 1 是否更新了 default country, 都讀取 default country 當前 cache 圖片 -> 算 hash -> 比對 last-applied hash -> 不同才套用.

步驟 2 每個 tick 都跑 (而非只在 slug 變時跑), 這樣才能涵蓋「切換 default country」與「啟動後桌布尚未對齊」的情況.

**Alternative considered**: 只在 default country slug 變化時套用桌布. 較省事但無法處理切換 default country 的情況, 且讓 hash 變得多餘 (slug 已能判斷). 否決.

### Decision 2: 桌布自動套用由 scheduler 擁有 (注入 `IWallpaperSetterService`)

scheduler 是背景關注點, 已持有 bytes / `saved.ImagePath` / 知道 default code, 而 `SystemParametersInfo` 與執行緒無關, 在背景執行緒呼叫安全. 把套用放在 `MainViewModel.ApplyRefresh` 會 (a) 讓背景 side-effect 耦合到 UI 層, (b) 只在 slug 變的 tick 才觸發 (與 Decision 1 衝突). 因此在 scheduler 建構子注入 `IWallpaperSetterService`.

**Alternative considered**: 透過 `WallpaperRefreshed` 事件讓 UI 套用. 否決, 同上耦合與時機問題.

### Decision 3: 持久化「單一 last-applied-desktop hash」, 非 per-country

若把 hash 存在每國 `CachedMetadata`, 當 default 從 `us` 切到 `jp`, jp 自身 metadata 的 hash 沒變 -> 桌布不會更新 -> 桌布卡在 us 的圖. 改用**單一** last-applied hash (一個值), 等同於「比對 default country 圖片 vs 桌布上實際的圖」, 切換國家時 jp 圖 hash != last-applied hash -> 正確套用.

儲存位置: 新增獨立 state 檔 `HomeDir.GetPath("desktop-state.json")`, 內含 `appliedImageHash` 與 (選用) `appliedCountryCode`. **不**放進 `UserPreferences`, 因為:
- `UserPreferencesService.SaveAsync` 會觸發 `PreferencesChanged`, scheduler 訂閱該事件做啟停判斷; 雖然 hash 變更不會改 interval/enabled (不致重啟), 但語意上把高頻變動的 runtime state 混入使用者偏好不乾淨.
- 獨立 state 檔職責單一, 易於測試與重置.

hash 演算法: `SHA256` over image bytes, 輸出 lowercase hex string. 影像通常數 MB, 每 tick 一次, 成本可忽略.

**Alternative considered**: per-metadata hash (否決, 切換國家失效); 存進 preferences (否決, 語意混淆).

### Decision 4: tick 開頭 discover 一次, 串到各國 refresh

`RefreshOneAsync` 內 `ResolveCountryAsync` 會 per-country 呼叫 `DiscoverCountriesAsync`. 雖然現況該方法是硬編碼清單 (無網路成本), 改為全部國家後仍會每國重複建一份 list. tick 開頭 discover 一次、把 `Country` 物件串給各國 refresh, 較乾淨且為未來 `DiscoverCountriesAsync` 改成網路來源預留效率. 此為效率優化, 非正確性需求.

## Risks / Trade-offs

- [全部國家 refresh 增加每 tick 工作量] → slug gate 維持, 只有真的換圖的國家才下載; 並行上限 (`SemaphoreSlim` ≤ 2) 維持, 控制同時網路 IO; 失敗隔離維持, 單國失敗不影響其他國.
- [背景執行緒套用桌布的執行緒安全] → `SystemParametersInfo` 為 Win32 thread-agnostic call, `WallpaperSetterService` 無共享可變狀態, 背景呼叫安全; 套用失敗 (`WallpaperSetterException`) MUST catch + log, 不可中斷排程, 且不可寫入新 hash (下次 tick 會重試).
- [state 檔損毀或遺失] → 讀取失敗時視為「無 last-applied hash」, 下次 tick 會重新套用一次桌布 (冪等, 至多多套一次), 不拋例外.
- [default country 當天無桌布 / cache 為空] → 桌布 reconcile 找不到 cache 圖片時為 no-op, 不動桌布、不寫 hash.
