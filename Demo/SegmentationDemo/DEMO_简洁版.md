# YoloDotNet 分割示例 - 快速演示

## 🎯 演示目标

展示如何使用 YoloDotNet 完成图像分割任务，检测并分割图像中的对象。

---

## 📋 修改摘要

### 主要改进

1. ✅ **移除测试依赖** - 项目可独立运行
2. ✅ **智能路径查找** - 自动定位模型和图片文件
3. ✅ **CPU 执行提供者** - 适用于所有硬件，无需 GPU
4. ✅ **完善错误处理** - 友好的错误提示

---

## 🔧 关键修改

### 1. 项目依赖 (`SegmentationDemo.csproj`)

```diff
- <ProjectReference Include="..\..\test\YoloDotNet.Test.Common\..." />
- <ProjectReference Include="..\..\YoloDotNet.ExecutionProvider.Cuda\..." />
+ <ProjectReference Include="..\..\YoloDotNet.ExecutionProvider.Cpu\..." />
```

### 2. 执行提供者 (`Program.cs`)

```diff
- ExecutionProvider = new CudaExecutionProvider(
-     model: SharedConfig.GetTestModelV11(ModelType.Segmentation),
-     gpuId: 0),
+ ExecutionProvider = new CpuExecutionProvider(
+     model: modelPath),
```

### 3. 新增功能

- ✅ 自动查找项目根目录
- ✅ 支持多模型版本（YOLOv11 / YOLOv8）
- ✅ 文件存在性检查

---

## 🚀 快速开始

### 运行命令

```bash
cd Demo/SegmentationDemo
dotnet run
```

### 预期输出

```
使用模型: ...\yolov11s-seg.onnx
使用圖片: ...\people.jpg
Loaded ONNX Model: Segmentation (yolo v11)

Inference Results: 19 objects
================================================================================
person (86.9%)
person (86.59%)
...
```

### 输出文件

- 📁 位置：`桌面/YoloDotNet_Results/Segmentation.jpg`
- 🖼️ 内容：带分割掩码、边界框、标签的标注图片

---

## 📊 运行结果

| 项目 | 结果 |
|------|------|
| 检测对象数 | 19 个 |
| 主要类别 | person |
| 置信度范围 | 33.89% - 86.9% |
| 执行时间 | ~1-3 秒（CPU） |
| 输出文件 | ✅ 已生成 |

---

## 💡 核心代码

### 初始化模型

```csharp
using var yolo = new Yolo(new YoloOptions
{
    ExecutionProvider = new CpuExecutionProvider(model: modelPath),
    ImageResize = ImageResize.Stretched,
    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
});
```

### 运行分割

```csharp
var results = yolo.RunSegmentation(
    image, 
    confidence: 0.24,      // 置信度阈值
    pixelConfedence: 0.5,  // 像素置信度
    iou: 0.7               // IoU 阈值
);
```

### 绘制结果

```csharp
image.Draw(results, _drawingOptions);
image.Save(fileName, SKEncodedImageFormat.Jpeg, 80);
```

---

## ✅ 验证结果

- [x] 项目构建成功
- [x] 程序运行成功
- [x] 检测到 19 个对象
- [x] 输出图片已生成
- [x] 控制台显示结果

---

## 🎨 功能展示

### 分割功能

- ✅ **像素级分割** - 精确的对象掩码
- ✅ **边界框** - 对象定位
- ✅ **标签显示** - 类别名称
- ✅ **置信度** - 检测可信度

### 可视化选项

```csharp
DrawBoundingBoxes = true          // 边界框
DrawConfidenceScore = true        // 置信度
DrawLabels = true                 // 标签
DrawSegmentationPixelMask = true  // 分割掩码
```

---

## 📈 性能对比

| 执行提供者 | 硬件要求 | 速度 | 适用场景 |
|-----------|---------|------|---------|
| **CPU** | 所有硬件 | 较慢 | ✅ 演示、开发 |
| CUDA | NVIDIA GPU | 快 | 生产环境 |

**当前配置**：CPU（适用于所有硬件）

---

## 🔍 技术栈

- **.NET 8.0** - 开发框架
- **YoloDotNet** - YOLO 模型封装
- **ONNX Runtime** - 模型推理
- **SkiaSharp** - 图像处理

---

## 📝 总结

✅ **独立运行** - 无需额外依赖  
✅ **易于使用** - 一键运行  
✅ **跨平台** - CPU 执行提供者  
✅ **完整功能** - 像素级分割 + 可视化  

**状态**：✅ 已完成并验证

---

**演示文档** | 2025年 | v1.0

