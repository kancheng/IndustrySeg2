# Industrial Product Surface Defect Detection System - Threading Improvements

> 📖 **Other Languages** | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)

## Project Overview

This project is an industrial product surface defect detection system based on YOLO models, developed using Windows Forms. The system supports two modes:
- **Monitoring Mode**: Automatically monitors specified directories and processes newly generated images in real-time
- **Manual Mode**: Manually select a single file or batch process images in a directory

## Threading Architecture Improvements

The system has implemented several important threading improvements to ensure stability and performance in high-concurrency scenarios.

### 1. Producer/Consumer Pattern

#### Implementation
Uses `System.Threading.Channels` to implement an asynchronous task queue:

```csharp
// Monitor queue and background worker (Producer/Consumer)
private readonly Channel<MonitorWorkItem> _monitorQueue = Channel.CreateUnbounded<MonitorWorkItem>(
    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
private Task? _monitorWorkerTask;
private CancellationTokenSource? _monitorCts;
```

#### Advantages
- **Decoupled Production and Consumption**: `FileSystemWatcher` events (producer) are separated from actual processing logic (consumer)
- **Non-blocking**: Event handling does not block the UI thread
- **Single Consumer**: Ensures processing order and avoids resource contention
- **Unbounded Queue**: Can accommodate a large number of pending tasks without losing events due to queue overflow

#### Workflow
1. **Producer**: When `FileSystemWatcher` detects a new directory, it enqueues a task
2. **Consumer**: Background worker thread reads tasks from the queue and processes them
3. **Deduplication**: Uses `ConcurrentDictionary` to track materials being processed, avoiding duplicate processing

```csharp
private async Task MonitorWorkerLoopAsync(CancellationToken ct)
{
    await foreach (var item in _monitorQueue.Reader.ReadAllAsync(ct))
    {
        // Deduplication: skip if material is already being processed
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

### 2. Inference Gate

#### Implementation
Uses `SemaphoreSlim` to ensure thread safety for YOLO model inference:

```csharp
// Inference gate: prevents _yolo from concurrent inference across multiple threads (thread-safety)
private readonly SemaphoreSlim _inferenceGate = new(1, 1);
```

#### Usage
Acquire the semaphore before model inference and release it after completion:

```csharp
// Run detection
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

#### Advantages
- **Thread Safety**: Ensures only one thread performs model inference at a time
- **Avoids Race Conditions**: Prevents multiple threads from accessing the YOLO model simultaneously, which could cause errors
- **Non-blocking Wait**: Uses `WaitAsync` instead of `Wait`, avoiding thread pool blocking

---

### 3. Thread-Safe UI Updates

#### Implementation
Provides a unified `InvokeUI` method to ensure UI updates execute on the correct thread:

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

#### Usage Example
All UI updates from background threads go through this method:

```csharp
InvokeUI(() =>
{
    currentFileLabel.Text = $"Current File: {fileName}";
    AddLog($"Processing: {fileName}");
    statusLabel.Text = $"Processing: {fileName}";
});
```

#### Advantages
- **Thread Safety**: Automatically determines whether to switch to the UI thread
- **Unified Interface**: All UI updates use the same pattern, reducing error risk
- **Performance Optimization**: If already on the UI thread, executes directly without additional overhead

---

### 4. CancellationToken Support

#### Implementation
Uses `CancellationToken` in long-running asynchronous operations:

```csharp
private CancellationTokenSource? _cancellationTokenSource;
private CancellationTokenSource? _monitorCts;
```

#### Usage Scenarios
1. **Manual Processing Mode**: Cancel processing when user clicks "Stop" button
2. **Monitoring Mode**: Cancel background work when stopping monitoring
3. **File Waiting**: Support cancellation while waiting for files to be ready

```csharp
private static async Task WaitFileReadyAsync(string path, CancellationToken ct)
{
    for (int i = 0; i < maxTry; i++)
    {
        ct.ThrowIfCancellationRequested();
        // ... check if file is ready
        await Task.Delay(delayMs, ct);
    }
}
```

#### Advantages
- **Responsive Cancellation**: Users can stop long-running operations at any time
- **Resource Cleanup**: Automatically releases resources on cancellation, avoiding resource leaks
- **Graceful Exit**: Implements graceful error handling through `OperationCanceledException`

---

### 5. File Ready Wait Mechanism

#### Implementation
The `WaitFileReadyAsync` method ensures files are fully written before reading:

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
            
            // File size must be stable (avoid reading while writing)
            if (fi.Length == lastSize)
            {
                // If we can open it, the write lock has been released (or at least readable)
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

#### Advantages
- **Avoids Reading Partial Files**: Waits for file size to stabilize before reading
- **File Lock Check**: Attempts to open file in read mode to confirm write lock is released
- **Timeout Protection**: Maximum wait of 8 seconds to avoid infinite waiting
- **Asynchronous Non-blocking**: Uses `Task.Delay` instead of `Thread.Sleep`, does not block thread pool

---

### 6. Deduplication Mechanism

#### Implementation
Uses `ConcurrentDictionary` to track material directories being processed:

```csharp
private readonly ConcurrentDictionary<string, byte> _inFlightMaterials = 
    new(StringComparer.OrdinalIgnoreCase);
```

#### Usage
Check if already being processed in `MonitorWorkerLoopAsync`:

```csharp
// Deduplication: skip if material is already being processed (avoids re-entry due to Created event jitter)
if (!_inFlightMaterials.TryAdd(item.MaterialDirPath, 0))
    continue;
```

#### Advantages
- **Avoids Duplicate Processing**: Prevents `FileSystemWatcher` event jitter from causing the same directory to be processed multiple times
- **Thread Safety**: `ConcurrentDictionary` provides thread-safe operations
- **High Performance**: `TryAdd` operation is O(1) time complexity

---

### 7. Asynchronous Processing Pattern

#### Implementation
All long-running operations use the `async/await` pattern:

```csharp
private async Task ProcessMaterialDirectory(string materialDirPath, CancellationToken ct)
{
    await Task.Run(async () =>
    {
        // Processing logic
    });
}
```

#### Advantages
- **Non-blocking UI**: UI thread is not blocked by long-running operations
- **Resource Utilization**: Fully utilizes .NET's asynchronous I/O capabilities
- **Scalability**: Can easily handle large numbers of concurrent tasks

---

## Thread Safety Summary

### Key Improvements

1. **Producer/Consumer Pattern**
   - Uses `Channel` to implement asynchronous task queue
   - Decouples event handling from actual processing logic

2. **Inference Gate**
   - Uses `SemaphoreSlim` to ensure thread safety for model inference
   - Prevents multiple threads from accessing the YOLO model simultaneously

3. **Thread-Safe UI Updates**
   - Unified `InvokeUI` method ensures UI updates on correct thread
   - Automatically determines whether thread switching is needed

4. **Cancellation Support**
   - Comprehensive use of `CancellationToken` for graceful cancellation
   - Responds to user operations and releases resources promptly

5. **File Ready Wait**
   - Intelligently waits for files to be fully written
   - Avoids reading partial files or file lock conflicts

6. **Deduplication Mechanism**
   - Uses `ConcurrentDictionary` to avoid duplicate processing
   - Prevents issues caused by event jitter

### Performance Advantages

- **High Concurrency Processing**: Can process multiple material directories simultaneously
- **Non-blocking UI**: User interface remains responsive at all times
- **Resource Efficiency**: Fully utilizes asynchronous I/O and thread pool
- **Stability**: Comprehensive error handling and resource cleanup mechanisms

## Usage Recommendations

1. **Monitoring Mode**: Suitable for production environments, automatically processes newly generated images
2. **Manual Mode**: Suitable for testing and debugging, allows precise control over processing flow
3. **Parameter Tuning**: Adjust Confidence, Pixel Confidence, and IoU thresholds according to actual needs
4. **Performance Optimization**: For higher processing speed, consider using CUDA execution provider

## Technical Architecture

- **Framework**: .NET 8.0 Windows Forms
- **Model Framework**: YoloDotNet v4.0
- **Execution Provider**: CPU (extendable to CUDA/OpenVINO)
- **Graphics Processing**: SkiaSharp 3.119.1
- **Asynchronous Processing**: System.Threading.Channels, async/await

## Important Notes

1. Ensure model files are in ONNX format (opset 17)
2. Monitoring directory structure should match expectations (Material Directory → Station Directory → Image Files)
3. Output directory requires write permissions
4. When processing large numbers of images, monitor disk space and memory usage
