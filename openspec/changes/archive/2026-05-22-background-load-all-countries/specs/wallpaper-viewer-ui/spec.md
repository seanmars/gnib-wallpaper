## ADDED Requirements

### Requirement: 每國載入狀態獨立追蹤

`CountryItem` MUST 暴露 per-country 的 `LoadState` (Loading / Loaded / Error) 與對應的 `ErrorMessage`. 國旗 row 中的每個按鈕 MUST 能以視覺方式 (e.g. spinner overlay, error tint) 反映該國當下的 LoadState. 同一時刻多個國家 MUST 可同時處於 Loading 狀態.

#### Scenario: 啟動時所有國旗同時 loading

- **WHEN** 應用程式啟動完成且所有國家 cache 皆為空
- **THEN** 國旗 row 上的 11 個按鈕 MUST 全部呈現 Loading 視覺狀態
- **AND** 不需使用者操作即可逐一變為 Loaded 視覺狀態

#### Scenario: 部分國家已 cache

- **WHEN** 啟動時 jp 與 us 為今日 cache 命中, 其餘為 cache miss
- **THEN** jp 與 us 國旗按鈕 MUST 直接顯示為 Loaded 狀態 (無 spinner)
- **AND** 其餘 9 個按鈕 MUST 顯示 Loading 狀態直到各自完成

#### Scenario: 單一國家載入失敗

- **WHEN** fr 在背景載入時網路 timeout
- **THEN** fr 國旗按鈕 MUST 視覺上呈現 Error 狀態 (e.g. 紅色邊框或 icon)
- **AND** 其他國家的 LoadState MUST 不受影響

### Requirement: 啟動時對所有國家 fan-out 載入

應用程式啟動且 `BingFetcher.DiscoverCountriesAsync()` 完成後, 系統 MUST 對清單內**每一個**國家發起背景今日桌布載入. fan-out MUST 在 `MainViewModel.InitializeAsync` 內完成派發 (即所有 EnsureLoadStarted 呼叫已執行), MUST NOT 等待任何下載完成才回傳. 預設選中國家的判定邏輯不變, 但該國 MUST NOT 享有比其他國家更高的載入優先權.

#### Scenario: 11 國全部 fan-out

- **WHEN** fetcher 回傳 11 國且 `InitializeAsync` 即將回傳
- **THEN** 11 國 MUST 全部已透過 `EnsureLoadStartedAsync` (或等效機制) 派發背景載入任務
- **AND** `InitializeAsync` MUST 在 fan-out 派發後立即回傳, MUST NOT await 任一國家的網路階段

#### Scenario: 啟動後即可看到國旗 row

- **WHEN** `InitializeAsync` 回傳完畢且尚無任何國家完成載入
- **THEN** UI MUST 已顯示 11 個國旗按鈕 (即使 FlagImage 仍在載入)
- **AND** 預設國家對應的圖片區 MUST 顯示 Loading 動畫

## MODIFIED Requirements

### Requirement: 點擊國旗切換國家

點擊任一國旗 MUST 觸發顯示該國今日桌布. 系統 MUST 在 cache 命中時即時切換 (無 loading), 若該國背景載入仍在進行則顯示 loading 動畫直到該國自身完成. 切換國家 MUST NOT 取消任何**其他**國家正在進行的背景載入. 重複點擊**同一**國家 MAY 取消並重啟該國載入 (僅當該國 state = Error 時).

#### Scenario: 點擊已快取國家

- **WHEN** 使用者點擊 jp 國旗且 jp cache 為今日
- **THEN** 圖片區 MUST 在 < 200 ms 內切換為日本桌布
- **AND** loading 動畫 MUST NOT 出現

#### Scenario: 點擊背景載入中的國家

- **WHEN** 使用者點擊 fr 國旗且 fr 在背景仍 Loading
- **THEN** 圖片區 MUST 顯示 loading 動畫
- **AND** fr 背景載入 MUST 繼續, MUST NOT 被重啟
- **AND** fr 完成後圖片區 MUST 自動切換為法國桌布

#### Scenario: 切換中其他國家持續載入

- **WHEN** us 與 jp 與 de 同時在背景載入
- **AND** 使用者於 us 載入中點擊 jp
- **THEN** us 與 de 的背景載入 MUST 持續不被 cancel
- **AND** SelectedCountry MUST 變為 jp 且圖片區顯示 jp 對應的 LoadState (Loading / Loaded)

#### Scenario: 點擊 Error 狀態國家重試

- **WHEN** fr LoadState = Error 且使用者再次點擊 fr 國旗
- **THEN** fr LoadState MUST 重置為 Loading 並重新發起背景載入
- **AND** 其他國家的 in-flight 任務 MUST 不受影響

### Requirement: Loading 動畫

當 `SelectedCountry.LoadState = Loading` 時, MUST 在圖片區中央顯示載入動畫 (ProgressBar 或 ProgressRing). 動畫 MUST 在 `SelectedCountry.LoadState` 轉為 Loaded 或 Error 時消失. 動畫的顯示 MUST 僅取決於目前選中國家的 state, 與其他國家的 LoadState 無關.

#### Scenario: 首次啟動 (預設國家 cache miss)

- **WHEN** 應用程式啟動且預設國家 cache 為空
- **THEN** 主視窗顯示後 MUST 在圖片區立即顯示 loading 動畫
- **AND** 動畫 MUST 在預設國家自身載入完成時消失

#### Scenario: 切換到背景仍 Loading 的國家

- **WHEN** 使用者點擊一個 `LoadState = Loading` 的國家
- **THEN** 圖片區 MUST 立即顯示 loading 動畫 (無 HTTP 重新發起)

#### Scenario: 切換到已 Loaded 國家

- **WHEN** 使用者點擊一個 `LoadState = Loaded` 的國家
- **THEN** 圖片區 MUST 直接顯示該國桌布
- **AND** loading 動畫 MUST NOT 出現
