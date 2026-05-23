## 1. Per-country state on CountryItem

- [x] 1.1 在 `WallpaperApp/ViewModels/CountryItem.cs` 新增 `[ObservableProperty] LoadState _loadState = LoadState.Loading` (初始值用 Loading 因 fan-out 於 ctor 後立即觸發; 沿用既有 enum, 不新增 Idle)
- [x] 1.2 在 `CountryItem` 新增 `[ObservableProperty] string _errorMessage = ""`
- [x] 1.3 確認 `CountryItem` 仍為 `ObservableObject` (CommunityToolkit MVVM) 以便 PropertyChanged 自動觸發

## 2. MainViewModel: per-country task tracking

- [x] 2.1 移除 `private CancellationTokenSource? _activeCts;` 字段
- [x] 2.2 新增 `private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight = new(StringComparer.OrdinalIgnoreCase);`
- [x] 2.3 新增 `private readonly SemaphoreSlim _httpGate = new(initialCount: 4, maxCount: 4);`
- [x] 2.4 新增 `const int MaxConcurrentDownloads = 4;` 並用於 `_httpGate` 初始化

## 3. Split SelectCountry from LoadCountry

- [x] 3.1 將原本 `[RelayCommand] private async Task SelectCountryAsync(CountryItem item)` 重構: 拆出 `private async Task LoadCountryAsync(CountryItem item, CancellationToken ct)` 承載抓圖邏輯 (cache check → fetcher → cache save → bitmap)
- [x] 3.2 新增 `[RelayCommand] private void SelectCountry(CountryItem item)` 同步切換 `SelectedCountry`, 並於 `item.LoadState == Error` 或該國 cts 不存在於 `_inFlight` 時呼叫 `EnsureLoadStartedAsync(item)`. **MUST NOT** cancel `_inFlight` 中其他國家.
- [x] 3.3 新增 `private void EnsureLoadStartedAsync(CountryItem item)`: 若 `item.LoadState == Loaded` 直接 return; 若 `_inFlight.ContainsKey(item.Code)` 且 state == Loading 直接 return; 否則建 `CancellationTokenSource`, `_inFlight.TryAdd(item.Code, cts)`, fire-and-forget `LoadCountryAsync(item, cts.Token)`
- [x] 3.4 在 `LoadCountryAsync` 開頭設 `item.LoadState = LoadState.Loading; item.ErrorMessage = "";`
- [x] 3.5 在 `LoadCountryAsync` 內: cache hit 路徑 (`_cache.TryLoadTodayAsync`) MUST 在 `_httpGate.WaitAsync` **之前**, 命中即 `ApplyCached` + `item.LoadState = Loaded` + return
- [x] 3.6 在 `LoadCountryAsync` 進入網路階段前: `await _httpGate.WaitAsync(ct).ConfigureAwait(true);` 並用 `try / finally { _httpGate.Release(); }` 包住 fetcher 三步呼叫
- [x] 3.7 成功路徑結尾: `item.LoadState = LoadState.Loaded;` (注意: 若 item 是當前 `SelectedCountry` 才同步 `CurrentImage` / `CurrentWallpaper` / `WindowTitle`, 見 task 5)
- [x] 3.8 例外路徑: 捕捉 `OperationCanceledException` 視為靜默 (ct 是該國自己的, 被取消代表使用者明確重試 / 跨日 reset); 其他 exception → `item.LoadState = LoadState.Error; item.ErrorMessage = ex.Message;`
- [x] 3.9 `LoadCountryAsync` 的 `finally`: `if (_inFlight.TryGetValue(item.Code, out var current) && ReferenceEquals(current, cts)) { _inFlight.TryRemove(item.Code, out _); cts.Dispose(); }`

## 4. InitializeAsync: fan-out to all countries

- [x] 4.1 `InitializeAsync` 內在解析完 `items` 與決定 `defaultItem` 之後, 直接呼叫 `SelectCountry(defaultItem)` 設定 SelectedCountry (此呼叫會 enqueue defaultItem 的載入)
- [x] 4.2 緊接著對 `items` 中**每一個** CountryItem 呼叫 `EnsureLoadStartedAsync(c)` (對已經 enqueue 的 default 因 `_inFlight.ContainsKey` 而自然 no-op)
- [x] 4.3 `InitializeAsync` MUST 在 fan-out 派發後立即回傳, MUST NOT `await` 任何 LoadCountryAsync 任務
- [x] 4.4 移除 `InitializeAsync` 內原本對 default 國家的 `await SelectCountryAsync(defaultItem)` (改為非阻塞)

## 5. Mirror SelectedCountry state to MainViewModel

- [x] 5.1 在 `MainViewModel` 內訂閱 `SelectedCountry.PropertyChanged`: 當 `LoadState` / `ErrorMessage` 變化時, 更新 `MainViewModel.State` / `MainViewModel.ErrorMessage`
- [x] 5.2 `SetSelectedCountry(item)` (或 `OnSelectedCountryChanged` partial method) MUST 先 unsubscribe 舊 `SelectedCountry` 的 PropertyChanged, 再 subscribe 新的, 然後同步一次 `State` / `ErrorMessage` / `CurrentImage` / `CurrentWallpaper`
- [x] 5.3 `LoadCountryAsync` 在成功取得 image bytes 時: 若 `ReferenceEquals(SelectedCountry, item)`, 同步更新 `CurrentImage`, `CurrentWallpaper`, `WindowTitle`; 否則只設 `item.LoadState = Loaded` (背景國家不踩當前 view) — 透過 `CountryItem.CachedImage/CachedWallpaper` + SelectedCountry PropertyChanged subscription 自動達成
- [x] 5.4 切換到一個已 Loaded 國家時: `SelectCountry` 從 cache 重建 `CurrentImage` (或保留每國的 `BitmapImage` 於 `CountryItem.CachedBitmap` 避免重讀檔) — 用 `CountryItem.CachedImage` 保留 BitmapImage

## 6. Per-country error retry

- [x] 6.1 `SelectCountry`: 若 `item.LoadState == Error`, MUST 直接呼叫 `EnsureLoadStartedAsync(item)` 重新發起; `LoadCountryAsync` 開頭會將 LoadState 設為 Loading (Error → Loading 直接過渡, 不經 Idle)
- [x] 6.2 重試 MUST 透過 `_inFlight` 的 add/remove 流程, 避免兩個 fr 任務並行

## 7. XAML: per-country visual state

- [x] 7.1 `WallpaperApp/MainWindow.xaml` 國旗按鈕新增 binding: 依 `CountryItem.LoadState` 顯示 spinner overlay (Loading) / red border (Error) / normal (Loaded)
- [x] 7.2 可用既有 `LoadStateToVisibilityConverter` 或新增專屬 converter, 不必為此重寫 Converters 架構
- [x] 7.3 圖片區 loading 動畫保留 binding 至 `MainViewModel.State` (透過 task 5 已 mirror 自 `SelectedCountry.LoadState`)

## 8. Verification

- [x] 8.1 手動驗收: 啟動 app 在空 cache 狀態下, 應看到 11 國旗全部呈 loading, 4 個一批進入網路階段 (可用 fiddler/devtools 觀察, 或加暫時 log)
- [x] 8.2 手動驗收: us 載入中點擊 jp, 觀察 us 載入完成後 us 國旗仍變 Loaded (證明未被 cancel)
- [x] 8.3 手動驗收: 關閉網路啟動 app, 11 國全部進 Error, 國旗顯示 error 視覺
- [x] 8.4 手動驗收: 點擊 Error 國家, 該國旗回到 Loading 並重發 (其他 Error 國家不受影響)
- [x] 8.5 `dotnet build WallpaperApp/WallpaperApp.csproj` MUST 無 warning / error
- [x] 8.6 確認 `WallpaperApp/Models/`, `WallpaperApp/Services/` (BingFetcher, WallpaperCache, FlagCache) 完全未變動 (此次 scope 限於 ViewModel + View)
