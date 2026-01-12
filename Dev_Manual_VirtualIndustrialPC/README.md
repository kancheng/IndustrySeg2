# 虛擬工控機測試目錄

此目錄用於測試虛擬工控機的投料狀態，配合 `IndustrySegSys` 系統進行自動檢測。

## 📋 使用說明

### 1. 生成測試數據

使用 `VirtualIndustrialPC` 工具生成測試數據：

```bash
cd ../VirtualIndustrialPC
python virtual_controller.py
```

生成的數據會輸出到 `VirtualIndustrialPC/lines/` 目錄。

### 2. 複製測試數據到此目錄

將生成的測試數據複製到此目錄，或直接將 `VirtualIndustrialPC` 的 `output_path` 配置為此目錄。

### 3. 使用 IndustrySegSys 進行檢測

1. 啟動 `IndustrySegSys` 應用程式
2. 選擇模型文件
3. 將「監控目錄」設置為此目錄（`dev_test_VirtualIndustrialPC`）
4. 選擇輸出目錄
5. 點擊「開始監控」

系統會自動監控此目錄，當有新的料號目錄（如 `202512290001`）出現時，會自動進行 YOLO 檢測分析。

## 📁 目錄結構

測試數據應符合以下結構：

```
dev_test_VirtualIndustrialPC/
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

## 🔄 工作流程

1. **虛擬工控機生成數據** → `VirtualIndustrialPC/lines/` 或直接到此目錄
2. **IndustrySegSys 監控** → 自動檢測新目錄
3. **YOLO 分析** → 對每個工站的每張圖片進行檢測
4. **結果輸出** → 保存到指定的輸出目錄，標記為 OK 或 NG

## ⚙️ 配置建議

在 `VirtualIndustrialPC/config.json` 中，可以將 `output_path` 設置為：

```json
{
  "output_path": "../dev_test_VirtualIndustrialPC"
}
```

這樣生成的測試數據會直接輸出到此目錄，方便測試。

## 📝 注意事項

- 確保此目錄有寫入權限
- 測試完成後可以清空此目錄以進行下一輪測試
- 建議在測試前備份重要數據

