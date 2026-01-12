# YoloDotNet 分割示例演示文档

## 📋 项目概述

本项目基于 **YoloDotNet** 库实现了一个完整的图像分割示例。YoloDotNet 是一个高性能的 C# 库，支持 YOLOv5-v12 模型，可以进行目标检测、分割、分类、姿态估计等任务。

## 🎯 演示目标

完成一个**可独立运行**的图像分割示例，展示如何使用 YoloDotNet 进行像素级图像分割。

---

## 📊 项目分析

### 原始项目结构

```
YoloDotNet/
├── Demo/
│   └── SegmentationDemo/          # 原始分割示例
│       ├── Program.cs
│       └── SegmentationDemo.csproj
├── test/
│   └── assets/
│       ├── Models/                 # ONNX 模型文件
│       └── Media/                  # 测试图片
└── YoloDotNet/                     # 核心库
```

### 原始代码的问题

1. ❌ **依赖测试项目**：需要引用 `YoloDotNet.Test.Common` 来获取模型和图片路径
2. ❌ **使用 CUDA**：默认使用 `CudaExecutionProvider`，需要 NVIDIA GPU
3. ❌ **路径硬编码**：通过 `SharedConfig` 获取路径，不够灵活

---

## 🔧 修改内容

### 1. 移除测试项目依赖

**修改前** (`SegmentationDemo.csproj`)：
```xml
<ItemGroup>
  <ProjectReference Include="..\..\test\YoloDotNet.Test.Common\YoloDotNet.Test.Common.csproj" />
  <ProjectReference Include="..\..\YoloDotNet.ExecutionProvider.Cuda\YoloDotNet.ExecutionProvider.Cuda.csproj" />
  <ProjectReference Include="..\..\YoloDotNet\YoloDotNet.csproj" />
</ItemGroup>
```

**修改后**：
```xml
<ItemGroup>
  <ProjectReference Include="..\..\YoloDotNet.ExecutionProvider.Cpu\YoloDotNet.ExecutionProvider.Cpu.csproj" />
  <ProjectReference Include="..\..\YoloDotNet\YoloDotNet.csproj" />
</ItemGroup>
```

**改进点**：
- ✅ 移除了对测试项目的依赖
- ✅ 改用 CPU 执行提供者，适用于所有硬件

---

### 2. 实现智能路径查找

**新增功能**：自动查找项目根目录下的模型和图片文件

```csharp
/// <summary>
/// 獲取模型文件路徑
/// 優先使用 YOLOv11 分割模型，如果不存在則嘗試 YOLOv8
/// </summary>
private static string GetModelPath()
{
    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
    var projectRoot = FindProjectRoot(currentDir);

    if (projectRoot != null)
    {
        // 優先使用 YOLOv11 分割模型
        var v11Model = Path.Combine(projectRoot, "test", "assets", "Models", "yolov11s-seg.onnx");
        if (File.Exists(v11Model))
            return v11Model;

        // 如果不存在，嘗試 YOLOv8 分割模型
        var v8Model = Path.Combine(projectRoot, "test", "assets", "Models", "yolov8s-seg.onnx");
        if (File.Exists(v8Model))
            return v8Model;
    }

    return Path.Combine("test", "assets", "Models", "yolov11s-seg.onnx");
}
```

**改进点**：
- ✅ 自动查找项目根目录（通过 `.git` 或 `.sln` 文件）
- ✅ 支持多个模型版本（优先 YOLOv11，备用 YOLOv8）
- ✅ 支持多个测试图片（优先 `people.jpg`，备用 `street.jpg`）

---

### 3. 改用 CPU 执行提供者

**修改前**：
```csharp
ExecutionProvider = new CudaExecutionProvider(
    model: SharedConfig.GetTestModelV11(ModelType.Segmentation),
    gpuId: 0),
```

**修改后**：
```csharp
ExecutionProvider = new CpuExecutionProvider(
    model: modelPath),
```

**改进点**：
- ✅ 适用于所有硬件（无需 GPU）
- ✅ 更简单的配置
- ✅ 更易于演示和测试

---

### 4. 添加错误处理

```csharp
if (!File.Exists(modelPath))
{
    Console.WriteLine($"錯誤: 找不到模型文件: {modelPath}");
    Console.WriteLine("請確保模型文件存在於 test/assets/Models/ 目錄下");
    return;
}

if (!File.Exists(imagePath))
{
    Console.WriteLine($"錯誤: 找不到圖片文件: {imagePath}");
    Console.WriteLine("請確保圖片文件存在於 test/assets/Media/ 目錄下");
    return;
}
```

**改进点**：
- ✅ 友好的错误提示
- ✅ 提前检查文件是否存在
- ✅ 避免运行时崩溃

---

## 🚀 使用方法

### 前置要求

1. **模型文件**：确保以下文件存在
   - `test/assets/Models/yolov11s-seg.onnx` 或 `yolov8s-seg.onnx`

2. **测试图片**：确保以下文件存在
   - `test/assets/Media/people.jpg` 或 `street.jpg`

### 运行步骤

#### 方式 1：命令行运行

```bash
# 进入项目目录
cd Demo/SegmentationDemo

# 构建项目
dotnet build

# 运行程序
dotnet run
```

#### 方式 2：Visual Studio

1. 打开 `YoloDotNet.sln`
2. 将 `SegmentationDemo` 设为启动项目
3. 按 `F5` 运行

---

## 📸 运行结果

### 控制台输出

```
使用模型: D:\CSHARPAICV\YoloDotNet_RAW\YoloDotNet\test\assets\Models\yolov11s-seg.onnx
使用圖片: D:\CSHARPAICV\YoloDotNet_RAW\YoloDotNet\test\assets\Media\people.jpg
Loaded ONNX Model: Segmentation (yolo v11)

Inference Results: 19 objects
================================================================================
person (86.9%)
person (86.59%)
person (85.83%)
person (85.03%)
person (84.02%)
person (81.76%)
person (76.47%)
person (75.53%)
person (74.83%)
person (72.72%)
person (72.63%)
person (66.77%)
person (63.15%)
person (54.71%)
person (53.18%)
person (47.44%)
person (46.43%)
person (37.65%)
person (33.89%)
```

### 输出文件

- **位置**：`桌面/YoloDotNet_Results/Segmentation.jpg`
- **内容**：带有分割掩码、边界框、标签和置信度的标注图片

---

## 🎨 功能特点

### 1. 像素级分割

程序会对图像中的每个对象进行像素级分割，生成精确的掩码。

### 2. 可视化选项

```csharp
_drawingOptions = new SegmentationDrawingOptions
{
    DrawBoundingBoxes = true,          // 绘制边界框
    DrawConfidenceScore = true,        // 显示置信度
    DrawLabels = true,                 // 显示标签
    DrawSegmentationPixelMask = true,  // 绘制分割掩码
    BoundingBoxOpacity = 128,          // 边界框透明度
    FontSize = 18,                     // 字体大小
    // ... 更多选项
};
```

### 3. 可调参数

```csharp
var results = yolo.RunSegmentation(
    image, 
    confidence: 0.24,      // 置信度阈值（0-1）
    pixelConfedence: 0.5, // 像素置信度阈值（0-1）
    iou: 0.7              // IoU 阈值（0-1）
);
```

---

## 📈 性能说明

### CPU vs GPU

| 执行提供者 | 硬件要求 | 推理速度 | 适用场景 |
|-----------|---------|---------|---------|
| CPU | 所有硬件 | 较慢 | 开发、测试、演示 |
| CUDA | NVIDIA GPU | 快 | 生产环境、实时处理 |

### 当前配置

- **执行提供者**：CPU（适用于所有硬件）
- **模型**：YOLOv11s-seg（小型模型，速度快）
- **推理时间**：约 1-3 秒（取决于 CPU 性能）

---

## 🔍 技术细节

### 使用的技术栈

- **.NET 8.0**：最新的 .NET 框架
- **YoloDotNet**：YOLO 模型的 C# 封装
- **ONNX Runtime**：模型推理引擎
- **SkiaSharp**：图像处理和绘制

### 模型信息

- **模型类型**：YOLOv11 Segmentation
- **输入尺寸**：640x640
- **输出**：边界框 + 分割掩码
- **类别数**：80（COCO 数据集）

### 图像预处理

```csharp
ImageResize = ImageResize.Stretched,  // 拉伸模式
SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
```

---

## 💡 扩展建议

### 1. 使用 GPU 加速

如果需要更快的推理速度，可以切换到 CUDA：

```csharp
// 修改 csproj 文件
<ProjectReference Include="..\..\YoloDotNet.ExecutionProvider.Cuda\..." />

// 修改 Program.cs
ExecutionProvider = new CudaExecutionProvider(
    model: modelPath,
    gpuId: 0  // GPU 设备 ID
),
```

### 2. 批量处理

可以修改代码处理多张图片：

```csharp
var imageFiles = Directory.GetFiles(imageDirectory, "*.jpg");
foreach (var imageFile in imageFiles)
{
    using var image = SKBitmap.Decode(imageFile);
    var results = yolo.RunSegmentation(image, 0.24, 0.5, 0.7);
    // ... 处理结果
}
```

### 3. 视频处理

可以参考 `VideoStreamDemo` 实现视频分割。

---

## ✅ 验证清单

- [x] 项目可以独立运行（不依赖测试项目）
- [x] 自动查找模型和图片文件
- [x] 使用 CPU 执行提供者（适用于所有硬件）
- [x] 完整的错误处理
- [x] 成功检测和分割对象
- [x] 输出结果图片到桌面
- [x] 控制台显示检测结果

---

## 📝 总结

通过本次修改，我们成功创建了一个：

1. ✅ **独立运行**的分割示例
2. ✅ **易于使用**的演示程序
3. ✅ **跨平台兼容**的解决方案（CPU 执行提供者）
4. ✅ **智能路径查找**的自动化流程

这个示例可以作为：
- 🎓 学习 YoloDotNet 的入门教程
- 🎯 演示图像分割功能的工具
- 🚀 进一步开发的基础模板

---

## 📚 相关资源

- **YoloDotNet GitHub**：https://github.com/NickSwardh/YoloDotNet
- **YOLO 官方文档**：https://docs.ultralytics.com/
- **ONNX 模型导出**：https://docs.ultralytics.com/modes/export/

---

**演示日期**：2025年  
**版本**：v1.0  
**状态**：✅ 已完成并验证

