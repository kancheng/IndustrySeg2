# 分割示例 (Segmentation Demo)

这是一个基于 YoloDotNet 的图像分割示例，可以独立运行，无需依赖测试项目。

## 功能特点

- ✅ **独立运行**：不依赖 `YoloDotNet.Test.Common` 项目
- ✅ **自动路径查找**：自动查找项目根目录下的模型和图片文件
- ✅ **CPU 执行**：使用 `CpuExecutionProvider`，适用于所有硬件
- ✅ **完整的分割功能**：支持像素级分割掩码、边界框、标签和置信度显示

## 运行要求

1. 确保模型文件存在于 `test/assets/Models/` 目录下：
   - `yolov11s-seg.onnx`（优先使用）
   - 或 `yolov8s-seg.onnx`（备用）

2. 确保测试图片存在于 `test/assets/Media/` 目录下：
   - `people.jpg`（优先使用）
   - 或 `street.jpg`（备用）

## 运行方式

### 方式 1：使用 dotnet CLI

```bash
cd Demo/SegmentationDemo
dotnet run
```

### 方式 2：使用 Visual Studio

1. 打开 `YoloDotNet.sln`
2. 将 `SegmentationDemo` 设为启动项目
3. 按 F5 运行

## 输出结果

程序运行后：
- 结果图片会保存到桌面的 `YoloDotNet_Results` 文件夹
- 控制台会显示检测到的对象数量和置信度
- 会自动打开结果文件夹（如果支持）

## 配置说明

### 执行提供者

当前示例使用 `CpuExecutionProvider`，适用于所有硬件。如果需要使用 GPU 加速，可以：

1. 修改 `SegmentationDemo.csproj`，将 `YoloDotNet.ExecutionProvider.Cpu` 替换为 `YoloDotNet.ExecutionProvider.Cuda`
2. 修改 `Program.cs`，将 `CpuExecutionProvider` 替换为 `CudaExecutionProvider`，并添加 GPU 设备 ID

### 分割参数

在 `Main` 方法中可以调整以下参数：

```csharp
var results = yolo.RunSegmentation(
    image, 
    confidence: 0.24,      // 置信度阈值（0-1）
    pixelConfedence: 0.5,  // 像素置信度阈值（0-1）
    iou: 0.7               // IoU 阈值（0-1）
);
```

### 绘制选项

在 `SetDrawingOptions` 方法中可以自定义：
- 边界框样式和颜色
- 标签字体和大小
- 分割掩码透明度
- 置信度显示格式

## 示例输出

```
使用模型: D:\...\test\assets\Models\yolov11s-seg.onnx
使用圖片: D:\...\test\assets\Media\people.jpg
Loaded ONNX Model: Segmentation (yolo v11)

Inference Results: 19 objects
================================================================================
person (86.9%)
person (86.59%)
person (85.83%)
...
```

## 注意事项

- 首次运行可能需要下载 ONNX Runtime 的本地依赖
- 模型文件较大，确保有足够的磁盘空间
- CPU 推理速度较慢，如需实时处理建议使用 GPU 加速

