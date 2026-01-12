# IndustrySegmentationDemo - 工业检测分割检测系统

基于 SegmentationDemo 的图形界面版本，提供友好的用户界面进行工业检测分割任务。

## 功能特性

- **图形界面操作**：提供直观的 WPF 图形界面，无需命令行操作
- **单文件/批量处理**：支持单张图片检测和批量图片处理
- **参数可调**：可实时调整 Confidence、Pixel Confidence 和 IoU 参数
- **实时预览**：处理结果实时显示在界面上
- **统计信息**：显示总处理数、NG（检测到目标）和 OK（未检测到目标）数量
- **进度显示**：批量处理时显示处理进度
- **日志输出**：详细的操作日志记录

## 使用方法

1. **选择模型文件**：点击"浏览..."按钮选择 ONNX 模型文件（如 sd900.onnx）
2. **选择图片**：
   - 单文件模式：选择单张图片文件
   - 批量处理模式：选择包含图片的目录
3. **选择输出目录**：选择结果保存的目录（默认为桌面上的 Industry_Results 文件夹）
4. **调整参数**（可选）：
   - Confidence：检测置信度阈值（默认 0.24）
   - Pixel Confidence：像素置信度阈值（默认 0.5）
   - IoU：交并比阈值（默认 0.7）
5. **开始检测**：点击"开始检测"按钮开始处理
6. **查看结果**：处理完成后，结果会显示在预览区域，并保存到输出目录

## 结果说明

- **NG**：检测到目标，表示可能存在缺陷或异常
- **OK**：未检测到目标，表示正常

结果文件名格式：`原文件名_NG.png` 或 `原文件名_OK.png`

## 技术栈

- .NET 8.0
- WPF (Windows Presentation Foundation)
- SkiaSharp (图像处理和显示)
- YoloDotNet (YOLO 模型推理)

## 项目结构

```
IndustrySegmentationDemo/
├── App.xaml                 # 应用程序定义
├── App.xaml.cs             # 应用程序代码
├── MainWindow.xaml         # 主窗口界面
├── MainWindow.xaml.cs      # 主窗口逻辑
├── AssemblyInfo.cs         # 程序集信息
└── IndustrySegmentationDemo.csproj  # 项目文件
```

## 依赖项

- YoloDotNet
- YoloDotNet.ExecutionProvider.Cpu
- SkiaSharp.Views.WPF
- System.Windows.Forms

## 注意事项

- 首次运行时会自动查找项目根目录下的默认模型和测试图片
- 批量处理时，处理结果会实时更新显示
- 可以随时点击"停止检测"按钮取消正在进行的处理
- 处理大量图片时，建议使用 GPU 加速（需要修改代码使用 CudaExecutionProvider）

