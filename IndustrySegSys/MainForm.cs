using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace IndustrySegSys
{
    public partial class MainForm : Form
    {
        // --- layout guards ---
        private bool _splittersInitialized = false;
        private bool _splitterInitInProgress = false;

        private Yolo? _yolo;
        private string? _currentModelPath; // 追蹤當前加載的模型路徑
        private SegmentationDrawingOptions _drawingOptions = default!;
        private Bitmap? _currentResultBitmap;
        private List<Bitmap> _resultBitmaps = new List<Bitmap>();
        private List<string> _resultImagePaths = new List<string>(); // 追蹤每張圖片的輸出路徑
        private int _currentImageIndex = -1;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalCount = 0;
        private int _ngCount = 0;
        private int _okCount = 0;
        private string _outputFolder = string.Empty;

        // 目錄監控相關
        private FileSystemWatcher? _fileSystemWatcher;
        private Dictionary<string, FileSystemWatcher> _materialWatchers = new Dictionary<string, FileSystemWatcher>();
        private HashSet<string> _processedMaterialDirs = new HashSet<string>();
        private object _processingLock = new object();

        // 監控佇列與背景工作 (Producer/Consumer)
        private readonly Channel<MonitorWorkItem> _monitorQueue = Channel.CreateUnbounded<MonitorWorkItem>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private Task? _monitorWorkerTask;
        private CancellationTokenSource? _monitorCts;
        private readonly ConcurrentDictionary<string, byte> _inFlightMaterials = new(StringComparer.OrdinalIgnoreCase);

        // 推論門閥：避免 _yolo 在多執行緒同時推論 (thread-safety)
        private readonly SemaphoreSlim _inferenceGate = new(1, 1);

        // 相機相關變數
        private VideoCaptureDevice? _videoSource;
        private FilterInfoCollection? _videoDevices;
        private Bitmap? _currentCameraFrame;
        private readonly object _cameraFrameLock = new object();
        private bool _isCapturing = false;
        private readonly SemaphoreSlim _cameraSaveSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        private record MonitorWorkItem(string MaterialDirPath, string Reason);

public MainForm()
        {
            InitializeComponent();
            // Ensure the window is large enough so SplitContainer min sizes will never break layout.
            this.MinimumSize = new Size(900, 650);
            if (this.Size.Width < 900 || this.Size.Height < 650)
            {
                this.Size = new Size(1200, 800);
            }

            // Delay splitter min sizes + distances until the form is actually shown (Handle created + real size).
            this.Shown += (s, e) => InitSplitContainersSafe();
            this.Resize += (s, e) => InitSplitContainersSafe();

            InitializeDrawingOptions();
            InitializeDefaultPaths();
            SetupEventHandlers();

            // 在窗口載入完成後應用布局比例
            this.Load += MainForm_Load;

            // 初始化時設置控件可見性（根據默認選中的監控模式）
            // 手動觸發一次事件以確保控件狀態正確
            AddLog("初始化完成，當前模式: " + (monitorModeRadio.Checked ? "監控模式" : "手動模式"));

            // 測試：直接設置手動模式控件可見性
            AddLog($"測試 - manualImagePanel 初始狀態: Visible={manualImagePanel.Visible}, Parent={manualImagePanel.Parent?.GetType().Name ?? "null"}");
            AddLog($"測試 - processSingleFileButton 初始狀態: Visible={processSingleFileButton.Visible}, Parent={processSingleFileButton.Parent?.GetType().Name ?? "null"}");
            AddLog($"測試 - progressGroupBox 初始狀態: Visible={progressGroupBox.Visible}, Parent={progressGroupBox.Parent?.GetType().Name ?? "null"}");

            if (monitorModeRadio.Checked)
            {
                // 觸發監控模式的事件處理
                monitorModeRadio.Checked = false;
                monitorModeRadio.Checked = true;
            }
            else if (manualModeRadio.Checked)
            {
                // 觸發手動模式的事件處理
                manualModeRadio.Checked = false;
                manualModeRadio.Checked = true;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 延遲設置布局比例，確保控件已經有實際尺寸
            this.BeginInvoke(new Action(() =>
            {
                // Ensure SplitContainers are initialized with safe MinSize and SplitterDistance after layout
                EnsureSplitContainersInitialized();
                // 窗口載入完成後，從配置文件讀取並應用布局比例
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(configPath);
                        var config = JsonSerializer.Deserialize<PathConfig>(jsonContent);
                        if (config != null)
                        {
                            ApplyLayoutRatios(config.MainSplitterRatio, config.RightSplitterRatio);
                        }
                    }
                    catch
                    {
                        // 如果讀取失敗，使用預設比例
                        ApplyLayoutRatios(0.3333, 0.5);
                    }
                }
                else
                {
                    // 如果沒有配置文件，使用預設比例
                    ApplyLayoutRatios(0.3333, 0.5);
                }
            }));
        }

        private void ApplyLayoutRatios(double? mainRatio, double? rightRatio)
        {
            if (mainSplitContainer == null || rightSplitContainer == null)
                return;

            try
            {
                // 確保控件已經有實際尺寸（依 Orientation 決定以 Height 或 Width 作為驗證基準）
                if (GetSplitTotalLength(mainSplitContainer) <= 0 || GetSplitTotalLength(rightSplitContainer) <= 0)
                {
                    // 如果尺寸還未確定，延遲執行
                    this.BeginInvoke(new Action(() => ApplyLayoutRatios(mainRatio, rightRatio)));
                    return;
                }

                // 應用主分隔線比例（圖片區域佔比）
                var mainTotal = GetSplitTotalLength(mainSplitContainer);
                var mainMinDistance = mainSplitContainer.Panel1MinSize;
                var mainMaxDistance = mainTotal - mainSplitContainer.Panel2MinSize;

                // 確保有效範圍存在
                if (mainMinDistance >= mainMaxDistance)
                {
                    AddLog($"主分隔線有效範圍無效: Min={mainMinDistance}, Max={mainMaxDistance}, Total={mainTotal}, Orientation={mainSplitContainer.Orientation}");
                    return;
                }

                if (mainRatio.HasValue && mainRatio.Value > 0 && mainRatio.Value < 1)
                {
                    var distance = Clamp((int)(mainTotal * mainRatio.Value), mainMinDistance, mainMaxDistance);
                    TrySetSplitterDistance(mainSplitContainer, distance);
                }
                else
                {
                    // 預設平均分布：33.33%，但確保在有效範圍內
                    var distance = Clamp(mainTotal / 3, mainMinDistance, mainMaxDistance);
                    TrySetSplitterDistance(mainSplitContainer, distance);
                }

                // 應用右側分隔線比例（終端區域佔比）
                var rightTotal = GetSplitTotalLength(rightSplitContainer);
                var rightMinDistance = rightSplitContainer.Panel1MinSize;
                var rightMaxDistance = rightTotal - rightSplitContainer.Panel2MinSize;

                // 確保有效範圍存在
                if (rightMinDistance >= rightMaxDistance)
                {
                    AddLog($"右側分隔線有效範圍無效: Min={rightMinDistance}, Max={rightMaxDistance}, Total={rightTotal}, Orientation={rightSplitContainer.Orientation}");
                    return;
                }

                if (rightRatio.HasValue && rightRatio.Value > 0 && rightRatio.Value < 1)
                {
                    var distance = Clamp((int)(rightTotal * rightRatio.Value), rightMinDistance, rightMaxDistance);
                    TrySetSplitterDistance(rightSplitContainer, distance);
                }
                else
                {
                    // 預設平均分布：50%，但確保在有效範圍內
                    var distance = Clamp(rightTotal / 2, rightMinDistance, rightMaxDistance);
                    TrySetSplitterDistance(rightSplitContainer, distance);
                }
            }
            catch (Exception ex)
            {
                AddLog($"應用布局比例失敗: {ex.Message}");
            }
        }

        private static int GetSplitTotalLength(SplitContainer sc)
            => sc.Orientation == Orientation.Horizontal ? sc.Height : sc.Width;

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);


        private void EnsureSplitContainersInitialized()
        {
            if (mainSplitContainer == null || rightSplitContainer == null)
                return;

            InitializeSplitContainer(mainSplitContainer, desiredPanel1Min: 100, desiredPanel2Min: 200, defaultRatio: 0.3333);
            InitializeSplitContainer(rightSplitContainer, desiredPanel1Min: 200, desiredPanel2Min: 200, defaultRatio: 0.5);
        }

        private void InitializeSplitContainer(SplitContainer sc, int desiredPanel1Min, int desiredPanel2Min, double defaultRatio)
        {
            // Total length depends on orientation. During early layout it can be 0.
            var total = GetSplitTotalLength(sc);
            if (total <= 0)
                return;

            // If the container is too small for desired mins, shrink mins dynamically to avoid SplitterDistance exceptions.
            var splitter = sc.SplitterWidth <= 0 ? 4 : sc.SplitterWidth;
            var min1 = Math.Max(0, desiredPanel1Min);
            var min2 = Math.Max(0, desiredPanel2Min);

            // Leave a small buffer to avoid edge cases during layout.
            const int buffer = 8;

            if (total < (min1 + min2 + splitter + buffer))
            {
                // Prefer keeping Panel1 min, shrink Panel2 first.
                var remaining = total - min1 - splitter - buffer;
                if (remaining < 0) remaining = 0;
                min2 = Math.Min(min2, remaining);

                // If still impossible, relax Panel1 too.
                if (total < (min1 + min2 + splitter + buffer))
                {
                    var remaining2 = total - splitter - buffer;
                    if (remaining2 < 0) remaining2 = 0;

                    // Split remaining between panels
                    min1 = Math.Min(min1, remaining2 / 2);
                    min2 = Math.Min(min2, remaining2 - min1);
                }
            }

            // Apply mins (these assignments can indirectly force SplitterDistance validation inside WinForms)
            try
            {
                sc.Panel1MinSize = min1;
                sc.Panel2MinSize = min2;
            }
            catch
            {
                // If WinForms still rejects during transient layout, just skip now and retry later.
                if (this.IsHandleCreated)
                    this.BeginInvoke(new Action(() => InitializeSplitContainer(sc, desiredPanel1Min, desiredPanel2Min, defaultRatio)));
                return;
            }

            // Now apply a safe SplitterDistance
            total = GetSplitTotalLength(sc);
            var minDistance = sc.Panel1MinSize;
            var maxDistance = total - sc.Panel2MinSize;

            if (total <= 0 || maxDistance <= minDistance)
                return;

            var distance = Clamp((int)(total * defaultRatio), minDistance, maxDistance);
            TrySetSplitterDistance(sc, distance);
        }

        private void TrySetSplitterDistance(SplitContainer sc, int distance)
        {
            try
            {
                sc.SplitterDistance = distance;
            }
            catch (InvalidOperationException)
            {
                // 某些時刻（例如 Resize 過程中）WinForms 仍可能暫時認為尺寸未就緒。
                // 這裡再以當下尺寸重新 clamp 一次並重試，避免直接拋出。
                var total = GetSplitTotalLength(sc);
                var min = sc.Panel1MinSize;
                var max = total - sc.Panel2MinSize;
                if (total <= 0 || max <= min)
                    return;

                try
                {
                    sc.SplitterDistance = Clamp(distance, min, max);
                }
                catch (InvalidOperationException)
                {
                    // 仍可能在某些時刻（例如佈局尚未完成）失敗，延遲重試避免崩潰
                    if (this.IsHandleCreated)
                        this.BeginInvoke(new Action(() => TrySetSplitterDistance(sc, distance)));
                }
            }
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

        private void SetupEventHandlers()
        {
            // TrackBar 值改變事件
            confidenceTrackBar.ValueChanged += (s, e) =>
            {
                confidenceValueLabel.Text = (confidenceTrackBar.Value / 100.0).ToString("F2");
            };

            pixelConfidenceTrackBar.ValueChanged += (s, e) =>
            {
                pixelConfidenceValueLabel.Text = (pixelConfidenceTrackBar.Value / 100.0).ToString("F2");
            };

            iouTrackBar.ValueChanged += (s, e) =>
            {
                iouValueLabel.Text = (iouTrackBar.Value / 100.0).ToString("F2");
            };

            // 模式切換
            monitorModeRadio.CheckedChanged += (s, e) =>
            {
                if (monitorModeRadio.Checked)
                {
                    manualImagePanel.Visible = false;
                    cameraModePanel.Visible = false;
                    startMonitorButton.Visible = true;
                    stopMonitorButton.Visible = true;
                    startButton.Visible = false;
                    stopButton.Visible = false;
                    processSingleFileButton.Visible = false;
                    processBatchButton.Visible = false;
                    progressGroupBox.Visible = false;
                    // 斷開相機（如果已連接）
                    DisconnectCamera();
                }
            };

            manualModeRadio.CheckedChanged += ManualModeRadio_CheckedChanged;
            
            cameraModeRadio.CheckedChanged += CameraModeRadio_CheckedChanged;

            // 監聽單文件路徑 TextBox 的文本變化
            singleFileTextBox.TextChanged += (s, e) =>
            {
                if (manualModeRadio.Checked)
                {
                    UpdateProcessButtonStates();
                }
            };

            // 監聽批量處理路徑 TextBox 的文本變化
            batchFileTextBox.TextChanged += (s, e) =>
            {
                if (manualModeRadio.Checked)
                {
                    UpdateProcessButtonStates();
                }
            };

            // 監聽模型路徑 TextBox 的文本變化
            modelPathTextBox.TextChanged += (s, e) =>
            {
                if (manualModeRadio.Checked)
                {
                    UpdateProcessButtonStates();
                }
            };

            // 監聽輸出目錄 TextBox 的文本變化
            outputPathTextBox.TextChanged += (s, e) =>
            {
                if (manualModeRadio.Checked)
                {
                    UpdateProcessButtonStates();
                }
            };
        }

        private void ManualModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            AddLog($"=== ManualModeRadio_CheckedChanged 觸發 ===");
            AddLog($"Checked = {manualModeRadio.Checked}");
            AddLog($"manualImagePanel 存在: {manualImagePanel != null}");
            AddLog($"processSingleFileButton 存在: {processSingleFileButton != null}");
            AddLog($"processBatchButton 存在: {processBatchButton != null}");
            AddLog($"progressGroupBox 存在: {progressGroupBox != null}");

            if (manualModeRadio.Checked)
            {
                AddLog("開始設置手動模式控件可見性");

                try
                {
                    manualImagePanel.Visible = true;
                    AddLog($"✓ manualImagePanel.Visible = {manualImagePanel.Visible}");
                    AddLog($"✓ manualImagePanel.Controls.Count = {manualImagePanel.Controls.Count}");

                    // 檢查 manualImageTable 的內容
                    if (manualImagePanel.Controls.Count > 0 && manualImagePanel.Controls[0] is TableLayoutPanel manualImageTable)
                    {
                        AddLog($"✓ manualImageTable.RowCount = {manualImageTable.RowCount}");
                        AddLog($"✓ manualImageTable.Controls.Count = {manualImageTable.Controls.Count}");
                        for (int i = 0; i < manualImageTable.Controls.Count; i++)
                        {
                            var ctrl = manualImageTable.Controls[i];
                            AddLog($"  - Control[{i}]: {ctrl.GetType().Name}, Visible={ctrl.Visible}");
                        }
                    }

                    startMonitorButton.Visible = false;
                    stopMonitorButton.Visible = false;
                    startButton.Visible = false;
                    stopButton.Visible = true;
                    AddLog($"✓ stopButton.Visible = {stopButton.Visible}");

                    processSingleFileButton.Visible = true;
                    AddLog($"✓ processSingleFileButton.Visible = {processSingleFileButton.Visible}");

                    processBatchButton.Visible = true;
                    AddLog($"✓ processBatchButton.Visible = {processBatchButton.Visible}");

                    progressGroupBox.Visible = true;
                    AddLog($"✓ progressGroupBox.Visible = {progressGroupBox.Visible}");

                    UpdateProcessButtonStates();

                    // 先調整 configGroupBox 高度（在刷新之前）
                    if (configGroupBox != null)
                    {
                        configGroupBox.Height = 320;  // 增加高度以顯示手動模式控件
                        AddLog($"✓ 調整 configGroupBox.Height = {configGroupBox.Height}");
                    }

                    // 強制刷新所有父容器
                    if (manualImagePanel.Parent != null)
                    {
                        // 如果父容器是 TableLayoutPanel，強制重新計算布局
                        if (manualImagePanel.Parent is TableLayoutPanel parentTable)
                        {
                            parentTable.PerformLayout();
                            AddLog($"✓ 執行 parentTable.PerformLayout()");
                        }

                        manualImagePanel.Parent.Invalidate();
                        manualImagePanel.Parent.Update();
                        AddLog($"✓ 刷新 manualImagePanel.Parent: {manualImagePanel.Parent.GetType().Name}");
                    }

                    // 刷新 configGroupBox
                    if (configGroupBox != null)
                    {
                        configGroupBox.Invalidate();
                        configGroupBox.Update();
                        configGroupBox.PerformLayout();
                        AddLog($"✓ 刷新 configGroupBox");
                    }

                    if (processSingleFileButton.Parent != null)
                    {
                        processSingleFileButton.Parent.Invalidate();
                        processSingleFileButton.Parent.Update();
                        AddLog($"✓ 刷新 processSingleFileButton.Parent: {processSingleFileButton.Parent.GetType().Name}");
                    }

                    if (progressGroupBox.Parent != null)
                    {
                        progressGroupBox.Parent.Invalidate();
                        progressGroupBox.Parent.Update();
                        AddLog($"✓ 刷新 progressGroupBox.Parent: {progressGroupBox.Parent.GetType().Name}");
                    }

                    this.Invalidate();
                    this.Update();
                    this.Refresh();
                    this.PerformLayout();

                    AddLog("=== 已切換到手動處理模式 - 所有控件已顯示 ===");
                }
                catch (Exception ex)
                {
                    AddLog($"錯誤: {ex.Message}");
                    AddLog($"堆棧: {ex.StackTrace}");
                }
            }
            else
            {
                // 當切換到其他模式時，隱藏手動模式的控件
                manualImagePanel.Visible = false;
                processSingleFileButton.Visible = false;
                processBatchButton.Visible = false;
                progressGroupBox.Visible = false;
                AddLog("已切換離開手動模式");
            }
        }

        private void CameraModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (cameraModeRadio.Checked)
            {
                try
                {
                    // 隱藏其他模式的控件
                    manualImagePanel.Visible = false;
                    cameraModePanel.Visible = true;
                    startMonitorButton.Visible = false;
                    stopMonitorButton.Visible = false;
                    processSingleFileButton.Visible = false;
                    processBatchButton.Visible = false;
                    progressGroupBox.Visible = false;
                    
                    // 初始化相機列表
                    CheckForCameras();
                    
                    AddLog("已切換到相機模式");
                }
                catch (Exception ex)
                {
                    AddLog($"切換到相機模式時發生錯誤: {ex.Message}");
                }
            }
            else
            {
                // 檢查相機是否已連接
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    // 阻止切換，恢復選中狀態
                    cameraModeRadio.Checked = true;
                    
                    // 顯示提示對話框（異步顯示，避免阻塞）
                    this.BeginInvoke(new Action(() =>
                    {
                        ShowCameraDisconnectDialog();
                    }));
                }
                else
                {
                    // 相機未連接，允許切換
                    cameraModePanel.Visible = false;
                    AddLog("已切換離開相機模式");
                }
            }
        }
        
        private void ShowCameraDisconnectDialog()
        {
            // 創建自定義對話框
            var dialog = new Form
            {
                Text = "相機連接中",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Width = 400,
                Height = 180,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };

            // 提示標籤
            var messageLabel = new Label
            {
                Text = "⚠️ 相機目前處於連接狀態。\n\n請先斷開相機後才能切換到其他模式。",
                Dock = DockStyle.Top,
                Padding = new Padding(20, 20, 20, 10),
                AutoSize = false,
                Height = 80,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            // 按鈕面板
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            // 取消按鈕
            var cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
            cancelButton.Click += (s, e) => dialog.Close();

            // 斷開相機按鈕
            var disconnectButton = new Button
            {
                Text = "斷開相機",
                Width = 120,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            disconnectButton.FlatAppearance.BorderSize = 0;
            disconnectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
            disconnectButton.Click += (s, e) =>
            {
                // 斷開相機
                DisconnectCamera();
                
                // 關閉對話框
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
                
                // 等待一小段時間確保相機完全斷開
                System.Threading.Thread.Sleep(100);
                
                // 允許切換模式（暫時取消事件處理，避免重複觸發）
                cameraModeRadio.CheckedChanged -= CameraModeRadio_CheckedChanged;
                try
                {
                    cameraModeRadio.Checked = false;
                }
                finally
                {
                    cameraModeRadio.CheckedChanged += CameraModeRadio_CheckedChanged;
                }
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(new Label { Width = 10 }); // 間距
            buttonPanel.Controls.Add(disconnectButton);

            dialog.Controls.Add(messageLabel);
            dialog.Controls.Add(buttonPanel);

            // 顯示對話框
            dialog.ShowDialog(this);
        }

        // ========== 線程安全更新方法 ==========

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
                using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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


        private void AddLog(string message)
        {
            InvokeUI(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                logTextBox.AppendText($"[{timestamp}] {message}\r\n");
                logTextBox.SelectionStart = logTextBox.Text.Length;
                logTextBox.ScrollToCaret();
            });
        }

        private void UpdateStatistics()
        {
            InvokeUI(() =>
            {
                totalCountLabel.Text = $"總處理數: {_totalCount}";
                ngCountLabel.Text = _ngCount.ToString();
                okCountLabel.Text = _okCount.ToString();

                if (_totalCount > 0)
                {
                    var yieldRate = (double)_okCount / _totalCount * 100.0;
                    yieldRateLabel.Text = $"{yieldRate:F2}%";
                }
                else
                {
                    yieldRateLabel.Text = "0.00%";
                }
            });
        }

        // ========== SKBitmap 轉換為 Bitmap ==========

        private Bitmap SKBitmapToBitmap(SKBitmap skBitmap)
        {
            // 方法 1: 通過 PNG 編碼（較慢但可靠，推薦用於小圖片）
            using (var image = SKImage.FromBitmap(skBitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                var stream = data.AsStream();
                return new Bitmap(stream);
            }
        }

        // 方法 2: 直接像素複製（較快，適合大圖片，但需要處理格式轉換）
        private Bitmap SKBitmapToBitmapFast(SKBitmap skBitmap)
        {
            var bitmap = new Bitmap(skBitmap.Width, skBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            try
            {
                var srcPtr = skBitmap.GetPixels();
                var dstPtr = bitmapData.Scan0;
                var bytesPerPixel = 4; // ARGB
                var rowBytes = bitmap.Width * bytesPerPixel;

                unsafe
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        var srcRow = (byte*)srcPtr + (y * skBitmap.RowBytes);
                        var dstRow = (byte*)dstPtr + (y * bitmapData.Stride);

                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            // SKBitmap 是 RGBA，需要轉換為 ARGB
                            dstRow[x * 4 + 0] = srcRow[x * 4 + 2]; // B -> B
                            dstRow[x * 4 + 1] = srcRow[x * 4 + 1]; // G -> G
                            dstRow[x * 4 + 2] = srcRow[x * 4 + 0]; // R -> R
                            dstRow[x * 4 + 3] = srcRow[x * 4 + 3]; // A -> A
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        private void ShowImageAtIndex(int index)
        {
            if (index < 0 || index >= _resultBitmaps.Count)
                return;

            InvokeUI(() =>
            {
                _currentImageIndex = index;
                _currentResultBitmap = _resultBitmaps[index];

                // 設置 PictureBox 顯示圖片
                resultPictureBox.Image = _currentResultBitmap;
                resultPictureBox.SizeMode = PictureBoxSizeMode.Zoom; // 保持寬高比縮放

                // 隱藏"暫無圖片"標籤
                noImageLabel.Visible = false;

                // 更新導航
                UpdateImageNavigation();

                // 載入並顯示 JSON 資訊
                if (index < _resultImagePaths.Count)
                {
                    LoadAndDisplayJson(_resultImagePaths[index]);
                }
                else
                {
                    jsonInfoTextBox.Text = "查無該 JSON 訊息";
                }
            });
        }

        private void UpdateImageNavigation()
        {
            InvokeUI(() =>
            {
                if (_resultBitmaps.Count <= 1)
                {
                    imageControlPanel.Visible = false;
                    return;
                }

                imageControlPanel.Visible = true;
                imageCounterLabel.Text = $"{_currentImageIndex + 1} / {_resultBitmaps.Count}";
                previousImageButton.Enabled = _currentImageIndex > 0;
                nextImageButton.Enabled = _currentImageIndex < _resultBitmaps.Count - 1;
            });
        }

        // ========== 文件對話框 ==========

        private void BrowseModelButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "ONNX模型文件 (*.onnx)|*.onnx|所有文件 (*.*)|*.*";
                dialog.Title = "選擇模型文件";

                // 設置初始目錄：如果 TextBox 有路徑，使用其目錄；否則使用項目下的 test\assets\Models
                if (!string.IsNullOrWhiteSpace(modelPathTextBox.Text) && File.Exists(modelPathTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(modelPathTextBox.Text);
                }
                else
                {
                    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    var projectRoot = FindProjectRoot(currentDir);
                    if (projectRoot != null)
                    {
                        var modelDir = Path.Combine(projectRoot, "test", "assets", "Models");
                        if (Directory.Exists(modelDir))
                        {
                            dialog.InitialDirectory = modelDir;
                        }
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    modelPathTextBox.Text = dialog.FileName;
                    AddLog($"已選擇模型: {dialog.FileName}");
                    SavePathsToConfig();
                    UpdateProcessButtonStates();
                    
                    // 自動初始化新模型
                    _ = LoadModelAsync();
                }
            }
        }
        
        // 統一的模型加載方法
        private async Task LoadModelAsync()
        {
            if (string.IsNullOrWhiteSpace(modelPathTextBox.Text) || !File.Exists(modelPathTextBox.Text))
            {
                AddLog("模型路徑無效，無法加載模型");
                return;
            }

            try
            {
                InvokeUI(() =>
                {
                    AddLog("正在初始化模型...");
                    statusLabel.Text = "正在初始化模型...";
                });

                // 在背景執行緒加載模型，避免阻塞UI
                await Task.Run(() =>
                {
                    // 釋放舊模型
                    _yolo?.Dispose();
                    _yolo = null;

                    // 加載新模型
                    _yolo = new Yolo(new YoloOptions
                    {
                        ExecutionProvider = new CpuExecutionProvider(model: modelPathTextBox.Text),
                        ImageResize = ImageResize.Stretched,
                        SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                    });
                });

                // 更新當前模型路徑
                _currentModelPath = modelPathTextBox.Text;

                InvokeUI(() =>
                {
                    AddLog($"模型加載成功: {_yolo?.ModelInfo}");
                    statusLabel.Text = "模型加載成功";
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"模型初始化失敗: {ex.Message}");
                    statusLabel.Text = "模型加載失敗";
                    MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        private void BrowseWatchPathButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇監控目錄";

                // 設置初始目錄：如果 TextBox 有路徑，使用該路徑；否則使用項目根目錄
                if (!string.IsNullOrWhiteSpace(watchPathTextBox.Text) && Directory.Exists(watchPathTextBox.Text))
                {
                    dialog.InitialDirectory = watchPathTextBox.Text;
                }
                else
                {
                    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    var projectRoot = FindProjectRoot(currentDir);
                    if (projectRoot != null)
                    {
                        dialog.InitialDirectory = projectRoot;
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    watchPathTextBox.Text = dialog.SelectedPath;
                    AddLog($"已選擇監控目錄: {dialog.SelectedPath}");
                    SavePathsToConfig();
                }
            }
        }

        private void BrowseOutputButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇輸出目錄";

                // 設置初始目錄：如果 TextBox 有路徑，使用該路徑；否則使用項目下的 Output
                if (!string.IsNullOrWhiteSpace(outputPathTextBox.Text) && Directory.Exists(outputPathTextBox.Text))
                {
                    dialog.InitialDirectory = outputPathTextBox.Text;
                }
                else
                {
                    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    var projectRoot = FindProjectRoot(currentDir);
                    if (projectRoot != null)
                    {
                        var outputDir = Path.Combine(projectRoot, "Output");
                        if (Directory.Exists(outputDir))
                        {
                            dialog.InitialDirectory = outputDir;
                        }
                        else
                        {
                            dialog.InitialDirectory = projectRoot;
                        }
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    outputPathTextBox.Text = dialog.SelectedPath;
                    _outputFolder = dialog.SelectedPath;
                    AddLog($"已選擇輸出目錄: {dialog.SelectedPath}");
                    SavePathsToConfig();
                    UpdateProcessButtonStates();
                }
            }
        }

        private void BrowseSingleFileButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "圖片文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件 (*.*)|*.*";
                dialog.Title = "選擇圖片文件";

                // 設置初始目錄：如果 TextBox 有路徑，使用其目錄；否則使用項目下的 VirtualIndustrialPC
                if (!string.IsNullOrWhiteSpace(singleFileTextBox.Text) && File.Exists(singleFileTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(singleFileTextBox.Text);
                }
                else
                {
                    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    var projectRoot = FindProjectRoot(currentDir);
                    if (projectRoot != null)
                    {
                        var imageDir = Path.Combine(projectRoot, "VirtualIndustrialPC");
                        if (Directory.Exists(imageDir))
                        {
                            dialog.InitialDirectory = imageDir;
                        }
                        else
                        {
                            dialog.InitialDirectory = projectRoot;
                        }
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    singleFileTextBox.Text = dialog.FileName;
                    AddLog($"已選擇單文件: {dialog.FileName}");
                    SavePathsToConfig();
                    UpdateProcessButtonStates();
                }
            }
        }

        private void BrowseBatchFileButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇批量處理目錄";

                // 設置初始目錄：如果 TextBox 有路徑，使用該路徑；否則使用項目下的 VirtualIndustrialPC
                if (!string.IsNullOrWhiteSpace(batchFileTextBox.Text) && Directory.Exists(batchFileTextBox.Text))
                {
                    dialog.InitialDirectory = batchFileTextBox.Text;
                }
                else
                {
                    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    var projectRoot = FindProjectRoot(currentDir);
                    if (projectRoot != null)
                    {
                        var imageDir = Path.Combine(projectRoot, "VirtualIndustrialPC");
                        if (Directory.Exists(imageDir))
                        {
                            dialog.InitialDirectory = imageDir;
                        }
                        else
                        {
                            dialog.InitialDirectory = projectRoot;
                        }
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    batchFileTextBox.Text = dialog.SelectedPath;
                    AddLog($"已選擇批量處理目錄: {dialog.SelectedPath}");
                    SavePathsToConfig();
                    UpdateProcessButtonStates();
                }
            }
        }

        

private void EnsureMonitorWorkerStarted()
{
    if (_monitorWorkerTask != null && !_monitorWorkerTask.IsCompleted) return;

    _monitorCts?.Cancel();
    _monitorCts?.Dispose();
    _monitorCts = new CancellationTokenSource();

    _monitorWorkerTask = Task.Run(() => MonitorWorkerLoopAsync(_monitorCts.Token));
}

private void EnqueueMaterialWork(string materialDirPath, string reason)
{
    if (string.IsNullOrWhiteSpace(materialDirPath)) return;
    _monitorQueue.Writer.TryWrite(new MonitorWorkItem(materialDirPath, reason));
}

private async Task MonitorWorkerLoopAsync(CancellationToken ct)
{
    await foreach (var item in _monitorQueue.Reader.ReadAllAsync(ct))
    {
        ct.ThrowIfCancellationRequested();

        // 去重：同一料號在處理中就略過（避免 Created 事件抖動導致重入）
        if (!_inFlightMaterials.TryAdd(item.MaterialDirPath, 0))
            continue;

        try
        {
            await ProcessMaterialDirectory(item.MaterialDirPath, ct);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            InvokeUI(() => AddLog($"[MonitorWorker] {item.MaterialDirPath} 失敗: {ex.Message}"));
        }
        finally
        {
            _inFlightMaterials.TryRemove(item.MaterialDirPath, out _);
        }
    }
}
// ========== 監控功能 ==========

        private async void StartMonitorButton_Click(object sender, EventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(modelPathTextBox.Text) || !File.Exists(modelPathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(watchPathTextBox.Text) || !Directory.Exists(watchPathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的監控目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPathTextBox.Text))
            {
                MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 創建輸出目錄
            _outputFolder = outputPathTextBox.Text;
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // 初始化 Yolo（如果尚未加載或模型路徑已改變）
            if (_yolo == null || _currentModelPath == null || 
                !_currentModelPath.Equals(modelPathTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                await LoadModelAsync();
                if (_yolo == null)
                {
                    return; // 模型加載失敗
                }
            }

            // 重置統計信息
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _processedMaterialDirs.Clear();
            UpdateStatistics();

            EnsureMonitorWorkerStarted();

            // 啟動目錄監控
            try
            {
                _fileSystemWatcher = new FileSystemWatcher(watchPathTextBox.Text)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _fileSystemWatcher.Created += FileSystemWatcher_Created;
                _fileSystemWatcher.Error += FileSystemWatcher_Error;

                AddLog($"開始監控目錄: {watchPathTextBox.Text}");
                statusLabel.Text = "監控中...";
                monitorStatusLabel.Text = "監控狀態: 運行中";
                monitorStatusLabel.ForeColor = Color.Green;

                // 處理已存在的目錄
                await ProcessExistingDirectories(watchPathTextBox.Text);

                startMonitorButton.Enabled = false;
                stopMonitorButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"啟動監控失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddLog($"啟動監控失敗: {ex.Message}");
            }
        }

        private void StopMonitorButton_Click(object sender, EventArgs e)
        {
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Created -= FileSystemWatcher_Created;
                _fileSystemWatcher.Error -= FileSystemWatcher_Error;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;

            // 停止背景監控工作
            try
            {
                _monitorCts?.Cancel();
            }
            catch { }
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
            statusLabel.Text = "監控已停止";
            monitorStatusLabel.Text = "監控狀態: 未啟動";
            monitorStatusLabel.ForeColor = Color.Gray;

            startMonitorButton.Enabled = true;
            stopMonitorButton.Enabled = false;
        }

        private async void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(1000); // 延遲確保目錄完全創建

            if (Directory.Exists(e.FullPath))
            {
                string watchPath = string.Empty;
                InvokeUI(() =>
                {
                    watchPath = watchPathTextBox.Text;
                });

                if (string.IsNullOrEmpty(watchPath))
                    return;

                var parentPath = Path.GetDirectoryName(e.FullPath);

                if (string.Equals(parentPath, watchPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 料號目錄
                    EnqueueMaterialWork(e.FullPath, "material_created");
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

                    InvokeUI(() =>
                    {
                        AddLog($"  已為料號目錄創建工站監控器: {Path.GetFileName(materialDirPath)}");
                    });
                }
                catch (Exception ex)
                {
                    InvokeUI(() =>
                    {
                        AddLog($"  創建工站監控器失敗: {ex.Message}");
                    });
                }
            }
        }

        private async void MaterialWatcher_StationCreated(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(1000); // 延遲確保目錄完全創建

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
                        InvokeUI(() =>
                        {
                            AddLog($"檢測到新工站目錄: {Path.GetFileName(materialDirPath)}/{stationName}");
                        });

                        // 重新處理料號目錄（會包含新創建的工站）
                        // 先從已處理列表中移除，以便重新處理
                        lock (_processingLock)
                        {
                            _processedMaterialDirs.Remove(materialDirPath);
                        }

                        EnqueueMaterialWork(materialDirPath, "station_created");
                    }
                }
            }
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            InvokeUI(() =>
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
                    EnqueueMaterialWork(dir, "existing_dir");
                    // 為每個料號目錄創建監控器
                    CreateMaterialWatcher(dir);
                }
            }
            catch (Exception ex)
            {
                AddLog($"處理現有目錄時發生錯誤: {ex.Message}");
            }
        }

        // ========== 圖像處理 ==========

        private async Task ProcessMaterialDirectory(string materialDirPath, CancellationToken ct)
        {
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
                    InvokeUI(() =>
                    {
                        currentMaterialLabel.Text = $"當前料號: {materialDirName}";
                        AddLog($"檢測到新料號目錄: {materialDirName}");
                    });

                    // 獲取所有工站目錄
                    var stationDirs = Directory.GetDirectories(materialDirPath)
                        .Where(d => Path.GetFileName(d).StartsWith("S", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(d => d)
                        .ToList();

                    if (stationDirs.Count == 0)
                    {
                        InvokeUI(() =>
                        {
                            AddLog($"  警告: 料號目錄 {materialDirName} 中沒有找到工站目錄");
                        });
                        return;
                    }

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

                        InvokeUI(() =>
                        {
                            AddLog($"  工站 {stationName}: {stationImages.Count} 張圖片");
                        });
                    }

                    if (allImageFiles.Count == 0)
                    {
                        InvokeUI(() =>
                        {
                            AddLog($"  警告: 料號目錄 {materialDirName} 中沒有找到圖片文件");
                        });
                        return;
                    }

                    // 獲取參數
                    double confidence = 0.24;
                    double pixelConfidence = 0.5;
                    double iou = 0.7;
                    InvokeUI(() =>
                    {
                        confidence = confidenceTrackBar.Value / 100.0;
                        pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
                        iou = iouTrackBar.Value / 100.0;
                    });

                    // 處理所有圖片
                    foreach (var imagePath in allImageFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            var fileName = Path.GetFileName(imagePath);
                            var relativePath = Path.GetRelativePath(materialDirPath, imagePath);

                            InvokeUI(() =>
                            {
                                currentFileLabel.Text = $"當前文件: {materialDirName}/{relativePath}";
                                AddLog($"  處理: {relativePath}");
                            });

                            // 等待檔案寫入完成，避免讀到半檔
                            await WaitFileReadyAsync(imagePath, ct);

                            // 加載圖片
                            await WaitFileReadyAsync(imagePath, ct);
                    using var image = SKBitmap.Decode(imagePath);
                            if (image == null)
                            {
                                InvokeUI(() =>
                                {
                                    AddLog($"    -> 錯誤: 無法加載圖片");
                                });
                                continue;
                            }

                            // 運行檢測
                            await _inferenceGate.WaitAsync(ct);
                            List<YoloDotNet.Models.Segmentation> results;
                            try
                            {
                                results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                            }
                            finally
                            {
                                _inferenceGate.Release();
                            }

                            stopwatch.Stop();
                            var processingTime = stopwatch.ElapsedMilliseconds;

                            // 確定結果
                            string suffix;
                            bool isNg = results.Count > 0;
                            if (isNg)
                            {
                                Interlocked.Increment(ref _ngCount);
                                suffix = "NG";
                                InvokeUI(() =>
                                {
                                    AddLog($"    -> 檢測到 {results.Count} 個目標，標記為 NG");
                                });
                            }
                            else
                            {
                                Interlocked.Increment(ref _okCount);
                                suffix = "OK";
                                InvokeUI(() =>
                                {
                                    AddLog($"    -> 未檢測到目標，標記為 OK");
                                });
                            }

                            // 繪製結果
                            image.Draw(results, _drawingOptions);

                            // 保存結果
                            var fileExtension = Path.GetExtension(imagePath);
                            var outputMaterialDir = Path.Combine(_outputFolder, materialDirName);
                            var outputStationDir = Path.Combine(outputMaterialDir, Path.GetFileName(Path.GetDirectoryName(imagePath)!));
                            Directory.CreateDirectory(outputStationDir);

                            var newFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{suffix}{fileExtension}";
                            var outputPath = Path.Combine(outputStationDir, newFileName);

                            var encodedFormat = GetEncodedFormat(fileExtension);
                            image.Save(outputPath, encodedFormat, 80);

                            // 保存 JSON 檔案（如果啟用）
                            bool shouldGenerateJson = false;
                            InvokeUI(() =>
                            {
                                shouldGenerateJson = generateJsonRadio.Checked;
                            });

                            if (shouldGenerateJson)
                            {
                                SaveJsonFile(outputPath, "MonitorMode", confidence, pixelConfidence, iou, results);
                            }

                            Interlocked.Increment(ref _totalCount);

                            // 轉換為 Bitmap 並更新顯示
                            var bitmap = SKBitmapToBitmap(image);
                            InvokeUI(() =>
                            {
                                _resultBitmaps.Add(bitmap);
                                _resultImagePaths.Add(outputPath);
                                _currentImageIndex = _resultBitmaps.Count - 1;
                                ShowImageAtIndex(_currentImageIndex);
                                processingSpeedLabel.Text = $"處理速度: {processingTime} ms";
                                AddLog($"    -> 已保存到: {outputPath}");
                                UpdateStatistics();
                            });
                        }
                        catch (Exception ex)
                        {
                            InvokeUI(() =>
                            {
                                AddLog($"    -> 錯誤: 處理 {Path.GetFileName(imagePath)} 時發生異常: {ex.Message}");
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvokeUI(() =>
                    {
                        AddLog($"處理料號目錄時發生錯誤: {ex.Message}");
                    });
                }
            });
        }

        // ========== 其他方法 ==========

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

        private void UpdateProcessButtonStates()
        {
            // 只在手動模式下更新按鈕狀態
            if (!manualModeRadio.Checked)
            {
                return;
            }

            bool hasModelPath = !string.IsNullOrWhiteSpace(modelPathTextBox.Text) && File.Exists(modelPathTextBox.Text);
            bool hasOutputPath = !string.IsNullOrWhiteSpace(outputPathTextBox.Text);

            // 檢查單文件路徑
            bool hasSingleFilePath = !string.IsNullOrWhiteSpace(singleFileTextBox.Text);
            bool isValidSingleFile = hasSingleFilePath && File.Exists(singleFileTextBox.Text) && !Directory.Exists(singleFileTextBox.Text);
            processSingleFileButton.Enabled = hasModelPath && hasOutputPath && isValidSingleFile;

            // 檢查批量處理路徑
            bool hasBatchFilePath = !string.IsNullOrWhiteSpace(batchFileTextBox.Text);
            bool isValidBatchDirectory = hasBatchFilePath && Directory.Exists(batchFileTextBox.Text) && !File.Exists(batchFileTextBox.Text);
            processBatchButton.Enabled = hasModelPath && hasOutputPath && isValidBatchDirectory;
        }

        private void InitializeDefaultPaths()
        {
            // 嘗試查找項目根目錄
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectRoot = FindProjectRoot(currentDir);

            // 設置默認路徑
            string? defaultModelPath = null;
            string? defaultOutputPath = null;

            if (projectRoot != null)
            {
                // 模型文件預設路徑：項目根目錄下的 test\assets\Models\sd900.onnx
                var sd900Model = Path.Combine(projectRoot, "test", "assets", "Models", "sd900.onnx");
                if (File.Exists(sd900Model))
                {
                    defaultModelPath = sd900Model;
                }

                // 輸出目錄預設路徑：項目根目錄下的 Output 目錄
                // 先檢查是否已存在（不區分大小寫），如果存在則使用實際的路徑
                var existingDirs = Directory.GetDirectories(projectRoot);
                var existingOutputDir = existingDirs.FirstOrDefault(d =>
                    string.Equals(Path.GetFileName(d), "Output", StringComparison.OrdinalIgnoreCase));

                string outputDir;
                if (existingOutputDir != null)
                {
                    // 使用已存在的目錄（保留實際大小寫）
                    outputDir = existingOutputDir;
                }
                else
                {
                    // 創建新目錄，使用大寫 Output
                    outputDir = Path.Combine(projectRoot, "Output");
                }

                try
                {
                    // 如果目錄不存在，創建它
                    if (!Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    defaultOutputPath = outputDir;
                }
                catch (Exception ex)
                {
                    AddLog($"無法創建預設輸出目錄 {outputDir}: {ex.Message}");
                    // 如果創建失敗，使用桌面目錄作為備用
                    defaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Industry_Results");
                }
            }

            // 如果找不到項目根目錄，使用桌面目錄作為備用
            if (defaultOutputPath == null)
            {
                defaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Industry_Results");
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
                                modelPathTextBox.Text = config.ModelPath;
                            }
                            else
                            {
                                invalidPaths.Add($"模型文件路徑無效: {config.ModelPath}");
                                if (defaultModelPath != null)
                                {
                                    modelPathTextBox.Text = defaultModelPath;
                                }
                            }
                        }
                        else if (defaultModelPath != null)
                        {
                            modelPathTextBox.Text = defaultModelPath;
                        }

                        // 檢查並應用監控目錄路徑
                        if (!string.IsNullOrEmpty(config.WatchPath))
                        {
                            if (Directory.Exists(config.WatchPath))
                            {
                                watchPathTextBox.Text = config.WatchPath;
                            }
                            else
                            {
                                invalidPaths.Add($"監控目錄路徑無效: {config.WatchPath}");
                            }
                        }

                        // 檢查並應用輸出目錄路徑
                        if (!string.IsNullOrEmpty(config.OutputPath))
                        {
                            // 檢查是否為舊的預設路徑，如果是則更新為新的預設路徑
                            string outputPathToUse = config.OutputPath;
                            if (projectRoot != null && !string.IsNullOrEmpty(defaultOutputPath))
                            {
                                var oldDefaultPath = Path.Combine(projectRoot, "VirtualIndustrialPC", "lines");
                                if (string.Equals(config.OutputPath, oldDefaultPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 自動遷移到新的預設路徑
                                    outputPathToUse = defaultOutputPath;
                                    AddLog($"檢測到舊的預設輸出目錄，已自動更新為新的預設路徑");
                                }
                            }

                            try
                            {
                                if (!Directory.Exists(outputPathToUse))
                                {
                                    Directory.CreateDirectory(outputPathToUse);
                                }
                                _outputFolder = outputPathToUse;
                                outputPathTextBox.Text = outputPathToUse;

                                // 如果路徑被更新，保存到配置文件
                                if (outputPathToUse != config.OutputPath)
                                {
                                    SavePathsToConfig();
                                }
                            }
                            catch
                            {
                                invalidPaths.Add($"輸出目錄路徑無效或無法創建: {outputPathToUse}");
                                _outputFolder = defaultOutputPath;
                                outputPathTextBox.Text = defaultOutputPath;
                            }
                        }
                        else
                        {
                            _outputFolder = defaultOutputPath;
                            outputPathTextBox.Text = defaultOutputPath;
                        }

                        // 檢查並應用單文件路徑
                        if (!string.IsNullOrEmpty(config.SingleFilePath))
                        {
                            if (File.Exists(config.SingleFilePath) && !Directory.Exists(config.SingleFilePath))
                            {
                                singleFileTextBox.Text = config.SingleFilePath;
                            }
                            else
                            {
                                invalidPaths.Add($"單文件路徑無效: {config.SingleFilePath}");
                            }
                        }

                        // 檢查並應用批量處理路徑
                        if (!string.IsNullOrEmpty(config.BatchFilePath))
                        {
                            if (Directory.Exists(config.BatchFilePath) && !File.Exists(config.BatchFilePath))
                            {
                                batchFileTextBox.Text = config.BatchFilePath;
                            }
                            else
                            {
                                invalidPaths.Add($"批量處理路徑無效: {config.BatchFilePath}");
                            }
                        }

                        // 更新按鈕狀態
                        UpdateProcessButtonStates();

                        // 如果有無效路徑，顯示提示訊息
                        if (invalidPaths.Count > 0)
                        {
                            var message = "配置文件中的以下路徑無效，已使用預設路徑：\n\n" + string.Join("\n", invalidPaths);
                            MessageBox.Show(message, "路徑驗證警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    MessageBox.Show($"讀取配置文件失敗: {ex.Message}\n\n將使用預設路徑。", "配置文件錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    SingleFilePath = string.Empty,
                    BatchFilePath = string.Empty,
                    MainSplitterRatio = 0.3333,  // 預設平均分布：圖片區域 33.33%
                    RightSplitterRatio = 0.5     // 預設平均分布：終端和 JSON 各 50%
                };

                var jsonContent = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, jsonContent);
                AddLog("已創建默認配置文件");
                // 布局比例將在 MainForm_Load 中應用
            }
            catch (Exception ex)
            {
                AddLog($"創建配置文件失敗: {ex.Message}");
            }

            // 使用默認值
            _outputFolder = defaultOutputPath;
            outputPathTextBox.Text = defaultOutputPath;

            if (defaultModelPath != null)
            {
                modelPathTextBox.Text = defaultModelPath;
            }

            // 初始化完成後，更新按鈕狀態
            if (manualModeRadio.Checked)
            {
                UpdateProcessButtonStates();
            }
        }

        private void SavePathsToConfig()
        {
            try
            {
                // 計算當前布局比例
                double? mainRatio = null;
                double? rightRatio = null;

                // 依 SplitContainer 的 Orientation 決定用 Height 或 Width
                if (mainSplitContainer != null)
                {
                    var total = GetSplitTotalLength(mainSplitContainer);
                    if (total > 0)
                        mainRatio = (double)mainSplitContainer.SplitterDistance / total;
                }

                if (rightSplitContainer != null)
                {
                    var total = GetSplitTotalLength(rightSplitContainer);
                    if (total > 0)
                        rightRatio = (double)rightSplitContainer.SplitterDistance / total;
                }

                var config = new PathConfig
                {
                    ModelPath = modelPathTextBox.Text,
                    WatchPath = watchPathTextBox.Text,
                    OutputPath = outputPathTextBox.Text,
                    SingleFilePath = singleFileTextBox.Text,
                    BatchFilePath = batchFileTextBox.Text,
                    MainSplitterRatio = mainRatio,
                    RightSplitterRatio = rightRatio
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
            public string? SingleFilePath { get; set; }
            public string? BatchFilePath { get; set; }
            public double? MainSplitterRatio { get; set; }  // 主分隔線比例（圖片區域佔比）
            public double? RightSplitterRatio { get; set; }  // 右側分隔線比例（終端區域佔比）
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

        private void PreviousImageButton_Click(object sender, EventArgs e)
        {
            if (_currentImageIndex > 0)
            {
                _currentImageIndex--;
                ShowImageAtIndex(_currentImageIndex);
            }
        }

        private void NextImageButton_Click(object sender, EventArgs e)
        {
            if (_currentImageIndex < _resultBitmaps.Count - 1)
            {
                _currentImageIndex++;
                ShowImageAtIndex(_currentImageIndex);
            }
        }

        private void OpenOutputFolderButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_outputFolder) || !Directory.Exists(_outputFolder))
            {
                MessageBox.Show("輸出目錄不存在！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", _outputFolder);
        }

        // ========== 手動處理模式方法 ==========

        private async void StartButton_Click(object sender, EventArgs e)
        {
            // StartButton 已移除，現在使用獨立的處理按鈕（processSingleFileButton 和 processBatchButton）
            // 保留此方法以避免 Designer 錯誤，但方法為空
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在停止處理...");
            statusLabel.Text = "正在停止...";

            // 重新啟用處理按鈕
            processSingleFileButton.Enabled = true;
            processBatchButton.Enabled = true;
        }

        private async void ProcessSingleFileButton_Click(object sender, EventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(modelPathTextBox.Text) || !File.Exists(modelPathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(singleFileTextBox.Text) || !File.Exists(singleFileTextBox.Text))
            {
                MessageBox.Show("請選擇有效的圖片文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPathTextBox.Text))
            {
                MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 創建輸出目錄
            _outputFolder = outputPathTextBox.Text;
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // 初始化 Yolo（如果尚未加載或模型路徑已改變）
            if (_yolo == null || _currentModelPath == null || 
                !_currentModelPath.Equals(modelPathTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                await LoadModelAsync();
                if (_yolo == null)
                {
                    return; // 模型加載失敗
                }
            }

            // 重置統計信息和圖片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/啟用按鈕
            processSingleFileButton.Enabled = false;
            processBatchButton.Enabled = false;
            stopButton.Enabled = true;
            progressBar.Value = 0;
            imageControlPanel.Visible = false;

            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            // 獲取參數值
            var confidence = confidenceTrackBar.Value / 100.0;
            var pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
            var iou = iouTrackBar.Value / 100.0;

            // 開始處理單文件
            try
            {
                await ProcessSingleFile(singleFileTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AddLog("處理已取消");
                statusLabel.Text = "處理已取消";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"處理過程中發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddLog($"錯誤: {ex.Message}");
            }
            finally
            {
                processSingleFileButton.Enabled = true;
                processBatchButton.Enabled = true;
                stopButton.Enabled = false;
                statusLabel.Text = "就緒";
            }
        }

        private async void ProcessBatchButton_Click(object sender, EventArgs e)
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(modelPathTextBox.Text) || !File.Exists(modelPathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(batchFileTextBox.Text) || !Directory.Exists(batchFileTextBox.Text))
            {
                MessageBox.Show("請選擇有效的批量處理目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPathTextBox.Text))
            {
                MessageBox.Show("請選擇輸出目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 創建輸出目錄
            _outputFolder = outputPathTextBox.Text;
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // 初始化 Yolo（如果尚未加載或模型路徑已改變）
            if (_yolo == null || _currentModelPath == null || 
                !_currentModelPath.Equals(modelPathTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                await LoadModelAsync();
                if (_yolo == null)
                {
                    return; // 模型加載失敗
                }
            }

            // 重置統計信息和圖片列表
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            _currentImageIndex = -1;
            ClearResultBitmaps();
            UpdateStatistics();

            // 禁用/啟用按鈕
            processSingleFileButton.Enabled = false;
            processBatchButton.Enabled = false;
            stopButton.Enabled = true;
            progressBar.Value = 0;
            imageControlPanel.Visible = false;

            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            // 獲取參數值
            var confidence = confidenceTrackBar.Value / 100.0;
            var pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
            var iou = iouTrackBar.Value / 100.0;

            // 開始批量處理
            try
            {
                await ProcessBatchFiles(batchFileTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AddLog("處理已取消");
                statusLabel.Text = "處理已取消";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"處理過程中發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddLog($"錯誤: {ex.Message}");
            }
            finally
            {
                processSingleFileButton.Enabled = true;
                processBatchButton.Enabled = true;
                stopButton.Enabled = false;
                statusLabel.Text = "就緒";
            }
        }

        private async Task ProcessSingleFile(string imagePath, double confidence, double pixelConfidence, double iou, CancellationToken cancellationToken)
        {
            // 重置計數器
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            InvokeUI(() =>
            {
                UpdateStatistics();
                progressBar.Maximum = 1;
                progressBar.Value = 0;
            });

            await Task.Run(async () =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(imagePath);
                    InvokeUI(() =>
                    {
                        currentFileLabel.Text = $"當前文件: {fileName}";
                        AddLog($"處理: {fileName}");
                        statusLabel.Text = $"正在處理: {fileName}";
                    });

                    // 等待檔案寫入完成，避免讀到半檔
                    await WaitFileReadyAsync(imagePath, cancellationToken);

                    // 加載圖片
                    await WaitFileReadyAsync(imagePath, cancellationToken);
                    using var image = SKBitmap.Decode(imagePath);
                    if (image == null)
                    {
                        throw new Exception($"無法加載圖片: {imagePath}");
                    }

                    // 運行檢測
                    await _inferenceGate.WaitAsync(cancellationToken);
                    List<YoloDotNet.Models.Segmentation> results;
                    try
                    {
                        results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                    }
                    finally
                    {
                        _inferenceGate.Release();
                    }

                    stopwatch.Stop();
                    var processingTime = stopwatch.ElapsedMilliseconds;

                    // 確定結果
                    string suffix;
                    _totalCount++;
                    if (results.Count > 0)
                    {
                        _ngCount++;
                        suffix = "NG";
                        InvokeUI(() =>
                        {
                            AddLog($"  -> 檢測到 {results.Count} 個目標，標記為 NG");
                        });
                    }
                    else
                    {
                        _okCount++;
                        suffix = "OK";
                        InvokeUI(() =>
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

                    // 保存 JSON 檔案（如果啟用）
                    bool shouldGenerateJson = false;
                    InvokeUI(() =>
                    {
                        shouldGenerateJson = generateJsonRadio.Checked;
                    });

                    if (shouldGenerateJson)
                    {
                        SaveJsonFile(outputPath, "ManualMode-SingleFile", confidence, pixelConfidence, iou, results);
                    }

                    // 轉換為 Bitmap 並更新顯示
                    var bitmap = SKBitmapToBitmap(image);
                    InvokeUI(() =>
                    {
                        _resultBitmaps.Add(bitmap);
                        _resultImagePaths.Add(outputPath);
                        _currentImageIndex = _resultBitmaps.Count - 1;
                        ShowImageAtIndex(_currentImageIndex);
                        processingSpeedLabel.Text = $"處理速度: {processingTime} ms";
                        AddLog($"  -> 已保存到: {outputPath}");
                        UpdateStatistics();
                        progressBar.Value = 1;

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
                    InvokeUI(() =>
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
                InvokeUI(() =>
                {
                    MessageBox.Show($"在目錄 {imageDirectory} 中找不到圖片文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
                return;
            }

            // 重置計數器
            _totalCount = 0;
            _ngCount = 0;
            _okCount = 0;
            InvokeUI(() =>
            {
                UpdateStatistics();
                progressBar.Maximum = imageFiles.Count;
                progressBar.Value = 0;
            });

            int processedCount = 0;

            await Task.Run(async () =>
            {
                foreach (var imagePath in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var fileName = Path.GetFileName(imagePath);
                        InvokeUI(() =>
                        {
                            currentFileLabel.Text = $"當前文件: {fileName}";
                            AddLog($"處理: {fileName}");
                            statusLabel.Text = $"正在處理: {fileName} ({processedCount + 1}/{imageFiles.Count})";
                        });

                        // 加載圖片
                        await WaitFileReadyAsync(imagePath, cancellationToken);
                    using var image = SKBitmap.Decode(imagePath);
                        if (image == null)
                        {
                            InvokeUI(() =>
                            {
                                AddLog($"  -> 錯誤: 無法加載圖片");
                            });
                            continue;
                        }

                        // 運行檢測
                        await _inferenceGate.WaitAsync(cancellationToken);
                        List<YoloDotNet.Models.Segmentation> results;
                        try
                        {
                            results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                        }
                        finally
                        {
                            _inferenceGate.Release();
                        }

                        stopwatch.Stop();
                        var processingTime = stopwatch.ElapsedMilliseconds;

                        // 確定結果
                        string suffix;
                        _totalCount++;
                        if (results.Count > 0)
                        {
                            _ngCount++;
                            suffix = "NG";
                            InvokeUI(() =>
                            {
                                AddLog($"  -> 檢測到 {results.Count} 個目標，標記為 NG");
                            });
                        }
                        else
                        {
                            _okCount++;
                            suffix = "OK";
                            InvokeUI(() =>
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

                        // 保存 JSON 檔案（如果啟用）
                        bool shouldGenerateJson = false;
                        InvokeUI(() =>
                        {
                            shouldGenerateJson = generateJsonRadio.Checked;
                        });

                        if (shouldGenerateJson)
                        {
                            SaveJsonFile(outputPath, "ManualMode-Batch", confidence, pixelConfidence, iou, results);
                        }

                        processedCount++;

                        // 轉換為 Bitmap 並更新顯示
                        var bitmap = SKBitmapToBitmap(image);
                        InvokeUI(() =>
                        {
                            _resultBitmaps.Add(bitmap);
                            _resultImagePaths.Add(outputPath);
                            _currentImageIndex = _resultBitmaps.Count - 1;
                            ShowImageAtIndex(_currentImageIndex);
                            processingSpeedLabel.Text = $"處理速度: {processingTime} ms";
                            AddLog($"  -> 已保存到: {outputPath}");
                            UpdateStatistics();
                            progressBar.Value = processedCount;
                            progressTextLabel.Text = $"{processedCount} / {imageFiles.Count}";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        InvokeUI(() =>
                        {
                            AddLog($"  -> 錯誤: 處理 {Path.GetFileName(imagePath)} 時發生異常: {ex.Message}");
                        });
                    }
                }
            }, cancellationToken);

            InvokeUI(() =>
            {
                AddLog($"處理完成！總共處理: {processedCount} 個文件");
                statusLabel.Text = "處理完成";

                if (_resultBitmaps.Count > 0)
                {
                    UpdateImageNavigation();
                }
            });
        }

        private void ClearResultBitmaps()
        {
            _currentResultBitmap = null;

            foreach (var bitmap in _resultBitmaps)
            {
                try
                {
                    bitmap?.Dispose();
                }
                catch { }
            }
            _resultBitmaps.Clear();
            _resultImagePaths.Clear();
            _currentImageIndex = -1;

            InvokeUI(() =>
            {
                resultPictureBox.Image = null;
                noImageLabel.Visible = true;
                imageControlPanel.Visible = false;
                jsonInfoTextBox.Text = "查無該 JSON 訊息";
            });
        }

        // ========== JSON 相關方法 ==========

        private class DetectionResultJson
        {
            public string Mode { get; set; } = string.Empty;
            public double Confidence { get; set; }
            public double PixelConfidence { get; set; }
            public double IoU { get; set; }
            public int DetectionCount { get; set; }
            public List<DetectionInfo> Detections { get; set; } = new List<DetectionInfo>();
        }

        private class DetectionInfo
        {
            public string Label { get; set; } = string.Empty;
            public double Confidence { get; set; }
        }

        private void SaveJsonFile(string imageOutputPath, string mode, double confidence, double pixelConfidence, double iou, List<YoloDotNet.Models.Segmentation> results)
        {
            try
            {
                var jsonPath = Path.ChangeExtension(imageOutputPath, ".json");
                var jsonData = new DetectionResultJson
                {
                    Mode = mode,
                    Confidence = confidence,
                    PixelConfidence = pixelConfidence,
                    IoU = iou,
                    DetectionCount = results.Count,
                    Detections = results.Select(r => new DetectionInfo
                    {
                        Label = r.Label.Name,
                        Confidence = r.Confidence
                    }).ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonContent = JsonSerializer.Serialize(jsonData, options);
                File.WriteAllText(jsonPath, jsonContent);
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"保存 JSON 檔案失敗: {ex.Message}");
                });
            }
        }

        private void LoadAndDisplayJson(string imagePath)
        {
            try
            {
                var jsonPath = Path.ChangeExtension(imagePath, ".json");
                if (File.Exists(jsonPath))
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var jsonData = JsonSerializer.Deserialize<DetectionResultJson>(jsonContent);

                    if (jsonData != null)
                    {
                        var displayText = $"模式: {jsonData.Mode}\r\n";
                        displayText += $"Confidence: {jsonData.Confidence:F2}\r\n";
                        displayText += $"Pixel Confidence: {jsonData.PixelConfidence:F2}\r\n";
                        displayText += $"IoU: {jsonData.IoU:F2}\r\n";
                        displayText += $"檢測目標數: {jsonData.DetectionCount}\r\n";

                        if (jsonData.DetectionCount > 0)
                        {
                            displayText += $"\r\n瑕疵資訊:\r\n";
                            for (int i = 0; i < jsonData.Detections.Count; i++)
                            {
                                var det = jsonData.Detections[i];
                                displayText += $"  {i + 1}. {det.Label} (Confidence: {det.Confidence:F2})\r\n";
                            }
                        }
                        else
                        {
                            displayText += "\r\n未檢測到瑕疵";
                        }

                        jsonInfoTextBox.Text = displayText;
                    }
                    else
                    {
                        jsonInfoTextBox.Text = "查無該 JSON 訊息";
                    }
                }
                else
                {
                    jsonInfoTextBox.Text = "查無該 JSON 訊息";
                }
            }
            catch (Exception ex)
            {
                jsonInfoTextBox.Text = $"讀取 JSON 失敗: {ex.Message}";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _yolo?.Dispose();

            // 停止所有監控器
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }

            lock (_processingLock)
            {
                foreach (var watcher in _materialWatchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    catch { }
                }
                _materialWatchers.Clear();
            }

            // 釋放所有 Bitmap
            ClearResultBitmaps();

            // 斷開相機
            DisconnectCamera();

            base.OnFormClosing(e);
        }
    

private void InitSplitContainersSafe()
        {
            if (_splittersInitialized || _splitterInitInProgress)
                return;

            if (!this.IsHandleCreated)
                return;

            _splitterInitInProgress = true;

            this.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (mainSplitContainer != null)
                        ApplySplitterSafe(mainSplitContainer, min1: 100, min2: 200, ratio: 0.33);

                    if (rightSplitContainer != null)
                        ApplySplitterSafe(rightSplitContainer, min1: 200, min2: 200, ratio: 0.50);

                    _splittersInitialized = true;
                }
                finally
                {
                    _splitterInitInProgress = false;
                }
            }));
        }

        private static void ApplySplitterSafe(SplitContainer sc, int min1, int min2, double ratio)
        {
            int total = (sc.Orientation == Orientation.Horizontal) ? sc.Height : sc.Width;
            if (total <= 0)
                return;

            // If too small, keep mins at 0 for now to avoid InvalidOperationException.
            if (total <= (min1 + min2 + sc.SplitterWidth + 2))
            {
                sc.Panel1MinSize = 0;
                sc.Panel2MinSize = 0;
                return;
            }

            sc.Panel1MinSize = min1;
            sc.Panel2MinSize = min2;

            int minDistance = sc.Panel1MinSize;
            int maxDistance = total - sc.Panel2MinSize;

            if (maxDistance <= minDistance)
                return;

            int desired = (int)(total * ratio);
            if (desired < minDistance) desired = minDistance;
            if (desired > maxDistance) desired = maxDistance;

            sc.SplitterDistance = desired;
        }

        // ========== 相機功能 ==========

        private void CheckForCameras()
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                if (_videoDevices.Count == 0)
                {
                    InvokeUI(() =>
                    {
                        lblCameraStatus.Text = "相機狀態: 未偵測到相機";
                        lblCameraStatus.ForeColor = Color.Red;
                        AddLog("未偵測到相機設備");
                    });
                    MessageBox.Show("未偵測到相機設備！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                InvokeUI(() =>
                {
                    cmbCameras.Items.Clear();
                    foreach (FilterInfo device in _videoDevices)
                    {
                        cmbCameras.Items.Add(device.Name);
                    }
                    if (cmbCameras.Items.Count > 0)
                    {
                        cmbCameras.SelectedIndex = 0;
                    }
                    lblCameraStatus.Text = $"相機狀態: 偵測到 {_videoDevices.Count} 個相機";
                    lblCameraStatus.ForeColor = Color.Gray;
                    AddLog($"偵測到 {_videoDevices.Count} 個相機設備");
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"檢查相機時發生錯誤: {ex.Message}");
                    lblCameraStatus.Text = "相機狀態: 檢查失敗";
                    lblCameraStatus.ForeColor = Color.Red;
                });
            }
        }

        private void BtnConnectCamera_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    // 斷開連接
                    DisconnectCamera();
                }
                else
                {
                    // 連接相機
                    if (_videoDevices == null || _videoDevices.Count == 0)
                    {
                        MessageBox.Show("沒有可用的相機設備！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (cmbCameras.SelectedIndex < 0)
                    {
                        MessageBox.Show("請選擇一個相機！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _videoSource = new VideoCaptureDevice(_videoDevices[cmbCameras.SelectedIndex].MonikerString);
                    _videoSource.NewFrame += VideoSource_NewFrame;
                    _videoSource.Start();
                    
                    InvokeUI(() =>
                    {
                        btnConnectCamera.Text = "斷開相機";
                        btnCaptureCamera.Enabled = true;
                        btnBurstCapture.Enabled = true;
                        lblCameraStatus.Text = "相機狀態: 已連接";
                        lblCameraStatus.ForeColor = Color.Green;
                        AddLog("相機已連接");
                    });
                }
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"連接相機時發生錯誤: {ex.Message}");
                    lblCameraStatus.Text = "相機狀態: 連接失敗";
                    lblCameraStatus.ForeColor = Color.Red;
                });
                MessageBox.Show($"連接相機時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisconnectCamera()
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= VideoSource_NewFrame;
                    _videoSource = null;
                }

                lock (_cameraFrameLock)
                {
                    _currentCameraFrame?.Dispose();
                    _currentCameraFrame = null;
                }

                InvokeUI(() =>
                {
                    btnConnectCamera.Text = "連接相機";
                    btnCaptureCamera.Enabled = false;
                    btnBurstCapture.Enabled = false;
                    lblCameraStatus.Text = "相機狀態: 未連接";
                    lblCameraStatus.ForeColor = Color.Gray;
                    resultPictureBox.Image = null;
                    noImageLabel.Visible = true;
                    AddLog("相機已斷開");
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"斷開相機時發生錯誤: {ex.Message}");
                });
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // 異步更新當前畫面快照
                _ = Task.Run(() =>
                {
                    Bitmap? clonedFrame = null;
                    try
                    {
                        clonedFrame = (Bitmap)eventArgs.Frame.Clone();
                    }
                    catch
                    {
                        clonedFrame?.Dispose();
                        return;
                    }

                    // 更新快照
                    lock (_cameraFrameLock)
                    {
                        _currentCameraFrame?.Dispose();
                        _currentCameraFrame = clonedFrame;
                    }
                });

                // 更新預覽畫面（使用現有的 resultPictureBox）
                InvokeUI(() =>
                {
                    try
                    {
                        Bitmap? previewFrame = null;
                        lock (_cameraFrameLock)
                        {
                            if (_currentCameraFrame != null)
                            {
                                previewFrame = (Bitmap)_currentCameraFrame.Clone();
                            }
                        }

                        if (previewFrame == null)
                        {
                            previewFrame = (Bitmap)eventArgs.Frame.Clone();
                        }

                        if (previewFrame != null)
                        {
                            var oldImage = resultPictureBox.Image;
                            resultPictureBox.Image = previewFrame;
                            oldImage?.Dispose();
                            noImageLabel.Visible = false;
                        }
                    }
                    catch
                    {
                        // 忽略錯誤，避免影響預覽
                    }
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"顯示相機畫面時發生錯誤: {ex.Message}");
                });
            }
        }

        private async void BtnCaptureCamera_Click(object? sender, EventArgs e)
        {
            if (_videoSource == null || !_videoSource.IsRunning)
            {
                MessageBox.Show("請先連接相機！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_yolo == null)
            {
                MessageBox.Show("請先初始化模型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_isCapturing) return;

            btnCaptureCamera.Enabled = false;
            _isCapturing = true;
            double delaySeconds = (double)numCaptureDelay.Value;

            if (delaySeconds > 0)
            {
                InvokeUI(() =>
                {
                    AddLog($"將在 {delaySeconds} 秒後拍照...");
                });

                // 倒數計時
                while (delaySeconds > 0 && _isCapturing)
                {
                    await Task.Delay(100);
                    delaySeconds -= 0.1;
                }
            }

            if (!_isCapturing)
            {
                btnCaptureCamera.Enabled = true;
                return;
            }

            try
            {
                Bitmap? frameToProcess = null;

                // 從當前畫面快照獲取最新畫面
                lock (_cameraFrameLock)
                {
                    if (_currentCameraFrame != null)
                    {
                        frameToProcess = (Bitmap)_currentCameraFrame.Clone();
                    }
                }

                if (frameToProcess == null && resultPictureBox.Image != null)
                {
                    frameToProcess = (Bitmap)resultPictureBox.Image.Clone();
                }

                if (frameToProcess != null)
                {
                    // 轉換為 SKBitmap 進行處理
                    using var skBitmap = BitmapToSKBitmap(frameToProcess);
                    if (skBitmap != null)
                    {
                        await ProcessCameraImage(skBitmap, "CameraCapture");
                    }
                    frameToProcess.Dispose();
                }
                else
                {
                    InvokeUI(() =>
                    {
                        AddLog("無法拍照：沒有畫面");
                        MessageBox.Show("無法拍照：沒有畫面", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"拍照時發生錯誤: {ex.Message}");
                    MessageBox.Show($"拍照時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                _isCapturing = false;
                btnCaptureCamera.Enabled = true;
            }
        }

        private async void BtnBurstCapture_Click(object? sender, EventArgs e)
        {
            if (_videoSource == null || !_videoSource.IsRunning)
            {
                MessageBox.Show("請先連接相機！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_yolo == null)
            {
                MessageBox.Show("請先初始化模型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_isCapturing) return;

            btnBurstCapture.Enabled = false;
            _isCapturing = true;
            int burstCount = (int)numBurstCount.Value;

            try
            {
                InvokeUI(() =>
                {
                    AddLog($"⚡ 開始連拍模式：1 秒內拍攝 {burstCount} 張照片並進行檢測...");
                });

                DateTime burstStartTime = DateTime.Now;
                double totalDuration = 1000.0; // 總共 1 秒
                double interval = totalDuration / burstCount; // 每張照片的間隔時間（毫秒）

                var processTaskList = new List<Task>();

                for (int i = 0; i < burstCount && _isCapturing; i++)
                {
                    Bitmap? currentFrameToProcess = null;

                    // 每次拍照都獲取最新的畫面
                    lock (_cameraFrameLock)
                    {
                        if (_currentCameraFrame != null)
                        {
                            currentFrameToProcess = (Bitmap)_currentCameraFrame.Clone();
                        }
                    }

                    if (currentFrameToProcess == null && resultPictureBox.Image != null)
                    {
                        currentFrameToProcess = (Bitmap)resultPictureBox.Image.Clone();
                    }

                    if (currentFrameToProcess != null)
                    {
                        var frame = currentFrameToProcess; // 捕獲變數
                        var frameIndex = i + 1;

                        // 創建異步處理任務（不阻塞拍攝循環）
                        var processTask = Task.Run(async () =>
                        {
                            try
                            {
                                using var skBitmap = BitmapToSKBitmap(frame);
                                if (skBitmap != null)
                                {
                                    await ProcessCameraImage(skBitmap, $"CameraBurst_{frameIndex}of{burstCount}");
                                }
                            }
                            finally
                            {
                                frame.Dispose();
                            }
                        });

                        processTaskList.Add(processTask);

                        // 更新進度顯示
                        InvokeUI(() =>
                        {
                            double elapsed = (DateTime.Now - burstStartTime).TotalMilliseconds;
                            double remaining = Math.Max(0, totalDuration - elapsed);
                            AddLog($"連拍進度：{frameIndex}/{burstCount} (剩餘 {remaining:F0}ms)");
                        });
                    }

                    // 計算下一張照片應該拍攝的時間點
                    double elapsedTime = (DateTime.Now - burstStartTime).TotalMilliseconds;
                    double nextShotTime = (i + 1) * interval;
                    double waitTime = Math.Max(0, nextShotTime - elapsedTime);

                    // 如果不是最後一張，等待到正確的時間點
                    if (i < burstCount - 1 && waitTime > 0)
                    {
                        await Task.Delay((int)waitTime);
                    }
                }

                // 等待所有處理任務完成
                if (processTaskList.Count > 0)
                {
                    await Task.WhenAll(processTaskList);
                }

                InvokeUI(() =>
                {
                    AddLog($"✅ 連拍完成：成功處理 {processTaskList.Count}/{burstCount} 張照片");
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"連拍時發生錯誤: {ex.Message}");
                    MessageBox.Show($"連拍時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                _isCapturing = false;
                btnBurstCapture.Enabled = true;
            }
        }

        private async Task ProcessCameraImage(SKBitmap skBitmap, string sourceName)
        {
            try
            {
                // 獲取參數
                double confidence = 0.24;
                double pixelConfidence = 0.5;
                double iou = 0.7;
                InvokeUI(() =>
                {
                    confidence = confidenceTrackBar.Value / 100.0;
                    pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
                    iou = iouTrackBar.Value / 100.0;
                });

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 運行檢測
                await _inferenceGate.WaitAsync();
                List<YoloDotNet.Models.Segmentation> results;
                try
                {
                    results = _yolo!.RunSegmentation(skBitmap, confidence: confidence, 
                                                     pixelConfedence: pixelConfidence, iou: iou);
                }
                finally
                {
                    _inferenceGate.Release();
                }

                stopwatch.Stop();
                var processingTime = stopwatch.ElapsedMilliseconds;

                // 確定結果
                string suffix;
                bool isNg = results.Count > 0;
                if (isNg)
                {
                    Interlocked.Increment(ref _ngCount);
                    suffix = "NG";
                    InvokeUI(() =>
                    {
                        AddLog($"  -> {sourceName}: 檢測到 {results.Count} 個目標，標記為 NG");
                    });
                }
                else
                {
                    Interlocked.Increment(ref _okCount);
                    suffix = "OK";
                    InvokeUI(() =>
                    {
                        AddLog($"  -> {sourceName}: 未檢測到目標，標記為 OK");
                    });
                }

                // 繪製結果
                skBitmap.Draw(results, _drawingOptions);

                // 保存結果
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var fileName = $"camera_{sourceName}_{timestamp}_{suffix}.jpg";
                var outputPath = Path.Combine(_outputFolder, fileName);
                
                // 確保輸出目錄存在
                if (!Directory.Exists(_outputFolder))
                {
                    Directory.CreateDirectory(_outputFolder);
                }

                var encodedFormat = SKEncodedImageFormat.Jpeg;
                skBitmap.Save(outputPath, encodedFormat, 80);

                // 保存 JSON 檔案（如果啟用）
                bool shouldGenerateJson = false;
                InvokeUI(() =>
                {
                    shouldGenerateJson = generateJsonRadio.Checked;
                });

                if (shouldGenerateJson)
                {
                    SaveJsonFile(outputPath, sourceName, confidence, pixelConfidence, iou, results);
                }

                Interlocked.Increment(ref _totalCount);

                // 轉換為 Bitmap 並更新顯示
                var bitmap = SKBitmapToBitmap(skBitmap);
                InvokeUI(() =>
                {
                    _resultBitmaps.Add(bitmap);
                    _resultImagePaths.Add(outputPath);
                    _currentImageIndex = _resultBitmaps.Count - 1;
                    ShowImageAtIndex(_currentImageIndex);
                    processingSpeedLabel.Text = $"處理速度: {processingTime} ms";
                    AddLog($"  -> 已保存到: {outputPath}");
                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                InvokeUI(() =>
                {
                    AddLog($"處理相機圖片時發生錯誤: {ex.Message}");
                });
            }
        }

        private SKBitmap? BitmapToSKBitmap(Bitmap bitmap)
        {
            try
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                return SKBitmap.Decode(stream);
            }
            catch
            {
                return null;
            }
        }

    } }