## ADDED Requirements

### Requirement: Cache 目錄位置

系統 SHALL 將圖片與 metadata 寫入 `%LOCALAPPDATA%/WallpaperApp/cache/{country_code}/` 目錄. 系統 MUST 在第一次寫入時遞迴建立目錄.

#### Scenario: 首次啟動建立目錄

- **WHEN** cache 根目錄不存在且系統嘗試寫入 us 國家的圖片
- **THEN** 系統 MUST 自動建立 `%LOCALAPPDATA%/WallpaperApp/cache/us/` 目錄

### Requirement: 每國僅保留最新一張

系統 SHALL 對每個國家在 cache 內僅保留最新一張 jpg 與其對應的 metadata json. 當新圖片落地後系統 MUST 刪除同國家資料夾內所有其他 jpg 與 json.

#### Scenario: 新圖片取代舊圖片

- **WHEN** us 國家資料夾已存在 `2026-05-20-OldSlug.jpg` + `.json`, 且系統下載新的 `2026-05-22-NewSlug.jpg`
- **THEN** 寫入新檔後系統 MUST 刪除 `2026-05-20-OldSlug.jpg` 與 `2026-05-20-OldSlug.json`
- **AND** 該資料夾結束狀態 MUST 僅有 `2026-05-22-NewSlug.jpg` 與 `2026-05-22-NewSlug.json`

#### Scenario: 寫入新檔時刪除舊檔失敗

- **WHEN** 舊檔被其他程序鎖定無法刪除
- **THEN** 系統 MUST 不拋出例外, 改為記錄 warning, 並保留新檔
- **AND** 新檔仍可正常被讀取與顯示

### Requirement: 快取命中判斷

系統 SHALL 在切換國家時先檢查該國 cache. 若 cache 內存在 metadata 且其 `fetched_date` (UTC 日期) 等於今日 UTC 日期, 系統 MUST 直接讀取本地檔案而非重新抓取網路.

#### Scenario: 今日已抓取過

- **WHEN** us cache 內含 metadata 且 `fetched_date` = 今日 UTC
- **THEN** 系統 MUST 從本地 jpg 載入 BitmapImage, 不發起 HTTP 請求

#### Scenario: 舊日資料

- **WHEN** us cache 內 metadata 的 `fetched_date` 為昨日或更早
- **THEN** 系統 MUST 視為 cache miss, 重新從網路抓取今日桌布

#### Scenario: Cache 不存在

- **WHEN** us 國家資料夾不存在或內無 metadata
- **THEN** 系統 MUST 視為 cache miss 並從網路抓取

### Requirement: Metadata JSON 格式

每張快取圖片 MUST 對應一個 `.json` 檔, 內含至少: `country_code`, `country_name`, `date` (YYYY-MM-DD), `slug`, `title`, `copyright`, `detail_url`, `download_urls` (含三種解析度的 URL, 缺者為 null), `downloaded_resolution`, `image_path` (絕對路徑), `bytes`, `fetched_at` (ISO 8601 UTC).

#### Scenario: 寫入完整 metadata

- **WHEN** 系統成功下載一張圖片並寫入 metadata
- **THEN** 對應 json 檔 MUST 為有效 JSON, 包含上述所有欄位
- **AND** `fetched_at` MUST 為 ISO 8601 UTC 格式 (e.g. `2026-05-22T10:30:00Z`)

### Requirement: Cache 寫入失敗不影響顯示

若 cache 寫入失敗 (磁碟滿、權限問題), 系統 MUST 仍顯示已下載的圖片 (in-memory), 並 log warning. 系統 MUST NOT 因 cache 失敗而顯示錯誤畫面.

#### Scenario: 磁碟寫入失敗

- **WHEN** 圖片已下載至記憶體但 `File.WriteAllBytesAsync` 拋出 IOException
- **THEN** 系統 MUST 捕捉例外、log warning
- **AND** UI MUST 仍顯示該圖片 (state = Loaded)
