// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using Microsoft.Win32;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;
using WinForms = System.Windows.Forms;

namespace IndustrySegmentationDemo
{
    /// <summary>
    /// 工业检测分割检测系统 - 图形界面版本
    /// 基于 SegmentationDemo 的功能，提供图形界面操作
    /// </summary>
    public partial class MainWindow : Window
    {
        private Yolo? _yolo;
        private SegmentationDrawingOptions _drawingOptions = default!;
        private SKBitmap? _currentResultBitmap;
        private List<SKBitmap> _resultBitmaps = new List<SKBitmap>(); // 保存所有处理过的图片
        private int _currentImageIndex = -1; // 当前显示的图片索引
        private Dispatcher _dispatcher;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalCount = 0;
        private int _ngCount = 0;
        private int _okCount = 0;
        private string _outputFolder = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            _dispatcher = Dispatcher.CurrentDispatcher;
            InitializeDrawingOptions();
            InitializeDefaultPaths();
        }

        private void InitializeDrawingOptions()
        {
            _drawingOptions = new SegmentationDrawingOptions
            {
                DrawBoundingBoxes = true,
                DrawConfidenceScore = true,
                DrawLabels = true,
                EnableFontShadow = true,
                Font = SKTypeface.Default,
                FontSize = 18,
                FontColor = SKColors.White,
                DrawLabelBackground = true,
                EnableDynamicScaling = true,
                BorderThickness = 2,
                BoundingBoxOpacity = 128,
                DrawSegmentationPixelMask = true
            };
        }

        private void InitializeDefaultPaths()
        {
            // 设置默认输出目录
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Industry_Results");
            OutputPathTextBox.Text = _outputFolder;

            // 尝试查找默认模型路径
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectRoot = FindProjectRoot(currentDir);
            if (projectRoot != null)
            {
                var sd900Model = Path.Combine(projectRoot, "test", "assets", "Models", "sd900.onnx");
                if (File.Exists(sd900Model))
                {
                    ModelPathTextBox.Text = sd900Model;
                }
            }
        }

        private string? FindProjectRoot(DirectoryInfo? dir)
        {
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, "YoloDotNet.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private void BrowseModelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "ONNX模型文件 (*.onnx)|*.onnx|所有文件 (*.*)|*.*",
                Title = "选择模型文件"
            };

            if (dialog.ShowDialog() == true)
            {
                ModelPathTextBox.Text = dialog.FileName;
                AddLog($"已选择模型: {dialog.FileName}");
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (SingleFileRadio.IsChecked == true)
            {
                // 单文件模式
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件 (*.*)|*.*",
                    Title = "选择图片文件"
                };

                if (dialog.ShowDialog() == true)
                {
                    ImagePathTextBox.Text = dialog.FileName;
                    AddLog($"已选择图片: {dialog.FileName}");
                }
            }
            else
            {
                // 批量处理模式
                var dialog = new WinForms.FolderBrowserDialog
                {
                    Description = "选择图片目录"
                };

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    ImagePathTextBox.Text = dialog.SelectedPath;
                    AddLog($"已选择图片目录: {dialog.SelectedPath}");
                }
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "选择输出目录"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                OutputPathTextBox.Text = dialog.SelectedPath;
                _outputFolder = dialog.SelectedPath;
                AddLog($"已选择输出目录: {dialog.SelectedPath}");
            }
        }

        private void ConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ConfidenceValue != null)
            {
                ConfidenceValue.Text = e.NewValue.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private void PixelConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PixelConfidenceValue != null)
            {
                PixelConfidenceValue.Text = e.NewValue.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private void IouSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IouValue != null)
            {
                IouValue.Text = e.NewValue.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(ModelPathTextBox.Text) || !File.Exists(ModelPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("请选择有效的模型文件！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("请选择图片文件或目录！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (SingleFileRadio.IsChecked == true && !File.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("请选择有效的图片文件！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (BatchFileRadio.IsChecked == true && !Directory.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("请选择有效的图片目录！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("请选择输出目录！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 创建输出目录
            _outputFolder = OutputPathTextBox.Text;
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // 初始化 Yolo
            try
            {
                AddLog("正在初始化模型...");
                StatusText.Text = "正在初始化模型...";

                _yolo?.Dispose();
                _yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(model: ModelPathTextBox.Text),
                    ImageResize = ImageResize.Stretched,
                    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                });

                AddLog($"模型加载成功: {_yolo.ModelInfo}");
                StatusText.Text = "模型加载成功";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"模型初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"模型初始化失败: {ex.Message}");
                return;
            }

            // 重置统计信息和图片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/启用按钮
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;
            
            // 隐藏切换控制栏
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Collapsed;

            // 创建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 在进入后台线程之前获取参数值（避免线程访问错误）
            var confidence = ConfidenceSlider.Value;
            var pixelConfidence = PixelConfidenceSlider.Value;
            var iou = IouSlider.Value;

            // 开始处理
            try
            {
                if (SingleFileRadio.IsChecked == true)
                {
                    await ProcessSingleFile(ImagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
                }
                else
                {
                    await ProcessBatchFiles(ImagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("处理已取消");
                StatusText.Text = "处理已取消";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"处理过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"错误: {ex.Message}");
            }
            finally
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "就绪";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在停止处理...");
            StatusText.Text = "正在停止...";
        }

        private async Task ProcessSingleFile(string imagePath, double confidence, double pixelConfidence, double iou, CancellationToken cancellationToken)
        {
            _totalCount = 1;
            _dispatcher.Invoke(() =>
            {
                UpdateStatistics();
                ProgressBar.Maximum = 1;
                ProgressBar.Value = 0;
            });

            await Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(imagePath);
                    _dispatcher.Invoke(() =>
                    {
                        CurrentFileText.Text = fileName;
                        AddLog($"处理: {fileName}");
                        StatusText.Text = $"正在处理: {fileName}";
                    });

                    // 加载图片
                    using var image = SKBitmap.Decode(imagePath);
                    if (image == null)
                    {
                        throw new Exception($"无法加载图片: {imagePath}");
                    }

                    // 运行检测（使用传入的参数，而不是从 UI 控件获取）
                    var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);

                    // 确定结果
                    string suffix;
                    if (results.Count > 0)
                    {
                        _ngCount++;
                        suffix = "NG";
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  -> 检测到 {results.Count} 个目标，标记为 NG");
                        });
                    }
                    else
                    {
                        _okCount++;
                        suffix = "OK";
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  -> 未检测到目标，标记为 OK");
                        });
                    }

                    // 绘制结果
                    image.Draw(results, _drawingOptions);

                    // 保存结果
                    var fileExtension = Path.GetExtension(imagePath);
                    var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                    var outputPath = Path.Combine(_outputFolder, newFileName);

                    var encodedFormat = GetEncodedFormat(fileExtension);
                    image.Save(outputPath, encodedFormat, 80);

                    // 更新显示
                    var copiedBitmap = image.Copy();
                    _dispatcher.Invoke(() =>
                    {
                        // 添加到图片列表
                        _resultBitmaps.Add(copiedBitmap);
                        _currentImageIndex = _resultBitmaps.Count - 1;
                        
                        // 显示当前图片
                        ShowImageAtIndex(_currentImageIndex);
                        
                        // 隐藏"暂无图片"提示
                        NoImageText.Visibility = Visibility.Collapsed;
                        
                        // 更新切换控制栏
                        UpdateImageNavigation();
                        
                        AddLog($"  -> 已保存到: {outputPath}");
                        UpdateStatistics();
                        ProgressBar.Value = 1;
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"  -> 错误: {ex.Message}");
                    });
                }
            }, cancellationToken);
        }

        private async Task ProcessBatchFiles(string imageDirectory, double confidence, double pixelConfidence, double iou, CancellationToken cancellationToken)
        {
            // 获取所有图片文件
            var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };
            var imageFiles = new List<string>();
            foreach (var extension in imageExtensions)
            {
                imageFiles.AddRange(Directory.GetFiles(imageDirectory, extension, SearchOption.TopDirectoryOnly));
            }

            if (imageFiles.Count == 0)
            {
                _dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"在目录 {imageDirectory} 中找不到图片文件！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return;
            }

            _totalCount = imageFiles.Count;
            _dispatcher.Invoke(() =>
            {
                UpdateStatistics();
                ProgressBar.Maximum = imageFiles.Count;
                ProgressBar.Value = 0;
            });

            int processedCount = 0;

            await Task.Run(() =>
            {
                foreach (var imagePath in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(imagePath);
                        _dispatcher.Invoke(() =>
                        {
                            CurrentFileText.Text = fileName;
                            AddLog($"处理: {fileName}");
                            StatusText.Text = $"正在处理: {fileName} ({processedCount + 1}/{imageFiles.Count})";
                        });

                        // 加载图片
                        using var image = SKBitmap.Decode(imagePath);
                        if (image == null)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 错误: 无法加载图片");
                            });
                            continue;
                        }

                        // 运行检测（使用传入的参数，而不是从 UI 控件获取）
                        var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);

                        // 确定结果
                        string suffix;
                        if (results.Count > 0)
                        {
                            _ngCount++;
                            suffix = "NG";
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 检测到 {results.Count} 个目标，标记为 NG");
                            });
                        }
                        else
                        {
                            _okCount++;
                            suffix = "OK";
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 未检测到目标，标记为 OK");
                            });
                        }

                        // 绘制结果
                        image.Draw(results, _drawingOptions);

                        // 保存结果
                        var fileExtension = Path.GetExtension(imagePath);
                        var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                        var outputPath = Path.Combine(_outputFolder, newFileName);

                        var encodedFormat = GetEncodedFormat(fileExtension);
                        image.Save(outputPath, encodedFormat, 80);

                        processedCount++;

                        // 更新显示
                        var copiedBitmap = image.Copy();
                        _dispatcher.Invoke(() =>
                        {
                            // 添加到图片列表
                            _resultBitmaps.Add(copiedBitmap);
                            _currentImageIndex = _resultBitmaps.Count - 1;
                            
                            // 显示当前图片（显示最后一张）
                            ShowImageAtIndex(_currentImageIndex);
                            
                            // 隐藏"暂无图片"提示
                            NoImageText.Visibility = Visibility.Collapsed;
                            
                            // 更新切换控制栏
                            UpdateImageNavigation();
                            
                            AddLog($"  -> 已保存到: {outputPath}");
                            UpdateStatistics();
                            ProgressBar.Value = processedCount;
                            ProgressText.Text = $"{processedCount} / {imageFiles.Count}";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  -> 错误: 处理 {Path.GetFileName(imagePath)} 时发生异常: {ex.Message}");
                        });
                    }
                }
            }, cancellationToken);

            _dispatcher.Invoke(() =>
            {
                AddLog($"处理完成！总共处理: {processedCount} 个文件");
                StatusText.Text = "处理完成";
            });
        }

        private void UpdateStatistics()
        {
            TotalCountText.Text = _totalCount.ToString();
            NgCountText.Text = _ngCount.ToString();
            OkCountText.Text = _okCount.ToString();
        }

        private void AddLog(string message)
        {
            _dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private SKEncodedImageFormat GetEncodedFormat(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".gif" => SKEncodedImageFormat.Gif,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };
        }

        private void ResultImageElement_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

            // 安全地获取当前图片的引用
            SKBitmap? bitmap = null;
            try
            {
                bitmap = _currentResultBitmap;
                if (bitmap == null)
                {
                    return;
                }
            }
            catch
            {
                // 如果访问失败，直接返回
                return;
            }

            // 再次检查 bitmap 是否有效
            if (bitmap == null)
                return;

            // 安全地获取图片尺寸
            int bitmapWidth;
            int bitmapHeight;
            try
            {
                bitmapWidth = bitmap.Width;
                bitmapHeight = bitmap.Height;
                
                // 检查尺寸是否有效
                if (bitmapWidth <= 0 || bitmapHeight <= 0)
                    return;
            }
            catch
            {
                // 如果访问属性失败，说明 bitmap 已被释放
                return;
            }

            // 获取画布信息
            var info = e.Info;
            
            // 如果图片尺寸与画布尺寸匹配，直接绘制
            if (info.Width == bitmapWidth && info.Height == bitmapHeight)
            {
                try
                {
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                catch
                {
                    // 绘制失败，忽略
                }
            }
            else
            {
                // 计算缩放比例以适应显示区域（保持宽高比）
                var scale = Math.Min((float)info.Width / bitmapWidth, (float)info.Height / bitmapHeight);
                var scaledWidth = bitmapWidth * scale;
                var scaledHeight = bitmapHeight * scale;
                var x = (info.Width - scaledWidth) / 2;
                var y = (info.Height - scaledHeight) / 2;

                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);
                var sourceRect = new SKRect(0, 0, bitmapWidth, bitmapHeight);
                
                // 使用高质量采样进行缩放
                var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                using var paint = new SKPaint
                {
                    IsAntialias = true
                };
                
                try
                {
                    canvas.DrawBitmap(bitmap, sourceRect, destRect, paint);
                }
                catch
                {
                    // 绘制失败，忽略
                }
            }
        }

        private void OpenOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_outputFolder) || !Directory.Exists(_outputFolder))
            {
                System.Windows.MessageBox.Show("输出目录不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var shell = OperatingSystem.IsWindows() ? "explorer"
                     : OperatingSystem.IsLinux() ? "xdg-open"
                     : OperatingSystem.IsMacOS() ? "open"
                     : null;

            if (shell is not null)
                Process.Start(shell, _outputFolder);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _yolo?.Dispose();
            ClearResultBitmaps();
        }

        private void ClearResultBitmaps()
        {
            // 先清空当前显示的引用（不清除，因为它只是列表中的引用）
            _currentResultBitmap = null;
            
            // 释放列表中的所有 bitmap
            foreach (var bitmap in _resultBitmaps)
            {
                try
                {
                    if (bitmap != null)
                    {
                        bitmap.Dispose();
                    }
                }
                catch
                {
                    // 忽略释放错误（可能已经被释放）
                }
            }
            _resultBitmaps.Clear();
            _currentImageIndex = -1;
        }

        private void ShowImageAtIndex(int index)
        {
            if (index < 0 || index >= _resultBitmaps.Count)
                return;

            // 安全地获取 bitmap
            SKBitmap? bitmap = null;
            try
            {
                bitmap = _resultBitmaps[index];
                if (bitmap == null)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            // 安全地获取图片尺寸
            int width;
            int height;
            try
            {
                width = bitmap.Width;
                height = bitmap.Height;
                
                if (width <= 0 || height <= 0)
                    return;
            }
            catch
            {
                // bitmap 可能已被释放
                return;
            }

            // 更新当前显示的 bitmap（不释放，因为它在列表中）
            _currentResultBitmap = bitmap;
            
            // 设置 SKElement 的尺寸以匹配图片
            ResultImageElement.Width = width;
            ResultImageElement.Height = height;
            
            // 刷新显示
            ResultImageElement.InvalidateVisual();
        }

        private void UpdateImageNavigation()
        {
            if (_resultBitmaps.Count <= 1)
            {
                // 只有一张或没有图片，隐藏控制栏
                if (ImageControlPanel != null)
                    ImageControlPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // 显示控制栏
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Visible;

            // 更新计数器
            if (ImageCounterText != null)
                ImageCounterText.Text = $"{_currentImageIndex + 1} / {_resultBitmaps.Count}";

            // 更新按钮状态
            if (PreviousImageButton != null)
                PreviousImageButton.IsEnabled = _currentImageIndex > 0;

            if (NextImageButton != null)
                NextImageButton.IsEnabled = _currentImageIndex < _resultBitmaps.Count - 1;
        }

        private void PreviousImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex > 0)
            {
                _currentImageIndex--;
                ShowImageAtIndex(_currentImageIndex);
                UpdateImageNavigation();
            }
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex < _resultBitmaps.Count - 1)
            {
                _currentImageIndex++;
                ShowImageAtIndex(_currentImageIndex);
                UpdateImageNavigation();
            }
        }
    }
}

