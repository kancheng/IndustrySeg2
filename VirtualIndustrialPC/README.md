# 虛擬工控機系統

一個用於模擬工業控制機台運作的 Python 工具，根據 OK/NG 組圖自動生成虛假產品圖像，適用於測試、開發和演示場景。

## 📋 功能特點

- ✅ **自動生成產品圖像**：根據 OK/NG 組圖隨機生成產品圖像
- ✅ **混搭模式**：每張圖片獨立從 OK 或 NG 中隨機選擇，實現真實的混搭效果
- ✅ **可配置比率**：支援在 JSON 中設定 OK 和 NG 的比率
- ✅ **時間控制**：可設定進料間隔和工站處理間隔
- ✅ **自動配置**：首次執行時自動生成配置文件
- ✅ **路徑驗證**：自動檢查 OK/NG 目錄並提示狀態
- ✅ **編碼規則**：按照標準格式生成產品編碼

## 🚀 系統要求

- Python 3.6 或更高版本
- 標準庫模組（無需額外安裝套件）

## 📦 安裝

1. 克隆或下載此專案
2. 確保已安裝 Python 3.6+

```bash
# 檢查 Python 版本
python --version
```

## 🎯 使用方法

### 基本使用

直接執行主程式：

```bash
python virtual_controller.py
```

### 首次執行

首次執行時，系統會自動創建 `config.json` 配置文件。您可以根據需求修改配置。

## 📦 打包成可執行檔

使用 PyInstaller 可以將程式打包成獨立的 `.exe` 執行檔，無需安裝 Python 即可運行。

### 安裝 PyInstaller

```bash
pip install pyinstaller
```

### 打包指令

#### 基本打包（單一檔案）

```bash
pyinstaller --onefile virtual_controller.py
```

#### 打包選項說明

**單一檔案模式**（推薦）：
```bash
pyinstaller --onefile --name virtual_controller virtual_controller.py
```

**包含圖標**（可選）：
```bash
pyinstaller --onefile --icon=icon.ico --name virtual_controller virtual_controller.py
```

**隱藏控制台視窗**（GUI 模式，可選）：
```bash
pyinstaller --onefile --windowed --name virtual_controller virtual_controller.py
```

**完整打包指令範例**：
```bash
pyinstaller --onefile --name "虛擬工控機" --clean virtual_controller.py
```

### 打包後的文件

打包完成後，會在 `dist/` 目錄下生成可執行檔：

```
dist/
└── virtual_controller.exe  # 或您指定的名稱
```

### 使用打包後的執行檔

1. 將生成的 `.exe` 檔案複製到目標位置
2. 確保 `ok/` 和 `ng/` 目錄與 `.exe` 在同一目錄（或根據配置調整路徑）
3. 雙擊執行 `.exe` 檔案
4. 首次執行會自動生成 `config.json` 配置文件

### 打包注意事項

- **配置文件**：`config.json` 會在首次執行時自動生成，無需手動複製
- **目錄結構**：建議將 `ok/` 和 `ng/` 目錄與 `.exe` 放在同一目錄
- **防毒軟體**：某些防毒軟體可能會誤報，這是 PyInstaller 打包檔案的正常現象
- **檔案大小**：單一檔案模式會包含 Python 解釋器，檔案較大（約 10-20 MB）

### 打包目錄結構建議

```
release/
├── virtual_controller.exe
├── config.json          # 可選，首次執行會自動生成
├── ok/                  # OK 圖片目錄
│   └── ...
├── ng/                  # NG 圖片目錄
│   └── ...
└── lines/               # 輸出目錄（自動生成）
    └── ...
```

## ⚙️ 配置說明

配置文件 `config.json` 包含以下參數：

| 參數 | 說明 | 預設值 | 範例 |
|------|------|--------|------|
| `num_stations` | 工站數量 | 3 | 3 |
| `images_per_station` | 每個工站的圖片數量 | 4 | 4 |
| `ok_images_path` | OK 圖片目錄路徑 | "ok" | "ok" |
| `ng_images_path` | NG 圖片目錄路徑 | "ng" | "ng" |
| `output_path` | 輸出目錄路徑 | "lines" | "lines" |
| `daily_production` | 今日投料數 | 3 | 3 |
| `ok_rate` | OK 圖片選擇比率 | 0.5 | 0.5 (50%) |
| `ng_rate` | NG 圖片選擇比率 | 0.5 | 0.5 (50%) |
| `wait_time` | 進料間隔時間（秒） | 10 | 10 |
| `station_interval` | 工站處理間隔時間（秒） | 5 | 5 |

### 配置範例

```json
{
  "num_stations": 3,
  "images_per_station": 4,
  "ok_images_path": "ok",
  "ng_images_path": "ng",
  "output_path": "lines",
  "daily_production": 5,
  "ok_rate": 0.8,
  "ng_rate": 0.2,
  "wait_time": 10,
  "station_interval": 5
}
```

### 比率設定說明

- `ok_rate` 和 `ng_rate` 可以分別設定
- 如果只設定其中一個，另一個會自動計算（`1 - 設定值`）
- 如果兩個都設定但總和不等於 1，系統會自動正規化
- 例如：設定 `ok_rate: 0.8, ng_rate: 0.3`，系統會自動正規化為 `ok_rate: 0.727, ng_rate: 0.273`

## 📁 目錄結構

### 輸入目錄

```
project/
├── ok/              # OK 圖片目錄
│   ├── 1.png
│   ├── 2.png
│   └── ...
├── ng/              # NG 圖片目錄
│   ├── 1.png
│   ├── 2.png
│   └── ...
└── virtual_controller.py
```

### 輸出目錄結構

```
lines/
├── 202512290001/    # 日期 + 料號
│   ├── S1/          # 工站 1
│   │   ├── 1.png
│   │   ├── 2.png
│   │   ├── 3.png
│   │   └── 4.png
│   ├── S2/          # 工站 2
│   │   ├── 1.png
│   │   ├── 2.png
│   │   ├── 3.png
│   │   └── 4.png
│   └── S3/          # 工站 3
│       ├── 1.png
│       ├── 2.png
│       ├── 3.png
│       └── 4.png
├── 202512290002/
│   └── ...
└── 202512290003/
    └── ...
```

## 🔢 編碼規則

產品編碼格式：`YYYYMMDD + 料號(4位) + S + 工站號 + 圖片編號`

### 編碼範例

- `202512290001S11`：2025年12月29日，料號0001，工站1的第1張圖
- `202512290001S12`：2025年12月29日，料號0001，工站1的第2張圖
- `202512292501S43`：2025年12月29日，料號2501，工站4的第3張圖

### 編碼組成

1. **日期**：8位數字，格式為 `YYYYMMDD`
2. **料號**：4位數字，從 1 開始遞增
3. **工站標識**：`S` + 工站編號（1, 2, 3, ...）
4. **圖片編號**：每工站的圖片編號（1, 2, 3, ...）

## 🔄 工作流程

1. **初始化**：載入或創建配置文件
2. **驗證路徑**：檢查 OK/NG 目錄是否存在並提示狀態
3. **生成產品圖像**：
   - 為每個料號創建目錄
   - 為每個工站生成指定數量的圖片
   - 每張圖片根據設定的比率從 OK 或 NG 中隨機選擇
   - 工站之間等待 `station_interval` 秒
   - 進料之間等待 `wait_time` 秒

## 📝 執行範例

```
==================================================
虛擬工控機系統
==================================================
載入配置文件: config.json
✓ OK 圖片目錄找到 3 張圖片: ok
✓ NG 圖片目錄找到 16 張圖片: ng

開始生成產品圖像...
日期: 20251229
工站數量: 3
每工站圖片數: 4
今日投料數: 3
OK 率: 50.0%
NG 率: 50.0%
進料間隔: 10 秒
工站處理間隔: 5 秒

生成料號 0001...
  202512290001S11 [OK] -> lines\202512290001\S1\1.png
  202512290001S12 [NG] -> lines\202512290001\S1\2.png
  202512290001S13 [OK] -> lines\202512290001\S1\3.png
  202512290001S14 [NG] -> lines\202512290001\S1\4.png
  ...

等待 10 秒後處理下一片進料...

生成料號 0002...
  ...

✓ 完成！共進行 3 個產品的製造
輸出目錄: F:\newproj\vin\lines
==================================================
```

## ⚠️ 注意事項

1. **圖片格式**：支援 `.png` 和 `.jpg` 格式
2. **目錄檢查**：如果 OK 或 NG 目錄不存在或沒有圖片，系統會發出警告
3. **比率設定**：建議 `ok_rate` 和 `ng_rate` 的總和為 1，系統會自動正規化
4. **時間設定**：`wait_time` 和 `station_interval` 設為 0 則不等待

## 🛠️ 開發

### 程式結構

- `virtual_controller.py`：主程式，包含 `VirtualController` 類別
- `config.json`：配置文件（首次執行時自動生成）

### 主要類別和方法

- `VirtualController`：虛擬工控機主類別
  - `load_or_create_config()`：載入或創建配置
  - `validate_paths()`：驗證路徑
  - `generate_images()`：生成產品圖像
  - `generate_product_code()`：生成產品編碼

## 📄 授權

此專案為開源專案，可自由使用和修改。

## 🤝 貢獻

歡迎提交 Issue 和 Pull Request！

## 📧 聯絡

如有問題或建議，請透過 GitHub Issues 聯繫。

---

**版本**：1.0.0  
**最後更新**：2025-12-29

