// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace SegmentationDemo
{
    /// <summary>
    /// Demonstrates semantic segmentation on static images using the YoloDotNet library.
    /// 
    /// This demo loads a sample image, performs segmentation inference to detect pixel-level object masks,
    /// overlays the segmentation masks along with bounding boxes, labels, and confidence scores,
    /// and saves the annotated image to disk.
    /// 
    /// Key features showcased include:
    /// - Model initialization with flexible hardware options (CPU/GPU) and image preprocessing settings
    /// - Static image segmentation inference with adjustable confidence and mask thresholds
    /// - Comprehensive rendering options for segmentation masks, bounding boxes, labels, and confidence scores
    /// - Saving output images in a standard format with customizable compression
    /// - Console output of detected objects and their confidence levels
    /// - Automatic creation of an output folder on the desktop to store results
    /// 
    /// Execution providers:
    /// - CpuExecutionProvider: runs inference entirely on the CPU. Universally supported but slower.
    /// - CudaExecutionProvider: executes inference on an NVIDIA GPU using CUDA for accelerated performance.  
    ///   Optionally integrates with TensorRT for further optimization, supporting FP32, FP16, and INT8 precision modes.
    /// 
    /// Important notes:
    /// - Choose the execution provider based on your available hardware and performance requirements.
    /// - SegmentationDrawingOptions provides extensive customization for visual output,
    ///   including font styling, colors, opacity, and mask rendering.
    /// - Segmentation masks are drawn as pixel-level overlays, providing precise object outlines.
    /// - Tail visualization for tracking is supported but not enabled in this static image demo (see VideoStream demo).
    /// - For setup instructions and examples, see the README:  
    ///   https://github.com/NickSwardh/YoloDotNet
    /// </summary>
    internal class Program
    {
        private static string _outputFolder = default!;
        private static SegmentationDrawingOptions _drawingOptions = default!;

        static void Main(string[] args)
        {
            CreateOutputFolder();
            SetDrawingOptions();

            // Get paths to model and image directory
            // 支持命令行参数：第一个参数是模型路径，第二个参数是图片目录路径
            // 用法: dotnet run -- "模型路径" "图片目录路径"
            // 或者: SegmentationDemo.exe "模型路径" "图片目录路径"
            string modelPath;
            string imageDirectory;

            if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
            {
                // 如果提供了命令行参数，使用命令行参数
                modelPath = args[0];
                Console.WriteLine($"使用命令行参数指定的模型: {modelPath}");
            }
            else
            {
                // 否则使用默认的查找方法
                modelPath = GetModelPath();
            }

            if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                // 如果提供了命令行参数，使用命令行参数
                imageDirectory = args[1];
                Console.WriteLine($"使用命令行参数指定的图片目录: {imageDirectory}");
            }
            else
            {
                // 否则使用默认的查找方法
                imageDirectory = GetImageDirectory();
            }

            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"錯誤: 找不到模型文件: {modelPath}");
                Console.WriteLine("請確保模型文件存在於指定路徑，或使用命令行參數指定：");
                Console.WriteLine("  用法: dotnet run -- \"模型路徑\" \"圖片目錄路徑\"");
                Console.WriteLine("  例如: dotnet run -- \"test/assets/Models/你的模型.onnx\" \"test/assets/Media/sd900\"");
                return;
            }

            if (!Directory.Exists(imageDirectory))
            {
                Console.WriteLine($"錯誤: 找不到圖片目錄: {imageDirectory}");
                Console.WriteLine("請確保圖片目錄存在於指定路徑，或使用命令行參數指定：");
                Console.WriteLine("  用法: dotnet run -- \"模型路徑\" \"圖片目錄路徑\"");
                Console.WriteLine("  例如: dotnet run -- \"test/assets/Models/你的模型.onnx\" \"test/assets/Media/sd900\"");
                return;
            }

            Console.WriteLine($"使用模型: {modelPath}");
            Console.WriteLine($"使用圖片目錄: {imageDirectory}");

            // Initialize YoloDotNet.
            // YoloOptions configures the model, hardware settings, and image processing behavior.
            using var yolo = new Yolo(new YoloOptions
            {
                // Select execution provider (determines how and where inference is executed).
                // Available execution providers:
                // 
                //   - CpuExecutionProvider
                //     Runs inference entirely on the CPU. Universally supported on all hardware.
                //
                //   - CudaExecutionProvider
                //     Executes inference on an NVIDIA GPU using CUDA for accelerated performance.  
                //     Optionally integrates with TensorRT for further optimization, supporting FP32, FP16,  
                //     and INT8 precision modes. This delivers significant speed improvements on compatible GPUs.  
                //     See the TensorRT demo and documentation for detailed configuration and best practices.
                //
                //   - OpenVinoExecutionProvider
                //     Runs inference using Intel's OpenVINO toolkit for optimized performance on Intel hardware.
                //
                //   - CoreMLExecutionProvider
                //     Executes inference using Apple's CoreML framework for efficient performance on macOS and iOS devices.
                //
                //   Important:  
                //     - Choose the provider that matches your available hardware and performance requirements.  
                //     - If using CUDA with TensorRT enabled, ensure your environment has a compatible CUDA, cuDNN, and TensorRT setup.
                //     - For detailed setup instructions and examples, see the README:
                //
                //   More information about execution providers and setup instructions can be found in the README:
                //   https://github.com/NickSwardh/YoloDotNet

                ExecutionProvider = new CpuExecutionProvider(
                    // Path or byte[] of the ONNX model to load.
                    model: modelPath),

                // Resize mode applied before inference. Proportional maintains the aspect ratio (adds padding if needed),
                // while Stretch resizes the image to fit the target size without preserving the aspect ratio.
                // Set this accordingly, as it directly impacts the inference results.
                ImageResize = ImageResize.Stretched,

                // Sampling options for resizing; affects inference speed and quality.
                // For examples of other sampling options, see benchmarks: https://github.com/NickSwardh/YoloDotNet/tree/master/test/YoloDotNet.Benchmarks
                SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None) // YoloDotNet default
            });

            // Print model type
            Console.WriteLine($"Loaded ONNX Model: {yolo.ModelInfo}");
            Console.WriteLine();

            // 获取目录中所有图片文件
            var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };
            var imageFiles = new List<string>();
            foreach (var extension in imageExtensions)
            {
                imageFiles.AddRange(Directory.GetFiles(imageDirectory, extension, SearchOption.TopDirectoryOnly));
            }

            if (imageFiles.Count == 0)
            {
                Console.WriteLine($"錯誤: 在目錄 {imageDirectory} 中找不到圖片文件");
                return;
            }

            Console.WriteLine($"找到 {imageFiles.Count} 個圖片文件，開始處理...");
            Console.WriteLine(new string('=', 80));

            int processedCount = 0;
            int ngCount = 0;
            int okCount = 0;

            // 处理每个图片文件
            foreach (var imagePath in imageFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(imagePath);
                    var fileExtension = Path.GetExtension(imagePath);
                    var fileDirectory = Path.GetDirectoryName(imagePath);

                    Console.WriteLine($"處理: {Path.GetFileName(imagePath)}");

                    // Load input image as SKBitmap (or SKImage)
                    using var image = SKBitmap.Decode(imagePath);

                    // Run inference
                    var results = yolo.RunSegmentation(image, confidence: 0.24, pixelConfedence: 0.5, iou: 0.7);

                    // 根据检测结果确定后缀
                    string suffix;
                    if (results.Count > 0)
                    {
                        suffix = "NG";
                        ngCount++;
                        Console.WriteLine($"  -> 檢測到 {results.Count} 個目標，標記為 NG");
                    }
                    else
                    {
                        suffix = "OK";
                        okCount++;
                        Console.WriteLine($"  -> 未檢測到目標，標記為 OK");
                    }

                    // 生成新的文件名
                    var newFileName = $"{fileName}_{suffix}{fileExtension}";
                    var outputPath = Path.Combine(_outputFolder, newFileName);

                    // Draw results on image
                    image.Draw(results, _drawingOptions);

                    // Save image to output folder (根据原始文件格式保存)
                    var encodedFormat = GetEncodedFormat(fileExtension);
                    image.Save(outputPath, encodedFormat, 80);

                    processedCount++;
                    Console.WriteLine($"  -> 已保存到: {outputPath}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  -> 錯誤: 處理 {Path.GetFileName(imagePath)} 時發生異常: {ex.Message}");
                    Console.WriteLine();
                }
            }

            // 输出统计信息
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"處理完成！");
            Console.WriteLine($"總共處理: {processedCount} 個文件");
            Console.WriteLine($"NG (檢測到目標): {ngCount} 個");
            Console.WriteLine($"OK (未檢測到目標): {okCount} 個");
            Console.WriteLine($"結果保存在: {_outputFolder}");

            DisplayOutputFolder();
        }

        private static void SetDrawingOptions()
        {
            // Set options for drawing
            _drawingOptions = new SegmentationDrawingOptions
            {
                DrawBoundingBoxes = true,
                DrawConfidenceScore = true,
                DrawLabels = true,
                EnableFontShadow = true,

                // SKTypeface defines the font used for text rendering.
                // SKTypeface.Default uses the system default font.
                // To load a custom font:
                //   - Use SKTypeface.FromFamilyName("fontFamilyName", SKFontStyle) to load by font family name (if installed).
                //   - Use SKTypeface.FromFile("path/to/font.ttf") to load a font directly from a file.
                // Example:
                //   Font = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
                //   Font = SKTypeface.FromFile("C:\\Fonts\\CustomFont.ttf")
                Font = SKTypeface.Default,

                FontSize = 18,
                FontColor = SKColors.White,
                DrawLabelBackground = true,
                EnableDynamicScaling = true,
                BorderThickness = 2,

                // By default, YoloDotNet automatically assigns colors to bounding boxes.
                // To override these default colors, you can define your own array of hexadecimal color codes.
                // Each element in the array corresponds to the class index in your model.
                // Example:
                //   BoundingBoxHexColors = ["#00ff00", "#547457", ...] // Color per class id

                BoundingBoxOpacity = 128,
                DrawSegmentationPixelMask = true

                // The following options configure tracked object tails, which visualize 
                // the movement path of detected objects across a sequence of frames or images.
                // Drawing the tail only works when tracking is enabled (e.g., using SortTracker).
                // This is demonstrated in the VideoStream demo.

                // DrawTrackedTail = false,
                // TailPaintColorEnd = new(),
                // ailPaintColorStart = new(),
                // TailThickness = 0,
            };
        }

        private static void PrintResults(List<Segmentation> results)
        {
            Console.WriteLine();
            Console.WriteLine($"Inference Results: {results.Count} objects");
            Console.WriteLine(new string('=', 80));

            Console.ForegroundColor = ConsoleColor.Blue;

            foreach (var result in results)
            {
                var label = result.Label.Name;
                var confidence = (result.Confidence * 100).ToString("0.##", CultureInfo.InvariantCulture);
                Console.WriteLine($"{label} ({confidence}%)");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void CreateOutputFolder()
        {
            //_outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "YoloDotNet_Results");
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Industry_Results");

            if (Directory.Exists(_outputFolder) is false)
                Directory.CreateDirectory(_outputFolder);
        }

        private static void DisplayOutputFolder()
        {
            var shell = OperatingSystem.IsWindows() ? "explorer"
                     : OperatingSystem.IsLinux() ? "xdg-open"
                     : OperatingSystem.IsMacOS() ? "open"
                     : null;

            if (shell is not null)
                Process.Start(shell, _outputFolder);
            else
                Console.WriteLine($"Results saved to: {_outputFolder}");
        }

        /// <summary>
        /// 獲取模型文件路徑
        /// 優先使用 sd900.onnx 模型
        /// </summary>
        private static string GetModelPath()
        {
            // 嘗試從當前執行目錄向上查找項目根目錄
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectRoot = FindProjectRoot(currentDir);

            if (projectRoot != null)
            {
                // 優先使用 sd900.onnx 模型
                var sd900Model = Path.Combine(projectRoot, "test", "assets", "Models", "sd900.onnx");
                if (File.Exists(sd900Model))
                    return sd900Model;

                // 如果不存在，嘗試 YOLOv11 分割模型（備用）
                var v11Model = Path.Combine(projectRoot, "test", "assets", "Models", "yolov11s-seg.onnx");
                if (File.Exists(v11Model))
                    return v11Model;

                // 如果不存在，嘗試 YOLOv8 分割模型（備用）
                var v8Model = Path.Combine(projectRoot, "test", "assets", "Models", "yolov8s-seg.onnx");
                if (File.Exists(v8Model))
                    return v8Model;
            }

            // 如果找不到，返回默認路徑
            return Path.Combine("test", "assets", "Models", "sd900.onnx");
        }

        /// <summary>
        /// 獲取測試圖片目錄路徑
        /// </summary>
        private static string GetImageDirectory()
        {
            // 嘗試從當前執行目錄向上查找項目根目錄
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectRoot = FindProjectRoot(currentDir);

            if (projectRoot != null)
            {
                // 優先使用 sd900 目錄
                var sd900Dir = Path.Combine(projectRoot, "test", "assets", "Media", "sd900");
                if (Directory.Exists(sd900Dir))
                    return sd900Dir;

                // 如果不存在，嘗試 Media 目錄（備用）
                var mediaDir = Path.Combine(projectRoot, "test", "assets", "Media");
                if (Directory.Exists(mediaDir))
                    return mediaDir;
            }

            // 如果找不到，返回默認路徑
            return Path.Combine("test", "assets", "Media", "sd900");
        }

        /// <summary>
        /// 查找項目根目錄（包含 .git 或 .sln 文件的目錄）
        /// </summary>
        private static string? FindProjectRoot(DirectoryInfo? dir)
        {
            while (dir != null)
            {
                // 檢查是否存在 .git 或 .sln 文件（項目根目錄標記）
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, "YoloDotNet.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// 根據文件擴展名獲取 SKEncodedImageFormat
        /// </summary>
        private static SKEncodedImageFormat GetEncodedFormat(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".gif" => SKEncodedImageFormat.Gif,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg // 默認使用 JPEG
            };
        }
    }
}
