# 工业产品表面缺陷检测系统 - 线程改进说明

> 📖 **其他语言版本** | [繁體中文](README.zh-TW.md) | [English](README.md)

## 项目概述

本项目是一个基于 YOLO 模型的工业产品表面缺陷检测系统，采用 Windows Forms 开发。系统支持两种模式：
- **监控模式**：自动监控指定目录，实时处理新产生的图片
- **手动模式**：手动选择单一文件或批量处理目录中的图片

## 线程架构改进

本系统在线程处理方面进行了多项重要改进，确保系统在高并发场景下的稳定性和性能。

### 1. Producer/Consumer 模式（生产者/消费者模式）

#### 实现方式
使用 `System.Threading.Channels` 实现异步任务队列：

```csharp
// 监控队列与背景工作 (Producer/Consumer)
private readonly Channel<MonitorWorkItem> _monitorQueue = Channel.CreateUnbounded<MonitorWorkItem>(
    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
private Task? _monitorWorkerTask;
private CancellationTokenSource? _monitorCts;
```

#### 优势
- **解耦生产与消费**：`FileSystemWatcher` 事件（生产者）与实际处理逻辑（消费者）分离
- **非阻塞**：事件处理不会阻塞 UI 线程
- **单一消费者**：确保处理顺序，避免资源竞争
- **无界队列**：可容纳大量待处理任务，不会因队列满而丢失事件

#### 工作流程
1. **生产者**：`FileSystemWatcher` 检测到新目录时，将任务加入队列
2. **消费者**：背景工作线程从队列读取任务并处理
3. **去重机制**：使用 `ConcurrentDictionary` 追踪正在处理的料号，避免重复处理

```csharp
private async Task MonitorWorkerLoopAsync(CancellationToken ct)
{
    await foreach (var item in _monitorQueue.Reader.ReadAllAsync(ct))
    {
        // 去重：同一料号在处理中就略过
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

### 2. 推论门阀（Inference Gate）

#### 实现方式
使用 `SemaphoreSlim` 确保 YOLO 模型推论的线程安全性：

```csharp
// 推论门阀：避免 _yolo 在多线程同时推论 (thread-safety)
private readonly SemaphoreSlim _inferenceGate = new(1, 1);
```

#### 使用场景
在进行模型推论前获取信号量，推论完成后释放：

```csharp
// 运行检测
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

#### 优势
- **线程安全**：确保同一时间只有一个线程进行模型推论
- **避免竞争条件**：防止多个线程同时访问 YOLO 模型导致错误
- **非阻塞等待**：使用 `WaitAsync` 而非 `Wait`，避免阻塞线程池

---

### 3. 线程安全的 UI 更新

#### 实现方式
提供统一的 `InvokeUI` 方法，确保 UI 更新在正确的线程上执行：

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

#### 使用示例
所有从背景线程更新 UI 的操作都通过此方法：

```csharp
InvokeUI(() =>
{
    currentFileLabel.Text = $"当前文件: {fileName}";
    AddLog($"处理: {fileName}");
    statusLabel.Text = $"正在处理: {fileName}";
});
```

#### 优势
- **线程安全**：自动判断是否需要切换到 UI 线程
- **统一接口**：所有 UI 更新使用相同模式，降低错误风险
- **性能优化**：如果已在 UI 线程，直接执行，无需额外开销

---

### 4. 取消令牌（CancellationToken）支持

#### 实现方式
在长时间运行的异步操作中使用 `CancellationToken`：

```csharp
private CancellationTokenSource? _cancellationTokenSource;
private CancellationTokenSource? _monitorCts;
```

#### 使用场景
1. **手动处理模式**：用户点击「停止」按钮时取消处理
2. **监控模式**：停止监控时取消背景工作
3. **文件等待**：在等待文件就绪时支持取消

```csharp
private static async Task WaitFileReadyAsync(string path, CancellationToken ct)
{
    for (int i = 0; i < maxTry; i++)
    {
        ct.ThrowIfCancellationRequested();
        // ... 检查文件是否就绪
        await Task.Delay(delayMs, ct);
    }
}
```

#### 优势
- **响应式取消**：用户可以随时停止长时间运行的操作
- **资源清理**：取消时自动释放资源，避免资源泄漏
- **优雅退出**：通过 `OperationCanceledException` 实现优雅的错误处理

---

### 5. 文件就绪等待机制

#### 实现方式
`WaitFileReadyAsync` 方法确保文件完全写入后再读取：

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
            
            // 文件大小需稳定（避免边写边读）
            if (fi.Length == lastSize)
            {
                // 能开启代表写入锁已释放（或至少可读）
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

#### 优势
- **避免读取半文件**：等待文件大小稳定后再读取
- **文件锁检查**：尝试以读取模式开启文件，确认写入锁已释放
- **超时保护**：最多等待 8 秒，避免无限等待
- **异步非阻塞**：使用 `Task.Delay` 而非 `Thread.Sleep`，不阻塞线程池

---

### 6. 去重机制

#### 实现方式
使用 `ConcurrentDictionary` 追踪正在处理的料号目录：

```csharp
private readonly ConcurrentDictionary<string, byte> _inFlightMaterials = 
    new(StringComparer.OrdinalIgnoreCase);
```

#### 使用场景
在 `MonitorWorkerLoopAsync` 中检查是否正在处理：

```csharp
// 去重：同一料号在处理中就略过（避免 Created 事件抖动导致重入）
if (!_inFlightMaterials.TryAdd(item.MaterialDirPath, 0))
    continue;
```

#### 优势
- **避免重复处理**：防止 `FileSystemWatcher` 事件抖动导致同一目录被处理多次
- **线程安全**：`ConcurrentDictionary` 提供线程安全的操作
- **高性能**：`TryAdd` 操作是 O(1) 时间复杂度

---

### 7. 异步处理模式

#### 实现方式
所有长时间运行的操作都使用 `async/await` 模式：

```csharp
private async Task ProcessMaterialDirectory(string materialDirPath, CancellationToken ct)
{
    await Task.Run(async () =>
    {
        // 处理逻辑
    });
}
```

#### 优势
- **非阻塞 UI**：UI 线程不会被长时间运行的操作阻塞
- **资源利用**：充分利用 .NET 的异步 I/O 能力
- **可扩展性**：可以轻松处理大量并发任务

---

## 线程安全总结

### 关键改进点

1. **Producer/Consumer 模式**
   - 使用 `Channel` 实现异步任务队列
   - 解耦事件处理与实际处理逻辑

2. **推论门阀**
   - 使用 `SemaphoreSlim` 确保模型推论的线程安全
   - 防止多线程同时访问 YOLO 模型

3. **线程安全的 UI 更新**
   - 统一的 `InvokeUI` 方法确保 UI 更新在正确线程
   - 自动判断是否需要切换线程

4. **取消支持**
   - 全面使用 `CancellationToken` 支持优雅取消
   - 响应用户操作，及时释放资源

5. **文件就绪等待**
   - 智能等待文件完全写入
   - 避免读取半文件或文件锁冲突

6. **去重机制**
   - 使用 `ConcurrentDictionary` 避免重复处理
   - 防止事件抖动导致的问题

### 性能优势

- **高并发处理**：可以同时处理多个料号目录
- **非阻塞 UI**：用户界面始终保持响应
- **资源效率**：充分利用异步 I/O 和线程池
- **稳定性**：完善的错误处理和资源清理机制

## 使用建议

1. **监控模式**：适合生产环境，自动处理新产生的图片
2. **手动模式**：适合测试和调试，可以精确控制处理流程
3. **参数调整**：根据实际需求调整 Confidence、Pixel Confidence 和 IoU 阈值
4. **性能优化**：如需更高处理速度，可考虑使用 CUDA 执行提供者

## 技术架构

- **框架**：.NET 8.0 Windows Forms
- **模型框架**：YoloDotNet v4.0
- **执行提供者**：CPU（可扩展至 CUDA/OpenVINO）
- **图形处理**：SkiaSharp 3.119.1
- **异步处理**：System.Threading.Channels, async/await

## 注意事项

1. 确保模型文件为 ONNX 格式（opset 17）
2. 监控目录结构应符合预期（料号目录 → 工站目录 → 图片文件）
3. 输出目录需要有写入权限
4. 处理大量图片时，注意磁盘空间和内存使用情况
