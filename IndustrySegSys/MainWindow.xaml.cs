// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using Microsoft.Win32;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;
using WinForms = System.Windows.Forms;

namespace IndustrySegSys
{
    /// <summary>
    /// 工業檢測分割檢測系統 - 自動監控模式
    /// 基於 IndustrySegmentationDemo 的功能，添加目錄監控和自動處理功能
    /// </summary>
    public partial class MainWindow : Window
    {
        private Yolo? _yolo;
        private SegmentationDrawingOptions _drawingOptions = default!;
        private SKBitmap? _currentResultBitmap;
        private List<SKBitmap> _resultBitmaps = new List<SKBitmap>(); // 保存所有處理過的圖片
        private int _currentImageIndex = -1; // 當前顯示的圖片索引
        private Dispatcher _dispatcher;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalCount = 0;
        private int _ngCount = 0;
        private int _okCount = 0;
        private string _outputFolder = string.Empty;
        
        // 目錄監控相關
        private FileSystemWatcher? _fileSystemWatcher;
        private Dictionary<string, FileSystemWatcher> _materialWatchers = new Dictionary<string, FileSystemWatcher>(); // 每個料號目錄的監控器
        private HashSet<string> _processedMaterialDirs = new HashSet<string>(); // 已處理的料號目錄
        private object _processingLock = new object(); // 處理鎖，防止重複處理

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
            // 獲取默認路徑
            var defaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Industry_Results");
            string? defaultModelPath = null;
            
            // 嘗試查找默認模型路徑
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectRoot = FindProjectRoot(currentDir);
            if (projectRoot != null)
            {
                var sd900Model = Path.Combine(projectRoot, "test", "assets", "Models", "sd900.onnx");
                if (File.Exists(sd900Model))
                {
                    defaultModelPath = sd900Model;
                }
            }
            
            // 嘗試從 JSON 文件讀取路徑配置
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            var invalidPaths = new List<string>();
            
            if (File.Exists(configPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<PathConfig>(jsonContent);
                    
                    if (config != null)
                    {
                        // 檢查並應用模型路徑
                        if (!string.IsNullOrEmpty(config.ModelPath))
                        {
                            if (File.Exists(config.ModelPath))
                            {
                                ModelPathTextBox.Text = config.ModelPath;
                            }
                            else
                            {
                                invalidPaths.Add($"模型文件路徑無效: {config.ModelPath}");
                                if (defaultModelPath != null)
                                {
                                    ModelPathTextBox.Text = defaultModelPath;
                                }
                            }
                        }
                        else if (defaultModelPath != null)
                        {
                            ModelPathTextBox.Text = defaultModelPath;
                        }
                        
                        // 檢查並應用監控目錄路徑
                        if (!string.IsNullOrEmpty(config.WatchPath))
                        {
                            if (Directory.Exists(config.WatchPath))
                            {
                                WatchPathTextBox.Text = config.WatchPath;
                            }
                            else
                            {
                                invalidPaths.Add($"監控目錄路徑無效: {config.WatchPath}");
                            }
                        }
                        
                        // 檢查並應用輸出目錄路徑
                        if (!string.IsNullOrEmpty(config.OutputPath))
                        {
                            // 輸出目錄如果不存在，嘗試創建
                            try
                            {
                                if (!Directory.Exists(config.OutputPath))
                                {
                                    Directory.CreateDirectory(config.OutputPath);
                                }
                                _outputFolder = config.OutputPath;
                                OutputPathTextBox.Text = config.OutputPath;
                            }
                            catch
                            {
                                invalidPaths.Add($"輸出目錄路徑無效或無法創建: {config.OutputPath}");
                                _outputFolder = defaultOutputPath;
                                OutputPathTextBox.Text = defaultOutputPath;
                            }
                        }
                        else
                        {
                            _outputFolder = defaultOutputPath;
                            OutputPathTextBox.Text = defaultOutputPath;
                        }
                        
                        // 檢查並應用圖片路徑
                        if (!string.IsNullOrEmpty(config.ImagePath))
                        {
                            // 嚴格檢查：文件必須是文件，目錄必須是目錄
                            bool isFile = File.Exists(config.ImagePath) && !Directory.Exists(config.ImagePath);
                            bool isDirectory = Directory.Exists(config.ImagePath) && !File.Exists(config.ImagePath);
                            
                            if (isFile)
                            {
                                ImagePathTextBox.Text = config.ImagePath;
                                SingleFileRadio.IsChecked = true;
                            }
                            else if (isDirectory)
                            {
                                ImagePathTextBox.Text = config.ImagePath;
                                BatchFileRadio.IsChecked = true;
                            }
                            else
                            {
                                invalidPaths.Add($"圖片路徑無效: {config.ImagePath}");
                            }
                        }
                        
                        // 確保按鈕狀態更新（無論是單文件還是批量處理）
                        // 使用 Dispatcher 確保 UI 已經初始化，使用 Normal 優先級確保及時執行
                        _dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateProcessButtonStates();
                        }), DispatcherPriority.Normal);
                        
                        // 如果有無效路徑，顯示提示訊息
                        if (invalidPaths.Count > 0)
                        {
                            var message = "配置文件中的以下路徑無效，已使用預設路徑：\n\n" + string.Join("\n", invalidPaths);
                            System.Windows.MessageBox.Show(message, "路徑驗證警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            AddLog("配置文件中有無效路徑，已使用預設值");
                        }
                        else
                        {
                            AddLog("已從配置文件讀取路徑設置");
                        }
                        
                        // 保存更新後的路徑（如果有無效路徑被修正）
                        if (invalidPaths.Count > 0)
                        {
                            SavePathsToConfig();
                        }
                        
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"讀取配置文件失敗: {ex.Message}");
                    System.Windows.MessageBox.Show($"讀取配置文件失敗: {ex.Message}\n\n將使用預設路徑。", "配置文件錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            
            // 如果沒有配置文件，創建一個默認的配置文件
            try
            {
                var defaultConfig = new PathConfig
                {
                    ModelPath = defaultModelPath ?? string.Empty,
                    WatchPath = string.Empty,
                    OutputPath = defaultOutputPath,
                    ImagePath = string.Empty
                };
                
                var jsonContent = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, jsonContent);
                AddLog("已創建默認配置文件");
            }
            catch (Exception ex)
            {
                AddLog($"創建配置文件失敗: {ex.Message}");
            }
            
            // 使用默認值
            _outputFolder = defaultOutputPath;
            OutputPathTextBox.Text = defaultOutputPath;
            
            if (defaultModelPath != null)
            {
                ModelPathTextBox.Text = defaultModelPath;
            }
            
            // 初始化完成後，更新按鈕狀態（如果已經在手動模式下）
            if (ManualModeRadio != null && ManualModeRadio.IsChecked == true)
            {
                // 使用 Dispatcher 確保 UI 已經初始化
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateProcessButtonStates();
                }), DispatcherPriority.Loaded);
            }
        }
        
        private void SavePathsToConfig()
        {
            try
            {
                var config = new PathConfig
                {
                    ModelPath = ModelPathTextBox.Text,
                    WatchPath = WatchPathTextBox.Text,
                    OutputPath = OutputPathTextBox.Text,
                    ImagePath = ImagePathTextBox.Text
                };
                
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, jsonContent);
            }
            catch (Exception ex)
            {
                AddLog($"保存配置文件失敗: {ex.Message}");
            }
        }
        
        private class PathConfig
        {
            public string? ModelPath { get; set; }
            public string? WatchPath { get; set; }
            public string? OutputPath { get; set; }
            public string? ImagePath { get; set; }
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
                Title = "選擇模型文件"
            };

            if (dialog.ShowDialog() == true)
            {
                ModelPathTextBox.Text = dialog.FileName;
                AddLog($"已選擇模型: {dialog.FileName}");
                SavePathsToConfig();
                UpdateProcessButtonStates();
            }
        }

        private void BrowseWatchPathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "選擇監控目錄"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                WatchPathTextBox.Text = dialog.SelectedPath;
                AddLog($"已選擇監控目錄: {dialog.SelectedPath}");
                SavePathsToConfig();
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (SingleFileRadio.IsChecked == true)
            {
                // 單文件模式
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "圖片文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件 (*.*)|*.*",
                    Title = "選擇圖片文件"
                };

                if (dialog.ShowDialog() == true)
                {
                    ImagePathTextBox.Text = dialog.FileName;
                    AddLog($"已選擇圖片: {dialog.FileName}");
                    SavePathsToConfig();
                    // 確保單文件 radio 被選中
                    SingleFileRadio.IsChecked = true;
                    UpdateProcessButtonStates();
                }
            }
            else
            {
                // 批量處理模式
                var dialog = new WinForms.FolderBrowserDialog
                {
                    Description = "選擇圖片目錄"
                };

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    ImagePathTextBox.Text = dialog.SelectedPath;
                    AddLog($"已選擇圖片目錄: {dialog.SelectedPath}");
                    SavePathsToConfig();
                    // 確保批量處理 radio 被選中
                    BatchFileRadio.IsChecked = true;
                    UpdateProcessButtonStates();
                }
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "選擇輸出目錄"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                OutputPathTextBox.Text = dialog.SelectedPath;
                _outputFolder = dialog.SelectedPath;
                AddLog($"已選擇輸出目錄: {dialog.SelectedPath}");
                SavePathsToConfig();
                UpdateProcessButtonStates();
            }
        }

        private void SingleFileRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 當切換到單文件模式時，立即更新按鈕狀態
            // 如果圖片路徑已經是一個文件，按鈕應該啟用
            UpdateProcessButtonStates();
            
            // 如果圖片路徑是目錄，清空它以便用戶選擇文件
            if (!string.IsNullOrWhiteSpace(ImagePathTextBox.Text) && Directory.Exists(ImagePathTextBox.Text))
            {
                ImagePathTextBox.Text = string.Empty;
                SavePathsToConfig();
            }
        }

        private void BatchFileRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 當切換到批量處理模式時，立即更新按鈕狀態
            // 如果圖片路徑已經是一個目錄，按鈕應該啟用
            UpdateProcessButtonStates();
            
            // 如果圖片路徑是文件，清空它以便用戶選擇目錄
            if (!string.IsNullOrWhiteSpace(ImagePathTextBox.Text) && File.Exists(ImagePathTextBox.Text))
            {
                ImagePathTextBox.Text = string.Empty;
                SavePathsToConfig();
            }
        }

        private void UpdateProcessButtonStates()
        {
            if (ProcessSingleFileButton == null || ProcessBatchButton == null)
                return;

            // 根據 RadioButton 選擇和路徑是否有效來啟用/禁用按鈕
            bool hasImagePath = !string.IsNullOrWhiteSpace(ImagePathTextBox.Text);
            bool hasModelPath = !string.IsNullOrWhiteSpace(ModelPathTextBox.Text) && File.Exists(ModelPathTextBox.Text);
            bool hasOutputPath = !string.IsNullOrWhiteSpace(OutputPathTextBox.Text);

            if (SingleFileRadio.IsChecked == true)
            {
                // 單文件模式：檢查是否選擇了文件（必須是文件，不能是目錄）
                bool isValidFile = hasImagePath && File.Exists(ImagePathTextBox.Text) && !Directory.Exists(ImagePathTextBox.Text);
                ProcessSingleFileButton.IsEnabled = hasModelPath && hasOutputPath && isValidFile;
                ProcessBatchButton.IsEnabled = false;
            }
            else if (BatchFileRadio.IsChecked == true)
            {
                // 批量處理模式：檢查是否選擇了目錄（必須是目錄，不能是文件）
                bool isValidDirectory = hasImagePath && Directory.Exists(ImagePathTextBox.Text) && !File.Exists(ImagePathTextBox.Text);
                ProcessSingleFileButton.IsEnabled = false;
                ProcessBatchButton.IsEnabled = hasModelPath && hasOutputPath && isValidDirectory;
            }
            else
            {
                // 沒有選擇模式，禁用所有按鈕
                ProcessSingleFileButton.IsEnabled = false;
                ProcessBatchButton.IsEnabled = false;
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

        private void MonitorModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 切換到自動監控模式
            // 檢查控件是否已初始化，避免在構造函數階段訪問未初始化的控件
            if (ManualImagePanel == null || StartMonitorButton == null || 
                StopMonitorButton == null || StartButton == null || StopButton == null ||
                ProcessSingleFileButton == null || ProcessBatchButton == null)
            {
                return;
            }

            ManualImagePanel.Visibility = Visibility.Collapsed;
            StartMonitorButton.Visibility = Visibility.Visible;
            StopMonitorButton.Visibility = Visibility.Visible;
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Collapsed;
            ProcessSingleFileButton.Visibility = Visibility.Collapsed;
            ProcessBatchButton.Visibility = Visibility.Collapsed;
            
            // 隱藏處理進度（自動監控模式下不顯示）
            if (ProgressGroupBox != null)
            {
                ProgressGroupBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ManualModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 切換到手動處理模式
            // 檢查控件是否已初始化，避免在構造函數階段訪問未初始化的控件
            if (ManualImagePanel == null || StartMonitorButton == null || 
                StopMonitorButton == null || StartButton == null || StopButton == null ||
                ProcessSingleFileButton == null || ProcessBatchButton == null)
            {
                return;
            }

            ManualImagePanel.Visibility = Visibility.Visible;
            StartMonitorButton.Visibility = Visibility.Collapsed;
            StopMonitorButton.Visibility = Visibility.Collapsed;
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            ProcessSingleFileButton.Visibility = Visibility.Visible;
            ProcessBatchButton.Visibility = Visibility.Visible;
            
            // 顯示處理進度（只在手動處理模式下顯示）
            if (ProgressGroupBox != null)
            {
                ProgressGroupBox.Visibility = Visibility.Visible;
            }
            
            // 初始化按鈕狀態
            StopButton.IsEnabled = false;
            UpdateProcessButtonStates();
        }

        private async void StartMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(ModelPathTextBox.Text) || !File.Exists(ModelPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(WatchPathTextBox.Text) || !Directory.Exists(WatchPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的監控目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 創建輸出目錄
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

                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                StatusText.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"模型初始化失敗: {ex.Message}");
                return;
            }

            // 重置統計信息
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _processedMaterialDirs.Clear();
            UpdateStatistics();

            // 啟動目錄監控
            try
            {
                _fileSystemWatcher = new FileSystemWatcher(WatchPathTextBox.Text)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _fileSystemWatcher.Created += FileSystemWatcher_Created;
                _fileSystemWatcher.Error += FileSystemWatcher_Error;

                AddLog($"開始監控目錄: {WatchPathTextBox.Text}");
                StatusText.Text = "監控中...";
                MonitorStatusText.Text = "監控狀態: 運行中";
                MonitorStatusText.Foreground = System.Windows.Media.Brushes.Green;

                // 處理已存在的目錄
                await ProcessExistingDirectories(WatchPathTextBox.Text);

                StartMonitorButton.IsEnabled = false;
                StopMonitorButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"啟動監控失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"啟動監控失敗: {ex.Message}");
            }
        }

        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Created -= FileSystemWatcher_Created;
                _fileSystemWatcher.Error -= FileSystemWatcher_Error;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }

            // 停止所有料號目錄的監控器
            lock (_processingLock)
            {
                foreach (var watcher in _materialWatchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Created -= MaterialWatcher_StationCreated;
                        watcher.Error -= FileSystemWatcher_Error;
                        watcher.Dispose();
                    }
                    catch { }
                }
                _materialWatchers.Clear();
            }

            AddLog("停止監控");
            StatusText.Text = "監控已停止";
            MonitorStatusText.Text = "監控狀態: 未啟動";
            MonitorStatusText.Foreground = System.Windows.Media.Brushes.Gray;

            StartMonitorButton.IsEnabled = true;
            StopMonitorButton.IsEnabled = false;
        }

        private async void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            // 延遲處理，確保目錄完全創建
            await Task.Delay(1000);

            if (Directory.Exists(e.FullPath))
            {
                // 檢查是否是料號目錄（在監控根目錄下直接創建的目錄）
                // 使用 Dispatcher 安全地訪問 UI 控件
                string watchPath = string.Empty;
                _dispatcher.Invoke(() =>
                {
                    watchPath = WatchPathTextBox.Text;
                });
                
                if (string.IsNullOrEmpty(watchPath))
                    return;
                
                var parentPath = Path.GetDirectoryName(e.FullPath);
                
                if (string.Equals(parentPath, watchPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 這是料號目錄，處理它並為它創建監控器
                    await ProcessMaterialDirectory(e.FullPath);
                    
                    // 為料號目錄創建監控器，監控工站目錄的創建
                    CreateMaterialWatcher(e.FullPath);
                }
            }
        }

        private void CreateMaterialWatcher(string materialDirPath)
        {
            lock (_processingLock)
            {
                // 如果已經有監控器，先移除
                if (_materialWatchers.ContainsKey(materialDirPath))
                {
                    var oldWatcher = _materialWatchers[materialDirPath];
                    oldWatcher.EnableRaisingEvents = false;
                    oldWatcher.Created -= MaterialWatcher_StationCreated;
                    oldWatcher.Error -= FileSystemWatcher_Error;
                    oldWatcher.Dispose();
                    _materialWatchers.Remove(materialDirPath);
                }

                // 創建新的監控器
                try
                {
                    var watcher = new FileSystemWatcher(materialDirPath)
                    {
                        NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = true
                    };

                    watcher.Created += MaterialWatcher_StationCreated;
                    watcher.Error += FileSystemWatcher_Error;

                    _materialWatchers[materialDirPath] = watcher;
                    
                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"  已為料號目錄創建工站監控器: {Path.GetFileName(materialDirPath)}");
                    });
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"  創建工站監控器失敗: {ex.Message}");
                    });
                }
            }
        }

        private async void MaterialWatcher_StationCreated(object sender, FileSystemEventArgs e)
        {
            // 延遲處理，確保目錄完全創建
            await Task.Delay(1000);

            if (Directory.Exists(e.FullPath))
            {
                // 檢查是否是工站目錄（以 S 開頭）
                var stationName = Path.GetFileName(e.FullPath);
                if (stationName.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    // 找到對應的料號目錄
                    var materialDirPath = Path.GetDirectoryName(e.FullPath);
                    if (materialDirPath != null && Directory.Exists(materialDirPath))
                    {
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"檢測到新工站目錄: {Path.GetFileName(materialDirPath)}/{stationName}");
                        });
                        
                        // 重新處理料號目錄（會包含新創建的工站）
                        // 先從已處理列表中移除，以便重新處理
                        lock (_processingLock)
                        {
                            _processedMaterialDirs.Remove(materialDirPath);
                        }
                        
                        await ProcessMaterialDirectory(materialDirPath);
                    }
                }
            }
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            _dispatcher.Invoke(() =>
            {
                AddLog($"監控錯誤: {e.GetException().Message}");
            });
        }

        private async Task ProcessExistingDirectories(string watchPath)
        {
            try
            {
                var directories = Directory.GetDirectories(watchPath);
                AddLog($"發現 {directories.Length} 個現有目錄，開始處理...");

                foreach (var dir in directories)
                {
                    await ProcessMaterialDirectory(dir);
                    // 為每個料號目錄創建監控器
                    CreateMaterialWatcher(dir);
                }
            }
            catch (Exception ex)
            {
                AddLog($"處理現有目錄時發生錯誤: {ex.Message}");
            }
        }

        private async Task ProcessMaterialDirectory(string materialDirPath)
        {
            // 使用鎖防止重複處理
            lock (_processingLock)
            {
                if (_processedMaterialDirs.Contains(materialDirPath))
                {
                    return;
                }
                _processedMaterialDirs.Add(materialDirPath);
            }

            await Task.Run(async () =>
            {
                try
                {
                    var materialDirName = Path.GetFileName(materialDirPath);
                    _dispatcher.Invoke(() =>
                    {
                        CurrentMaterialText.Text = materialDirName;
                        AddLog($"檢測到新料號目錄: {materialDirName}");
                    });

                    // 獲取所有工站目錄 (S1, S2, S3, ...)
                    var stationDirs = Directory.GetDirectories(materialDirPath)
                        .Where(d => Path.GetFileName(d).StartsWith("S", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(d => d)
                        .ToList();

                    if (stationDirs.Count == 0)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  警告: 料號目錄 {materialDirName} 中沒有找到工站目錄");
                        });
                        return;
                    }

                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"  找到 {stationDirs.Count} 個工站目錄");
                    });

                    // 處理每個工站的圖片
                    var allImageFiles = new List<string>();
                    foreach (var stationDir in stationDirs)
                    {
                        var stationName = Path.GetFileName(stationDir);
                        var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };
                        var stationImages = new List<string>();
                        
                        foreach (var extension in imageExtensions)
                        {
                            stationImages.AddRange(Directory.GetFiles(stationDir, extension, SearchOption.TopDirectoryOnly));
                        }

                        stationImages = stationImages.OrderBy(f => f).ToList();
                        allImageFiles.AddRange(stationImages);

                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  工站 {stationName}: {stationImages.Count} 張圖片");
                        });
                    }

                    if (allImageFiles.Count == 0)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  警告: 料號目錄 {materialDirName} 中沒有找到圖片文件");
                        });
                        return;
                    }

                    // 處理所有圖片
                    var confidence = _dispatcher.Invoke(() => ConfidenceSlider.Value);
                    var pixelConfidence = _dispatcher.Invoke(() => PixelConfidenceSlider.Value);
                    var iou = _dispatcher.Invoke(() => IouSlider.Value);

                    int processedCount = 0;
                    bool materialHasNg = false;

                    foreach (var imagePath in allImageFiles)
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            var fileName = Path.GetFileName(imagePath);
                            var relativePath = Path.GetRelativePath(materialDirPath, imagePath);
                            
                            _dispatcher.Invoke(() =>
                            {
                                CurrentFileText.Text = $"{materialDirName}/{relativePath}";
                                AddLog($"  處理: {relativePath}");
                            });

                            // 加載圖片
                            using var image = SKBitmap.Decode(imagePath);
                            if (image == null)
                            {
                                _dispatcher.Invoke(() =>
                                {
                                    AddLog($"    -> 錯誤: 無法加載圖片");
                                });
                                continue;
                            }

                            // 運行檢測
                            var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                            
                            // 記錄處理時間
                            stopwatch.Stop();
                            var processingTime = stopwatch.ElapsedMilliseconds;

                            // 確定結果
                            string suffix;
                            bool isNg = results.Count > 0;
                            if (isNg)
                            {
                                materialHasNg = true;
                                Interlocked.Increment(ref _ngCount);
                                suffix = "NG";
                                _dispatcher.Invoke(() =>
                                {
                                    AddLog($"    -> 檢測到 {results.Count} 個目標，標記為 NG");
                                });
                            }
                            else
                            {
                                Interlocked.Increment(ref _okCount);
                                suffix = "OK";
                                _dispatcher.Invoke(() =>
                                {
                                    AddLog($"    -> 未檢測到目標，標記為 OK");
                                });
                            }

                            // 繪製結果
                            image.Draw(results, _drawingOptions);

                            // 保存結果 - 保持目錄結構
                            var fileExtension = Path.GetExtension(imagePath);
                            var outputMaterialDir = Path.Combine(_outputFolder, materialDirName);
                            var outputStationDir = Path.Combine(outputMaterialDir, Path.GetFileName(Path.GetDirectoryName(imagePath)!));
                            Directory.CreateDirectory(outputStationDir);

                            var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                            var outputPath = Path.Combine(outputStationDir, newFileName);

                            var encodedFormat = GetEncodedFormat(fileExtension);
                            image.Save(outputPath, encodedFormat, 80);

                            processedCount++;
                            Interlocked.Increment(ref _totalCount);

                            // 更新顯示
                            var copiedBitmap = image.Copy();
                            _dispatcher.Invoke(() =>
                            {
                                _resultBitmaps.Add(copiedBitmap);
                                _currentImageIndex = _resultBitmaps.Count - 1;
                                ShowImageAtIndex(_currentImageIndex);
                                NoImageText.Visibility = Visibility.Collapsed;
                                UpdateImageNavigation();
                                AddLog($"    -> 已保存到: {outputPath}");
                                
                                // 更新處理速度顯示（單張圖的辨識時間）
                                ProcessingSpeedText.Text = $"{processingTime} ms";
                                
                                UpdateStatistics();
                            });
                        }
                        catch (Exception ex)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"    -> 錯誤: 處理 {Path.GetFileName(imagePath)} 時發生異常: {ex.Message}");
                            });
                        }
                    }

                    // 料號整體判斷
                    var materialResult = materialHasNg ? "NG" : "OK";
                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"料號 {materialDirName} 處理完成: {processedCount} 張圖片，整體結果: {materialResult}");
                    });
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        AddLog($"處理料號目錄時發生錯誤: {ex.Message}");
                    });
                }
            });
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(ModelPathTextBox.Text) || !File.Exists(ModelPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇圖片文件或目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (SingleFileRadio.IsChecked == true && !File.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的圖片文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (BatchFileRadio.IsChecked == true && !Directory.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的圖片目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 創建輸出目錄
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

                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                StatusText.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"模型初始化失敗: {ex.Message}");
                return;
            }

            // 重置統計信息和圖片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/啟用按鈕
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;
            
            // 隱藏切換控制欄
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Collapsed;

            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 在進入後台線程之前獲取參數值（避免線程訪問錯誤）
            var confidence = ConfidenceSlider.Value;
            var pixelConfidence = PixelConfidenceSlider.Value;
            var iou = IouSlider.Value;

            // 開始處理
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
                AddLog("處理已取消");
                StatusText.Text = "處理已取消";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"處理過程中發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"錯誤: {ex.Message}");
            }
            finally
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "就緒";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在停止處理...");
            StatusText.Text = "正在停止...";
            
            // 重新啟用處理按鈕
            if (ProcessSingleFileButton != null)
                ProcessSingleFileButton.IsEnabled = true;
            if (ProcessBatchButton != null)
                ProcessBatchButton.IsEnabled = true;
        }

        private async void ProcessSingleFileButton_Click(object sender, RoutedEventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(ModelPathTextBox.Text) || !File.Exists(ModelPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ImagePathTextBox.Text) || !File.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的圖片文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 創建輸出目錄
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

                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                StatusText.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"模型初始化失敗: {ex.Message}");
                return;
            }

            // 重置統計信息和圖片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/啟用按鈕
            ProcessSingleFileButton.IsEnabled = false;
            ProcessBatchButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;
            
            // 隱藏切換控制欄
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Collapsed;

            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 在進入後台線程之前獲取參數值（避免線程訪問錯誤）
            var confidence = ConfidenceSlider.Value;
            var pixelConfidence = PixelConfidenceSlider.Value;
            var iou = IouSlider.Value;

            // 開始處理單文件
            try
            {
                await ProcessSingleFile(ImagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AddLog("處理已取消");
                StatusText.Text = "處理已取消";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"處理過程中發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"錯誤: {ex.Message}");
            }
            finally
            {
                ProcessSingleFileButton.IsEnabled = true;
                ProcessBatchButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "就緒";
            }
        }

        private async void ProcessBatchButton_Click(object sender, RoutedEventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(ModelPathTextBox.Text) || !File.Exists(ModelPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ImagePathTextBox.Text) || !Directory.Exists(ImagePathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇有效的圖片目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 創建輸出目錄
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

                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                StatusText.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"模型初始化失敗: {ex.Message}");
                return;
            }

            // 重置統計信息和圖片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/啟用按鈕
            ProcessSingleFileButton.IsEnabled = false;
            ProcessBatchButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;
            
            // 隱藏切換控制欄
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Collapsed;

            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 在進入後台線程之前獲取參數值（避免線程訪問錯誤）
            var confidence = ConfidenceSlider.Value;
            var pixelConfidence = PixelConfidenceSlider.Value;
            var iou = IouSlider.Value;

            // 開始批量處理
            try
            {
                await ProcessBatchFiles(ImagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AddLog("處理已取消");
                StatusText.Text = "處理已取消";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"處理過程中發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"錯誤: {ex.Message}");
            }
            finally
            {
                ProcessSingleFileButton.IsEnabled = true;
                ProcessBatchButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "就緒";
            }
        }

        private async Task ProcessSingleFile(string imagePath, double confidence, double pixelConfidence, double iou, CancellationToken cancellationToken)
        {
            // 重置計數器
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _dispatcher.Invoke(() =>
            {
                UpdateStatistics();
                ProgressBar.Maximum = 1;
                ProgressBar.Value = 0;
            });

            await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(imagePath);
                    _dispatcher.Invoke(() =>
                    {
                        CurrentFileText.Text = fileName;
                        AddLog($"處理: {fileName}");
                        StatusText.Text = $"正在處理: {fileName}";
                    });

                    // 加載圖片
                    using var image = SKBitmap.Decode(imagePath);
                    if (image == null)
                    {
                        throw new Exception($"無法加載圖片: {imagePath}");
                    }

                    // 運行檢測（使用傳入的參數，而不是從 UI 控件獲取）
                    var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                    
                    // 記錄處理時間
                    stopwatch.Stop();
                    var processingTime = stopwatch.ElapsedMilliseconds;

                    // 確定結果
                    string suffix;
                    _totalCount++; // 增加總數
                    if (results.Count > 0)
                    {
                        _ngCount++;
                        suffix = "NG";
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  -> 檢測到 {results.Count} 個目標，標記為 NG");
                        });
                    }
                    else
                    {
                        _okCount++;
                        suffix = "OK";
                        _dispatcher.Invoke(() =>
                        {
                            AddLog($"  -> 未檢測到目標，標記為 OK");
                        });
                    }

                    // 繪製結果
                    image.Draw(results, _drawingOptions);

                    // 保存結果
                    var fileExtension = Path.GetExtension(imagePath);
                    var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                    var outputPath = Path.Combine(_outputFolder, newFileName);

                    var encodedFormat = GetEncodedFormat(fileExtension);
                    image.Save(outputPath, encodedFormat, 80);

                    // 更新顯示
                    var copiedBitmap = image.Copy();
                    _dispatcher.Invoke(() =>
                    {
                        // 添加到圖片列表
                        _resultBitmaps.Add(copiedBitmap);
                        _currentImageIndex = _resultBitmaps.Count - 1;
                        
                        // 顯示當前圖片
                        ShowImageAtIndex(_currentImageIndex);
                        
                        // 隱藏"暫無圖片"提示
                        NoImageText.Visibility = Visibility.Collapsed;
                        
                        // 更新切換控制欄
                        UpdateImageNavigation();
                        
                        // 更新處理速度顯示（單張圖的辨識時間）
                        ProcessingSpeedText.Text = $"{processingTime} ms";
                        
                        AddLog($"  -> 已保存到: {outputPath}");
                        UpdateStatistics();
                        ProgressBar.Value = 1;
                        
                        // 單文件處理完成後，顯示切換控制欄（雖然只有一張）
                        if (_resultBitmaps.Count > 0)
                        {
                            UpdateImageNavigation();
                        }
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
                        AddLog($"  -> 錯誤: {ex.Message}");
                    });
                }
            }, cancellationToken);
        }

        private async Task ProcessBatchFiles(string imageDirectory, double confidence, double pixelConfidence, double iou, CancellationToken cancellationToken)
        {
            // 獲取所有圖片文件
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
                    System.Windows.MessageBox.Show($"在目錄 {imageDirectory} 中找不到圖片文件！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return;
            }

            // 重置計數器
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
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

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var fileName = Path.GetFileName(imagePath);
                        _dispatcher.Invoke(() =>
                        {
                            CurrentFileText.Text = fileName;
                            AddLog($"處理: {fileName}");
                            StatusText.Text = $"正在處理: {fileName} ({processedCount + 1}/{imageFiles.Count})";
                        });

                        // 加載圖片
                        using var image = SKBitmap.Decode(imagePath);
                        if (image == null)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 錯誤: 無法加載圖片");
                            });
                            continue;
                        }

                        // 運行檢測（使用傳入的參數，而不是從 UI 控件獲取）
                        var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                        
                        // 記錄處理時間
                        stopwatch.Stop();
                        var processingTime = stopwatch.ElapsedMilliseconds;

                        // 確定結果
                        string suffix;
                        _totalCount++; // 增加總數（只計算成功處理的圖片）
                        if (results.Count > 0)
                        {
                            _ngCount++;
                            suffix = "NG";
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 檢測到 {results.Count} 個目標，標記為 NG");
                            });
                        }
                        else
                        {
                            _okCount++;
                            suffix = "OK";
                            _dispatcher.Invoke(() =>
                            {
                                AddLog($"  -> 未檢測到目標，標記為 OK");
                            });
                        }

                        // 繪製結果
                        image.Draw(results, _drawingOptions);

                        // 保存結果
                        var fileExtension = Path.GetExtension(imagePath);
                        var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                        var outputPath = Path.Combine(_outputFolder, newFileName);

                        var encodedFormat = GetEncodedFormat(fileExtension);
                        image.Save(outputPath, encodedFormat, 80);

                        processedCount++;

                        // 更新顯示
                        var copiedBitmap = image.Copy();
                        _dispatcher.Invoke(() =>
                        {
                            // 添加到圖片列表
                            _resultBitmaps.Add(copiedBitmap);
                            _currentImageIndex = _resultBitmaps.Count - 1;
                            
                            // 顯示當前圖片（顯示最後一張）
                            ShowImageAtIndex(_currentImageIndex);
                            
                            // 隱藏"暫無圖片"提示
                            NoImageText.Visibility = Visibility.Collapsed;
                            
                            // 更新切換控制欄
                            UpdateImageNavigation();
                            
                            // 更新處理速度顯示（單張圖的辨識時間）
                            ProcessingSpeedText.Text = $"{processingTime} ms";
                            
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
                            AddLog($"  -> 錯誤: 處理 {Path.GetFileName(imagePath)} 時發生異常: {ex.Message}");
                        });
                    }
                }
            }, cancellationToken);

            _dispatcher.Invoke(() =>
            {
                AddLog($"處理完成！總共處理: {processedCount} 個文件");
                StatusText.Text = "處理完成";
                
                // 如果有處理的圖片，顯示切換控制欄
                if (_resultBitmaps.Count > 0)
                {
                    UpdateImageNavigation();
                }
            });
        }

        private void UpdateStatistics()
        {
            TotalCountText.Text = _totalCount.ToString();
            NgCountText.Text = _ngCount.ToString();
            OkCountText.Text = _okCount.ToString();
            
            // 計算良率（OK數/總數 * 100%）
            if (_totalCount > 0)
            {
                var yieldRate = (double)_okCount / _totalCount * 100.0;
                YieldRateText.Text = $"{yieldRate:F2}%";
            }
            else
            {
                YieldRateText.Text = "0.00%";
            }
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

            // 安全地獲取當前圖片的引用
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
                // 如果訪問失敗，直接返回
                return;
            }

            // 再次檢查 bitmap 是否有效
            if (bitmap == null)
                return;

            // 安全地獲取圖片尺寸
            int bitmapWidth;
            int bitmapHeight;
            try
            {
                bitmapWidth = bitmap.Width;
                bitmapHeight = bitmap.Height;
                
                // 檢查尺寸是否有效
                if (bitmapWidth <= 0 || bitmapHeight <= 0)
                    return;
            }
            catch
            {
                // 如果訪問屬性失敗，說明 bitmap 已被釋放
                return;
            }

            // 獲取畫布信息
            var info = e.Info;
            
            // 如果圖片尺寸與畫布尺寸匹配，直接繪製
            if (info.Width == bitmapWidth && info.Height == bitmapHeight)
            {
                try
                {
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                catch
                {
                    // 繪製失敗，忽略
                }
            }
            else
            {
                // 計算縮放比例以適應顯示區域（保持寬高比）
                var scale = Math.Min((float)info.Width / bitmapWidth, (float)info.Height / bitmapHeight);
                var scaledWidth = bitmapWidth * scale;
                var scaledHeight = bitmapHeight * scale;
                var x = (info.Width - scaledWidth) / 2;
                var y = (info.Height - scaledHeight) / 2;

                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);
                var sourceRect = new SKRect(0, 0, bitmapWidth, bitmapHeight);
                
                // 使用高質量採樣進行縮放
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
                    // 繪製失敗，忽略
                }
            }
        }

        private void OpenOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_outputFolder) || !Directory.Exists(_outputFolder))
            {
                System.Windows.MessageBox.Show("輸出目錄不存在！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // 先清空當前顯示的引用（不清除，因為它只是列表中的引用）
            _currentResultBitmap = null;
            
            // 釋放列表中的所有 bitmap
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
                    // 忽略釋放錯誤（可能已經被釋放）
                }
            }
            _resultBitmaps.Clear();
            _currentImageIndex = -1;
        }

        private void ShowImageAtIndex(int index)
        {
            if (index < 0 || index >= _resultBitmaps.Count)
                return;

            // 安全地獲取 bitmap
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

            // 安全地獲取圖片尺寸
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
                // bitmap 可能已被釋放
                return;
            }

            // 更新當前顯示的 bitmap（不釋放，因為它在列表中）
            _currentResultBitmap = bitmap;
            
            // 設置 SKElement 的尺寸以匹配圖片（Viewbox 會自動處理縮放和置中）
            ResultImageElement.Width = width;
            ResultImageElement.Height = height;
            
            // 觸發重繪
            ResultImageElement.InvalidateVisual();
            
            // 刷新顯示
            ResultImageElement.InvalidateVisual();
        }

        private void UpdateImageNavigation()
        {
            if (_resultBitmaps.Count <= 1)
            {
                // 只有一張或沒有圖片，隱藏控制欄
                if (ImageControlPanel != null)
                    ImageControlPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // 顯示控制欄
            if (ImageControlPanel != null)
                ImageControlPanel.Visibility = Visibility.Visible;

            // 更新計數器
            if (ImageCounterText != null)
                ImageCounterText.Text = $"{_currentImageIndex + 1} / {_resultBitmaps.Count}";

            // 更新按鈕狀態
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