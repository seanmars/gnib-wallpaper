## Context

目前 `MainWindow.xaml` 為 `DockPanel` 兩列版面 (`Top` = 國旗 row Border, fill = 圖片 Grid). 啟動國家寫死在 `MainViewModel.DefaultCountryCode = "us"`. 應用程式已有 `UserPreferencesService` (atomic write-temp-then-replace) 與 `preferences.json` (目前只存 `CloseAction`). MVVM stack 使用 `CommunityToolkit.Mvvm` (`ObservableObject` + `[RelayCommand]`). composition root 在 `App.xaml.cs`, 已將 `WindowCloseService` 等 service 暴露為 `App.CurrentApp.WindowClose`.

本次新增的 navbar 與 settings UI 是首個「應用程式層級設定」surface, 之後 (預設解析度, auto-refresh, 主題, 等) 都會接到同一個入口, 所以骨架要可擴充, 但目前 only one 設定項 (Default country).

## Goals / Non-Goals

**Goals:**
- 在主視窗最上方放置 navbar, 右側 gear icon 點擊開啟 settings.
- Settings 為獨立視窗 (modal owner = MainWindow), 第一個區段為 Default country.
- 使用者選擇 default country 後立即持久化 (與 close action 偏好同檔案).
- 啟動時讀取 default country preference, 若 null 或非 fetcher 國家清單成員, fallback 至 `us`.
- 對舊版 `preferences.json` (沒有 `defaultCountryCode` 欄位) 完全相容.

**Non-Goals:**
- 不調整選定 default country 後**立即切換**當前桌布 (避免使用者誤觸即時跳走). 設定只影響**下次啟動**.
- 不新增 Settings 視窗內其他分頁/設定項 (預設解析度等留待後續 change).
- 不導入 DI container (繼續沿用 `App.xaml.cs` 手動 wiring 風格).
- 不引入 IconFont / FluentSystemIcons NuGet; gear icon 以 XAML `Path` 向量繪製.

## Decisions

### 1. navbar 是獨立 row, 不是國旗 row 的延伸

**Decision**: 在現有國旗 row 之上, 多加一個 `Border DockPanel.Dock="Top"` 作為 navbar. 結構由上而下: navbar → 國旗 row → 圖片區. 三者皆是 `DockPanel` children, 圖片 Grid 仍為 `LastChildFill`.

**Why**: navbar 與國旗 row 職責不同 (一個是 app shell, 一個是內容導覽). 混在同一 row 會讓國旗 row 失去「水平捲軸 + ScrollViewer」的單純語意, 且設計上 navbar 之後會放更多 app-level 控制 (帳號 / 主題 / 通知等), 應有獨立容器.

**Alternative considered**: 把 gear button 塞進國旗 row 最右側. 拒絕原因 — 國旗 row 已用 `ScrollViewer + HorizontalScrollBarVisibility="Auto"`, 國旗多時 gear button 會被捲到看不見.

### 2. Settings 為獨立 `Window`, owner = MainWindow

**Decision**: 新增 `Views/SettingsWindow.xaml` (繼承 `Window`), 透過 `Owner = mainWindow; ShowDialog()` 顯示.

**Why**: WPF 慣例的 modal 設定面板就是 child Window + `ShowDialog`. 它自動處理 ESC 關閉, 集中焦點, 點不到 owner. 與既有 `CloseConfirmDialog` 模式一致 (檢視 `Views/CloseConfirmDialog.xaml`).

**Alternative considered**: in-place panel / overlay. 拒絕原因 — 需要自製 overlay + dismiss 邏輯, 也會擠壓圖片區. 也考慮 `Page` + `NavigationWindow`, 對單頁設定 over-engineering.

### 3. Default country 設定只影響下次啟動, 不立即切換

**Decision**: 選擇 default country 後只呼叫 `IUserPreferencesService.SetDefaultCountryAsync(code)`, 不觸發 `MainViewModel.SelectCountryAsync`.

**Why**: 這是「啟動偏好」, 不是「目前檢視」. 使用者調設定時可能在看 jp 桌布, 不該因設了 fr 為預設就立刻被切走. 與 `CloseAction` 偏好同一個 mental model — 「設定下次行為」.

**Alternative considered**: 立即套用 + 「Apply」按鈕. 拒絕原因 — 增加複雜度, 而當前只有一個設定項. 後續若新增需要 apply 的設定 (e.g. 解析度) 再加 Apply / Cancel pattern.

### 4. `UserPreferences` 直接新增屬性, 不分檔

**Decision**: `Models/UserPreferences.cs` 新增 `public string? DefaultCountryCode { get; set; }`, 沿用既有 `preferences.json` 與 `UserPreferencesService`.

**Why**: 與 `CloseAction` 同等性質 (使用者層級偏好). 拆成兩個檔案 (e.g. `app-settings.json` vs `preferences.json`) 只會增加 IO 與不一致風險. `System.Text.Json` 預設對缺欄位寬容, 舊檔案 deserialize 後 `DefaultCountryCode = null`.

**Alternative considered**: 拆 `appsettings.json` 與 `preferences.json`. 拒絕原因 — semantic 上同類, 沒有獨立 lifecycle. (若日後出現需 reset 的設定群組可以再拆.)

### 5. Settings 開啟入口由 `MainViewModel` 暴露 command

**Decision**: 新增 `MainViewModel.OpenSettingsCommand` (`[RelayCommand]`). navbar 的 gear `Button.Command` 綁此 command. command 內以 `App.CurrentApp` 取得 main window 作 owner, 建立 `SettingsWindow` + `SettingsViewModel` 並 `ShowDialog`. `SettingsViewModel` 需接受 `IUserPreferencesService` 與目前的國家清單 (從 `MainViewModel.Countries` 傳入).

**Why**: 維持單一 ViewModel root + composition root 在 `App.xaml.cs` 的既有風格, 不引入 navigation service. 國家清單已存在於 `MainViewModel.Countries`, 直接傳遞避免重複 `DiscoverCountriesAsync`.

**Alternative considered**: 引入 `INavigationService` + `IDialogService` 抽象. 拒絕原因 — 過度設計. 專案還小, View ↔ ViewModel 直接連線足夠.

### 6. fallback 行為集中在 `InitializeAsync`

**Decision**: `InitializeAsync` 改為:
```
var prefs = await _preferences.LoadAsync();
var preferred = prefs.DefaultCountryCode;
var defaultItem =
    (!string.IsNullOrEmpty(preferred)
        ? items.FirstOrDefault(i => string.Equals(i.Code, preferred, StringComparison.OrdinalIgnoreCase))
        : null)
    ?? items.FirstOrDefault(i => string.Equals(i.Code, "us", StringComparison.OrdinalIgnoreCase))
    ?? items.FirstOrDefault();
```

**Why**: 既有 fallback 鏈已存在 (US → first), 只在最前面插入「使用者偏好」即可, 行為一致且讀者一眼能看懂優先序.

## Risks / Trade-offs

- **[Risk] 偏好檔載入失敗 (磁碟錯誤 / JSON 壞)** → `UserPreferencesService.LoadAsync` 已 catch `IOException / JsonException / UnauthorizedAccessException` 並回傳 `new UserPreferences()`, 等同 `DefaultCountryCode = null`, 自動 fallback 至 us. 沒有新風險.
- **[Risk] 使用者選的 code 之後 fetcher 不再回傳 (站方移除某國)** → `InitializeAsync` fallback 鏈會接住, 不會 crash, 但使用者也不會收到「您的預設國家已不可用」提示. Mitigation: 文件記錄, 未來可加 toast. 此 change 不處理.
- **[Trade-off] Settings 為 modal, 開啟時不能操作主視窗** → 對單頁設定 acceptable; 若日後設定變多可改 non-modal. 不在此 scope.
- **[Trade-off] Gear icon 為向量 `Path`, 視覺風格與 OS 圖示不完全一致** → 接受, 換來零相依與可隨主題改色.
- **[Risk] navbar 高度擠壓圖片區可視高度** → MinHeight 設小 (32-40 px), 對主視窗 MinHeight 400 仍有充足空間. 已驗證設計上可行.

## Migration Plan

1. **Schema 擴充先行**: 先合併 `UserPreferences.DefaultCountryCode` 屬性 (`System.Text.Json` 自動相容舊檔), 此時 default country = null = 既有 us 行為.
2. **UI 落地**: 加入 navbar + Settings 視窗, 寫入路徑啟用.
3. **InitializeAsync 接上偏好**: 啟動讀取 preference, 才會發生「使用者體驗到的」行為改變.
4. **Rollback**: 撤回此 change 後, 舊版 binary 讀取含 `defaultCountryCode` 的偏好檔 — `System.Text.Json` 預設忽略未知欄位, 不會 crash. 使用者只是失去 default country 設定, 退回 `us`.

## Open Questions

- 是否需在 Settings UI 顯示「目前實際載入的國家可能與預設不同」之提示? **暫定不做** — 行為已透過 Decision 3 明示 (只影響下次啟動), navbar/settings 介面文案可放小字註明.
- gear icon 是否需要 hover / active 視覺 state? **暫定提供基本 hover (背景變色)**, 與 close confirm 按鈕風格一致.
