# 工業產品表面缺陷檢測系統 - 執行緒改進說明

> 📖 **其他語言版本** | [简体中文](README.zh-CN.md) | [English](README.md)

## 專案概述

本專案是一個基於 YOLO 模型的工業產品表面缺陷檢測系統，採用 Windows Forms 開發。系統支援兩種模式：
- **監控模式**：自動監控指定目錄，即時處理新產生的圖片
- **手動模式**：手動選擇單一檔案或批量處理目錄中的圖片

## 執行緒架構改進

本系統在執行緒處理方面進行了多項重要改進，確保系統在高併發場景下的穩定性和效能。

### 1. Producer/Consumer 模式（生產者/消費者模式）

#### 實現方式
使用 `System.Threading.Channels` 實現異步任務佇列：

```csharp
// 監控佇列與背景工作 (Producer/Consumer)
private readonly Channel<MonitorWorkItem> _monitorQueue = Channel.CreateUnbounded<MonitorWorkItem>(
    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
private Task? _monitorWorkerTask;
private CancellationTokenSource? _monitorCts;
```

#### 優勢
- **解耦生產與消費**：`FileSystemWatcher` 事件（生產者）與實際處理邏輯（消費者）分離
- **非阻塞**：事件處理不會阻塞 UI 執行緒
- **單一消費者**：確保處理順序，避免資源競爭
- **無界佇列**：可容納大量待處理任務，不會因佇列滿而丟失事件

#### 工作流程
1. **生產者**：`FileSystemWatcher` 檢測到新目錄時，將任務加入佇列
2. **消費者**：背景工作執行緒從佇列讀取任務並處理
3. **去重機制**：使用 `ConcurrentDictionary` 追蹤正在處理的料號，避免重複處理

```csharp
private async Task MonitorWorkerLoopAsync(CancellationToken ct)
{
    await foreach (var item in _monitorQueue.Reader.ReadAllAsync(ct))
    {
        // 去重：同一料號在處理中就略過
        if (!_inFlightMaterials.TryAdd(item.MaterialDirPath, 0))
            continue;
        
        try
        {
            await ProcessMaterialDirectory(item.MaterialDirPath, ct);
        }
        finally
        {
            _inFlightMaterials.TryRemove(item.MaterialDirPath, out _);
        }
    }
}
```

---

### 2. 推論門閥（Inference Gate）

#### 實現方式
使用 `SemaphoreSlim` 確保 YOLO 模型推論的線程安全性：

```csharp
// 推論門閥：避免 _yolo 在多執行緒同時推論 (thread-safety)
private readonly SemaphoreSlim _inferenceGate = new(1, 1);
```

#### 使用場景
在進行模型推論前獲取信號量，推論完成後釋放：

```csharp
// 運行檢測
await _inferenceGate.WaitAsync(ct);
List<SegmentationBoundingBox> results;
try
{
    results = _yolo!.RunSegmentation(image, confidence: confidence, 
                                     pixelConfedence: pixelConfidence, iou: iou);
}
finally
{
    _inferenceGate.Release();
}
```

#### 優勢
- **線程安全**：確保同一時間只有一個執行緒進行模型推論
- **避免競爭條件**：防止多個執行緒同時訪問 YOLO 模型導致錯誤
- **非阻塞等待**：使用 `WaitAsync` 而非 `Wait`，避免阻塞執行緒池

---

### 3. 線程安全的 UI 更新

#### 實現方式
提供統一的 `InvokeUI` 方法，確保 UI 更新在正確的執行緒上執行：

```csharp
private void InvokeUI(Action action)
{
    if (InvokeRequired)
    {
        Invoke(action);
    }
    else
    {
        action();
    }
}
```

#### 使用範例
所有從背景執行緒更新 UI 的操作都通過此方法：

```csharp
InvokeUI(() =>
{
    currentFileLabel.Text = $"當前文件: {fileName}";
    AddLog($"處理: {fileName}");
    statusLabel.Text = $"正在處理: {fileName}";
});
```

#### 優勢
- **執行緒安全**：自動判斷是否需要切換到 UI 執行緒
- **統一介面**：所有 UI 更新使用相同模式，降低錯誤風險
- **效能優化**：如果已在 UI 執行緒，直接執行，無需額外開銷

---

### 4. 取消令牌（CancellationToken）支持

#### 實現方式
在長時間運行的異步操作中使用 `CancellationToken`：

```csharp
private CancellationTokenSource? _cancellationTokenSource;
private CancellationTokenSource? _monitorCts;
```

#### 使用場景
1. **手動處理模式**：用戶點擊「停止」按鈕時取消處理
2. **監控模式**：停止監控時取消背景工作
3. **檔案等待**：在等待檔案就緒時支持取消

```csharp
private static async Task WaitFileReadyAsync(string path, CancellationToken ct)
{
    for (int i = 0; i < maxTry; i++)
    {
        ct.ThrowIfCancellationRequested();
        // ... 檢查檔案是否就緒
        await Task.Delay(delayMs, ct);
    }
}
```

#### 優勢
- **響應式取消**：用戶可以隨時停止長時間運行的操作
- **資源清理**：取消時自動釋放資源，避免資源洩漏
- **優雅退出**：通過 `OperationCanceledException` 實現優雅的錯誤處理

---

### 5. 檔案就緒等待機制

#### 實現方式
`WaitFileReadyAsync` 方法確保檔案完全寫入後再讀取：

```csharp
private static async Task WaitFileReadyAsync(string path, CancellationToken ct)
{
    const int maxTry = 80; // 8s (80 * 100ms)
    const int delayMs = 100;
    
    long lastSize = -1;
    for (int i = 0; i < maxTry; i++)
    {
        ct.ThrowIfCancellationRequested();
        
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists)
            {
                await Task.Delay(delayMs, ct);
                continue;
            }
            
            // 檔案大小需穩定（避免邊寫邊讀）
            if (fi.Length == lastSize)
            {
                // 能開啟代表寫入鎖已釋放（或至少可讀）
                using var _ = new FileStream(path, FileMode.Open, 
                                           FileAccess.Read, FileShare.Read);
                return;
            }
            
            lastSize = fi.Length;
        }
        catch
        {
            // still writing/locking
        }
        
        await Task.Delay(delayMs, ct);
    }
    
    throw new IOException($"File not ready: {path}");
}
```

#### 優勢
- **避免讀取半檔**：等待檔案大小穩定後再讀取
- **檔案鎖檢查**：嘗試以讀取模式開啟檔案，確認寫入鎖已釋放
- **超時保護**：最多等待 8 秒，避免無限等待
- **異步非阻塞**：使用 `Task.Delay` 而非 `Thread.Sleep`，不阻塞執行緒池

---

### 6. 去重機制

#### 實現方式
使用 `ConcurrentDictionary` 追蹤正在處理的料號目錄：

```csharp
private readonly ConcurrentDictionary<string, byte> _inFlightMaterials = 
    new(StringComparer.OrdinalIgnoreCase);
```

#### 使用場景
在 `MonitorWorkerLoopAsync` 中檢查是否正在處理：

```csharp
// 去重：同一料號在處理中就略過（避免 Created 事件抖動導致重入）
if (!_inFlightMaterials.TryAdd(item.MaterialDirPath, 0))
    continue;
```

#### 優勢
- **避免重複處理**：防止 `FileSystemWatcher` 事件抖動導致同一目錄被處理多次
- **線程安全**：`ConcurrentDictionary` 提供線程安全的操作
- **高效能**：`TryAdd` 操作是 O(1) 時間複雜度

---

### 7. 異步處理模式

#### 實現方式
所有長時間運行的操作都使用 `async/await` 模式：

```csharp
private async Task ProcessMaterialDirectory(string materialDirPath, CancellationToken ct)
{
    await Task.Run(async () =>
    {
        // 處理邏輯
    });
}
```

#### 優勢
- **非阻塞 UI**：UI 執行緒不會被長時間運行的操作阻塞
- **資源利用**：充分利用 .NET 的異步 I/O 能力
- **可擴展性**：可以輕鬆處理大量並發任務

---

## 執行緒安全總結

### 關鍵改進點

1. **Producer/Consumer 模式**
   - 使用 `Channel` 實現異步任務佇列
   - 解耦事件處理與實際處理邏輯

2. **推論門閥**
   - 使用 `SemaphoreSlim` 確保模型推論的線程安全
   - 防止多執行緒同時訪問 YOLO 模型

3. **線程安全的 UI 更新**
   - 統一的 `InvokeUI` 方法確保 UI 更新在正確執行緒
   - 自動判斷是否需要切換執行緒

4. **取消支持**
   - 全面使用 `CancellationToken` 支持優雅取消
   - 響應用戶操作，及時釋放資源

5. **檔案就緒等待**
   - 智能等待檔案完全寫入
   - 避免讀取半檔或檔案鎖衝突

6. **去重機制**
   - 使用 `ConcurrentDictionary` 避免重複處理
   - 防止事件抖動導致的問題

### 效能優勢

- **高併發處理**：可以同時處理多個料號目錄
- **非阻塞 UI**：用戶介面始終保持響應
- **資源效率**：充分利用異步 I/O 和執行緒池
- **穩定性**：完善的錯誤處理和資源清理機制

## 使用建議

1. **監控模式**：適合生產環境，自動處理新產生的圖片
2. **手動模式**：適合測試和調試，可以精確控制處理流程
3. **參數調整**：根據實際需求調整 Confidence、Pixel Confidence 和 IoU 閾值
4. **效能優化**：如需更高處理速度，可考慮使用 CUDA 執行提供者

## 技術架構

- **框架**：.NET 8.0 Windows Forms
- **模型框架**：YoloDotNet v4.0
- **執行提供者**：CPU（可擴展至 CUDA/OpenVINO）
- **圖形處理**：SkiaSharp 3.119.1
- **異步處理**：System.Threading.Channels, async/await

## 注意事項

1. 確保模型檔案為 ONNX 格式（opset 17）
2. 監控目錄結構應符合預期（料號目錄 → 工站目錄 → 圖片檔案）
3. 輸出目錄需要有寫入權限
4. 處理大量圖片時，注意磁碟空間和記憶體使用情況
