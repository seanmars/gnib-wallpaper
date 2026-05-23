## Context

WallpaperApp 是一個 WPF (.NET, Windows-only) 桌面 app, 主要結構為 `App.xaml.cs` 於 `OnStartup` 內手動建構 services 並注入 ViewModel (見 `App.xaml.cs:25-40`). 目前 services 命名空間為 `WallpaperApp.Services`, 涵蓋 `BingFetcher`, `WallpaperCache`, `FlagCache`, `UserPreferencesService`, `WindowCloseService` 等. 沒有引入 DI container, 服務都是 stateless 或單例形式直接 `new`. 既有 service 慣例:

- 介面命名為 `I{Name}Service` (例外: `BingFetcher` 是具象 class, 因為 stateless 且無多型需求).
- 拋出 `FetcherException` 來統一 fetcher 領域錯誤 (`Services/FetcherException.cs`).
- 例外被 ViewModel 捕捉並轉成 `LoadState.Error` + `ErrorMessage`.

此 change 加入新的 service 層能力 ("設定圖片為 Windows 桌布"), 但**不**修改任何 ViewModel 或 view, 也**不**加入 UI 入口. 後續 change 將決定要在哪個 UI 表面 (主視窗按鈕 / tray 選單 / settings) 接這個 service.

## Goals / Non-Goals

**Goals:**
- 提供單一同步 (或可選 async) 方法 `SetWallpaperFromFile(string path)`, 將指定的本地圖片設為當前使用者的 Windows 桌布.
- 強制套用 Fill style (registry `WallpaperStyle = 10`, `TileWallpaper = 0`).
- 對輸入做嚴格驗證: 路徑必須存在; 副檔名必須為 `.bmp`, `.jpg`, `.jpeg`, `.png` (大小寫不敏感).
- 失敗時拋出明確的 typed exception, 讓 caller 能分辨 "檔案不存在 / 格式不支援 / Win32 呼叫失敗".
- 與既有 service 風格一致: 介面 + 具象實作 + `App.OnStartup` 手動構造注入.

**Non-Goals:**
- 不轉換圖片格式 (例如 webp -> jpg). 使用者選擇了 "回報錯誤, 不做轉檔".
- 不支援多螢幕個別桌布 (`IDesktopWallpaper` COM API 雖然可以做到, 但增加複雜度, 留給後續 change).
- 不處理鎖定畫面 (lock screen) 桌布.
- 不暴露其他 wallpaper style (Fit/Stretch/Tile/Center/Span). 固定為 Fill.
- 不做 UI 整合. 沒有按鈕 / 命令 / ViewModel 變更.
- 不做 unit test (本專案目前無 test project, 與既有慣例一致).

## Decisions

### Decision 1: 使用 `SystemParametersInfo` (P/Invoke) 而非 `IDesktopWallpaper` COM API

**Choice**: P/Invoke `user32.dll!SystemParametersInfoW` with `SPI_SETDESKWALLPAPER (0x0014)`.

**Rationale**:
- 單螢幕 / 全螢幕同一張桌布的場景下, `SystemParametersInfo` 是最短路徑, 從 Windows 95 起就穩定.
- `IDesktopWallpaper` COM API (Win8+) 主要優勢是多螢幕個別設定與 slideshow, 已被 Non-Goals 排除.
- P/Invoke 不需額外 NuGet 套件, 不需 COM interop boilerplate.

**Alternatives considered**:
- `IDesktopWallpaper` via `CoCreateInstance(CLSID_DesktopWallpaper)`: 否決 (overkill).
- 直接寫 registry 然後等 explorer 重新整理: 否決 (需要使用者登出或手動 refresh, 體驗差).

### Decision 2: Wallpaper style 寫入 registry, 不寫過渡 path

**Choice**: 在呼叫 `SystemParametersInfo` 之前, 寫 `HKEY_CURRENT_USER\Control Panel\Desktop`:
- `WallpaperStyle` (REG_SZ) = `"10"` (Fill)
- `TileWallpaper` (REG_SZ) = `"0"`

**Rationale**:
- `SystemParametersInfo(SPI_SETDESKWALLPAPER)` 本身不接受 style 參數. style 由 registry 決定, OS 在套用時讀取.
- 順序: 先寫 registry -> 再呼叫 `SystemParametersInfo`, 這樣 OS 在 broadcast `WM_SETTINGCHANGE` 時用的是新 style.
- 用 `Microsoft.Win32.Registry.CurrentUser.OpenSubKey(..., writable: true)`, 失敗時 fallback (見 Risk).

**Alternatives considered**:
- 不寫 registry, 沿用系統當前 style: 已在 user 選擇中被否決.
- 用 `RegistryView.Registry64` 顯式指定: 否決 (`HKEY_CURRENT_USER` 不分 64/32 view).

### Decision 3: SPI flags 採用 `SPIF_UPDATEINIFILE | SPIF_SENDCHANGE`

**Choice**: 第四個參數 `fuWinIni` 傳入 `0x01 | 0x02 = 0x03`.

**Rationale**:
- `SPIF_UPDATEINIFILE (0x01)`: 將設定寫回使用者設定 (持久化).
- `SPIF_SENDCHANGE (0x02)`: 廣播 `WM_SETTINGCHANGE`, 讓其他 process (e.g. explorer.exe) 即時刷新.
- 兩者皆缺 -> 重啟後復原, 或要手動 refresh.

### Decision 4: API surface 為同步方法 + typed exception

**Choice**:
```csharp
public interface IWallpaperSetterService
{
    void SetWallpaperFromFile(string imagePath);
}

public sealed class WallpaperSetterException : Exception
{
    public WallpaperSetterErrorKind Kind { get; }
    public WallpaperSetterException(WallpaperSetterErrorKind kind, string message, Exception? inner = null) ...
}

public enum WallpaperSetterErrorKind
{
    FileNotFound,
    UnsupportedFormat,
    Win32CallFailed,
    RegistryWriteFailed,
}
```

**Rationale**:
- `SystemParametersInfo` 與 registry write 都是 fast local I/O, 沒有實際 async 受益. 同步介面更直觀.
- 即使內部會被 ViewModel call site 包進 `Task.Run`, 介面保持同步; caller 自行決定要不要丟到 background thread.
- 用 typed `Kind` enum 讓 UI 之後可以針對不同類型錯誤呈現不同訊息 (例如 "格式不支援" vs "Win32 失敗" 給不同提示).

**Alternatives considered**:
- 回傳 `Task<bool>` + log: 否決 (吞 exception, 違反 fail-fast 風格).
- 用單一 `FetcherException`: 否決 (語意不符, 這不是 fetch).

### Decision 5: 副檔名白名單驗證 (case-insensitive)

**Choice**: 接受副檔名 `.bmp`, `.jpg`, `.jpeg`, `.png` (大小寫不敏感). 其他副檔名拋 `UnsupportedFormat`.

**Rationale**:
- 這四種是 `SystemParametersInfo(SPI_SETDESKWALLPAPER)` 在現代 Windows (Win8+) 直接支援的格式.
- 不做檔頭 (magic bytes) 驗證, 只看副檔名: 一是 OS 也是看副檔名載圖; 二是若副檔名與內容不符那是上層 caller 的責任.
- App 既有桌布來源 `WallpaperCache` 已是 JPG, 因此實際使用路徑會 100% 通過驗證.

### Decision 6: DI 註冊位置

**Choice**: 在 `App.xaml.cs::OnStartup` 增一行 `WallpaperSetter = new WallpaperSetterService();`, 以 `IWallpaperSetterService` 暴露為 `App.WallpaperSetter` property (對齊 `Preferences`, `WindowClose`).

**Rationale**:
- 專案沒有 DI container, 此模式與 `Preferences`, `WindowClose` 完全一致.
- 後續 ViewModel 或新 service 可透過 `App.CurrentApp.WallpaperSetter` 取得實例; 或 constructor 注入.

## Risks / Trade-offs

- **[Risk] `SystemParametersInfo` 在某些罕見情境回傳 false 但無 Win32 error code** → Mitigation: 呼叫前 `Marshal.SetLastSystemError(0)`, 呼叫後若回傳 false 則用 `Marshal.GetLastWin32Error()` 包進 `WallpaperSetterException(Kind = Win32CallFailed)`. error code = 0 時用 generic message.

- **[Risk] Registry write 在企業環境被 group policy 鎖** → Mitigation: 將 registry write 包進 try/catch, 失敗時拋 `WallpaperSetterException(Kind = RegistryWriteFailed)`. 不嘗試以 admin 提權繼續.

- **[Risk] 使用者傳入相對路徑 / UNC 路徑** → Mitigation: 在驗證階段 `Path.GetFullPath(imagePath)` 標準化, `SystemParametersInfo` 必須吃絕對路徑.

- **[Risk] 使用者傳入路徑指向被刪除的暫存檔, OS 套用後桌布變黑** → Mitigation: 不負責生命週期, 但在驗證階段確認 `File.Exists`. caller 自行確保檔案在 SPI 呼叫期間存在. (Bing wallpaper 路徑來自 `WallpaperCache`, 不會被外部刪除.)

- **[Trade-off] 同步 API vs async** → 同步介面簡單清楚, 但若 UI thread 直接呼叫且 SPI 異常慢, 可能短暫卡 UI. 接受此 trade-off; ViewModel 整合時 (後續 change) 可用 `Task.Run` 包裝.

- **[Trade-off] 固定 Fill style** → 喪失 Tile / Fit 等選項. 對齊本次 user 決策; 未來若加 `WallpaperStyle` 參數是 non-breaking 擴充.

## Migration Plan

無資料 / API 遷移需求. 純粹新增 service. Rollout:

1. 加入 service + 介面 + exception types.
2. `App.xaml.cs` 增加 property & 構造.
3. 編譯通過即可發版. 因 UI 還未接上, 行為對使用者無變化, 風險為 0.

Rollback: revert commit; 無任何 persisted state 需清理.

## Open Questions

- 後續 UI 要在哪裡接這個 service? (主畫面浮層按鈕? 圖片右鍵選單? tray 選單?) — 留給 follow-up change. 此 change 不阻塞.
- 是否要 log 設定成功 / 失敗 事件? — 專案目前沒有 logger, 暫不引入. 失敗時 typed exception 已足夠讓 UI 顯示訊息.
