## Why

啟動國家目前硬編碼為 `us` (見 `MainViewModel.DefaultCountryCode`), 使用者無法設定自己最常看的國家為預設. 應用程式也缺少一個放置「應用層級設定」的明確 UI 位置, 未來新增其他偏好 (如預設解析度, 自動更新時段) 都會無處可放. 此次調整一次性建立 top navbar 與一個正式的 settings UI surface, 並把第一個可配置項 (預設國家) 接上.

## What Changes

- 在 MainWindow 最上方新增一個 **top navbar**, 位於現有國旗 row 之上 (兩者皆為 DockPanel Top stack, navbar 在最上).
- top navbar 右側放置一個 **settings icon button** (gear icon). 點擊開啟 **Settings 視窗** (modal 或 owner-bound dialog).
- Settings 視窗第一個分頁/區段為 **Default country**, 列出所有從 fetcher 探索到的國家 (含國旗), 使用者可選擇任一國家為啟動預設值. 選擇後 MUST 立即持久化, 但 MUST NOT 立刻切換目前顯示的桌布.
- `UserPreferences` 新增 `DefaultCountryCode: string?` 欄位; 既有 `CloseAction` 欄位行為與檔案路徑不變. 反序列化 MUST 對缺欄位 (舊版檔案) 寬容.
- `MainViewModel.InitializeAsync` 啟動時 MUST 優先讀取使用者偏好的 default country, 若為 null 或對應 code 不在 fetcher 回傳清單中則 fallback 回 `us`.
- README 「操作」段落更新, 描述 navbar / settings 入口與 default country 設定.

## Capabilities

### New Capabilities
- `app-settings`: 應用程式 settings UI surface (navbar gear 按鈕 + Settings 視窗 shell). 涵蓋如何開啟設定, 設定視窗的版面骨架, 與「Default country」設定本身的行為.

### Modified Capabilities
- `wallpaper-viewer-ui`: 主視窗版面從兩列改為三列 (top navbar + 國旗 row + 圖片區); 啟動預設國家從硬編 `us` 改為「使用者偏好 → fallback us」.
- `system-tray`: `preferences.json` schema 擴充為包含 `defaultCountryCode` 欄位; 寫入規則 (atomic write, 損毀視為不存在) 套用至此新欄位.

## Impact

- **Code**:
  - `WallpaperApp/MainWindow.xaml` 新增 navbar Border (DockPanel Top, 在國旗 row 之上).
  - `WallpaperApp/Models/UserPreferences.cs` 新增 `DefaultCountryCode` 屬性.
  - `WallpaperApp/Services/UserPreferencesService.cs` 既有讀寫 API 自動涵蓋新欄位; 需新增 `SetDefaultCountryAsync` 便捷方法 (與 `ResetCloseActionAsync` 同風格).
  - `WallpaperApp/ViewModels/MainViewModel.cs` `InitializeAsync` 改為從 `IUserPreferencesService` 取得偏好決定首選國家.
  - 新增 `WallpaperApp/Views/SettingsWindow.xaml` (+ `.cs`) 與 `WallpaperApp/ViewModels/SettingsViewModel.cs`.
  - 新增 navbar 內 settings button 的 command 鏈 (主視窗 ViewModel 或新 `ShellViewModel`).
  - 新增 gear icon 資源 (向量 `Path` 或 `Assets/` PNG / SVG).
- **Persistence**: `preferences.json` 新增欄位 `defaultCountryCode` (string code, e.g. `"jp"`, 或 omit). 舊檔案缺欄位 MUST 視為 null.
- **Dependencies**: 無新增 NuGet. 可重用既有 `CommunityToolkit.Mvvm`.
- **Backwards compatibility**: 舊版 `preferences.json` (只含 `closeAction`) 仍 MUST 可正常載入.
