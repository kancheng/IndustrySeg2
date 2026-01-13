# IndustrySegSys Windows Forms 遷移方案

## 📋 現有 WPF 實現分析

### 1. 核心架構特點

#### 1.1 UI 框架
- **框架**: WPF (Windows Presentation Foundation)
- **XAML 布局**: 使用 Grid、StackPanel、GroupBox 等容器
- **數據綁定**: 使用 TextBlock、TextBox 等控件
- **圖像顯示**: 使用 `SkiaSharp.Views.WPF.SKElement` 直接渲染

#### 1.2 線程模型
- **Dispatcher**: 使用 `Dispatcher.Invoke()` 和 `Dispatcher.BeginInvoke()` 進行線程安全的 UI 更新
- **異步處理**: 使用 `async/await` + `Task.Run()` 在後台線程處理圖像
- **線程安全**: 所有 UI 更新都通過 Dispatcher 進行

#### 1.3 關鍵組件

| 組件 | WPF 實現 | 功能 |
|------|---------|------|
| **主窗口** | `Window` | 主應用窗口 |
| **圖像顯示** | `SKElement` (SkiaSharp) | 直接渲染 SKBitmap |
| **布局** | `Grid`, `StackPanel` | 響應式布局 |
| **控件** | `TextBox`, `Button`, `Slider` | 標準 WPF 控件 |
| **線程更新** | `Dispatcher.Invoke()` | 線程安全更新 |
| **文件對話框** | `OpenFileDialog`, `FolderBrowserDialog` | 文件選擇 |

#### 1.4 核心功能流程

```
1. 初始化
   ├── 加載配置文件 (config.json)
   ├── 初始化 YOLO 模型
   └── 設置繪圖選項

2. 自動監控模式
   ├── FileSystemWatcher 監控目錄
   ├── 檢測新料號目錄
   ├── 為料號目錄創建工站監控器
   ├── 處理圖片（異步）
   ├── 更新 UI（通過 Dispatcher）
   └── 保存結果

3. 手動處理模式
   ├── 選擇圖片/目錄
   ├── 批量處理（異步）
   ├── 實時更新進度
   └── 顯示結果
```

---

## ✅ Windows Forms 遷移可行性

### 結論：**完全可行，且相對簡單**

Windows Forms 與 WPF 都是桌面應用框架，遷移難度較低：

| 特性 | WPF | Windows Forms | 遷移難度 |
|------|-----|---------------|---------|
| **線程更新** | `Dispatcher.Invoke()` | `Control.Invoke()` | ⭐ 簡單 |
| **圖像顯示** | `SKElement` | `PictureBox` + `SKBitmap` 轉換 | ⭐⭐ 中等 |
| **布局** | XAML Grid | TableLayoutPanel/FlowLayoutPanel | ⭐⭐ 中等 |
| **文件對話框** | `OpenFileDialog` | `OpenFileDialog` (相同) | ⭐ 簡單 |
| **異步處理** | `async/await` | `async/await` (相同) | ⭐ 簡單 |
| **FileSystemWatcher** | 直接使用 | 直接使用 (相同) | ⭐ 簡單 |

---

## 🏗️ Windows Forms 架構設計

### 1. 項目結構

```
IndustrySegSys.WinForms/
├── MainForm.cs                    # 主窗體
├── MainForm.Designer.cs          # 窗體設計器
├── MainForm.resx                 # 資源文件
│
├── Services/                      # 服務類（可選）
│   ├── MonitoringService.cs      # 監控服務
│   └── ProcessingService.cs      # 處理服務
│
├── Controls/                      # 自定義控件（可選）
│   └── ImageViewer.cs            # 圖像查看控件
│
└── IndustrySegSys.WinForms.csproj
```

### 2. 控件映射表

| WPF 控件 | Windows Forms 控件 | 說明 |
|---------|-------------------|------|
| `Window` | `Form` | 主窗體 |
| `Grid` | `TableLayoutPanel` | 網格布局 |
| `StackPanel` | `FlowLayoutPanel` 或 `Panel` | 流式布局 |
| `GroupBox` | `GroupBox` | 分組框（相同） |
| `TextBox` | `TextBox` | 文本框（相同） |
| `Button` | `Button` | 按鈕（相同） |
| `Slider` | `TrackBar` | 滑塊 |
| `TextBlock` | `Label` | 標籤 |
| `ProgressBar` | `ProgressBar` | 進度條（相同） |
| `StatusBar` | `StatusStrip` | 狀態欄 |
| `ScrollViewer` | `Panel` + `AutoScroll` | 滾動容器 |
| `SKElement` | `PictureBox` + 轉換 | 圖像顯示 |

---

## 🔧 核心實現

### 1. 主窗體設計 (MainForm.Designer.cs)

```csharp
namespace IndustrySegSys.WinForms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        
        // 配置區域控件
        private GroupBox configGroupBox;
        private TextBox modelPathTextBox;
        private Button browseModelButton;
        private TextBox watchPathTextBox;
        private Button browseWatchPathButton;
        private TextBox outputPathTextBox;
        private Button browseOutputButton;
        private RadioButton monitorModeRadio;
        private RadioButton manualModeRadio;
        private Panel manualImagePanel;
        private TextBox imagePathTextBox;
        private Button browseImageButton;
        private RadioButton singleFileRadio;
        private RadioButton batchFileRadio;
        private TrackBar confidenceTrackBar;
        private Label confidenceValueLabel;
        private TrackBar pixelConfidenceTrackBar;
        private Label pixelConfidenceValueLabel;
        private TrackBar iouTrackBar;
        private Label iouValueLabel;
        
        // 控制按鈕
        private Button startMonitorButton;
        private Button stopMonitorButton;
        private Button startButton;
        private Button stopButton;
        private Button processSingleFileButton;
        private Button processBatchButton;
        private Button openOutputFolderButton;
        
        // 主內容區域
        private TableLayoutPanel mainContentPanel;
        private GroupBox imagePreviewGroupBox;
        private Panel imageContainerPanel;
        private PictureBox resultPictureBox;
        private Label noImageLabel;
        private Panel imageControlPanel;
        private Button previousImageButton;
        private Label imageCounterLabel;
        private Button nextImageButton;
        
        // 信息面板
        private GroupBox statisticsGroupBox;
        private Label totalCountLabel;
        private Label ngCountLabel;
        private Label okCountLabel;
        private Label yieldRateLabel;
        private Label currentMaterialLabel;
        private Label currentFileLabel;
        private GroupBox progressGroupBox;
        private ProgressBar progressBar;
        private Label progressTextLabel;
        private Label processingSpeedLabel;
        private GroupBox logGroupBox;
        private TextBox logTextBox;
        
        // 狀態欄
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel monitorStatusLabel;
        
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 900);
            this.Text = "工業檢測系統 - 自動監控模式";
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            
            // 創建控件
            CreateConfigPanel();
            CreateControlButtons();
            CreateMainContent();
            CreateStatusBar();
            
            // 設置布局
            SetupLayout();
        }
        
        private void CreateConfigPanel()
        {
            configGroupBox = new GroupBox
            {
                Text = "配置",
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Height = 200
            };
            
            var configTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            // 第一行：模型文件和監控目錄
            modelPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseModelButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseModelButton.Click += BrowseModelButton_Click;
            
            var modelPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            modelPanel.Controls.Add(new Label { Text = "模型文件:", Width = 100, AutoSize = false });
            modelPanel.Controls.Add(modelPathTextBox);
            modelPanel.Controls.Add(browseModelButton);
            configTable.Controls.Add(modelPanel, 0, 0);
            
            watchPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseWatchPathButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseWatchPathButton.Click += BrowseWatchPathButton_Click;
            
            var watchPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            watchPanel.Controls.Add(new Label { Text = "監控目錄:", Width = 100, AutoSize = false });
            watchPanel.Controls.Add(watchPathTextBox);
            watchPanel.Controls.Add(browseWatchPathButton);
            configTable.Controls.Add(watchPanel, 1, 0);
            
            // 第二行：輸出目錄和工作模式
            outputPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseOutputButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseOutputButton.Click += BrowseOutputButton_Click;
            
            var outputPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            outputPanel.Controls.Add(new Label { Text = "輸出目錄:", Width = 100, AutoSize = false });
            outputPanel.Controls.Add(outputPathTextBox);
            outputPanel.Controls.Add(browseOutputButton);
            configTable.Controls.Add(outputPanel, 0, 1);
            
            monitorModeRadio = new RadioButton { Text = "自動監控模式", Checked = true };
            manualModeRadio = new RadioButton { Text = "手動處理模式" };
            
            var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            modePanel.Controls.Add(new Label { Text = "工作模式:", Width = 100, AutoSize = false });
            modePanel.Controls.Add(monitorModeRadio);
            modePanel.Controls.Add(manualModeRadio);
            configTable.Controls.Add(modePanel, 1, 1);
            
            // 第三行：手動模式圖片選擇（初始隱藏）
            manualImagePanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            imagePathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseImageButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseImageButton.Click += BrowseImageButton_Click;
            singleFileRadio = new RadioButton { Text = "單文件", Checked = true };
            batchFileRadio = new RadioButton { Text = "批量處理" };
            
            var imagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            imagePanel.Controls.Add(new Label { Text = "圖片路徑:", Width = 100, AutoSize = false });
            imagePanel.Controls.Add(imagePathTextBox);
            imagePanel.Controls.Add(browseImageButton);
            imagePanel.Controls.Add(new Label { Text = "處理模式:", Width = 100, AutoSize = false, Margin = new Padding(20, 0, 0, 0) });
            imagePanel.Controls.Add(singleFileRadio);
            imagePanel.Controls.Add(batchFileRadio);
            manualImagePanel.Controls.Add(imagePanel);
            configTable.Controls.Add(manualImagePanel, 0, 2);
            configTable.SetColumnSpan(manualImagePanel, 2);
            
            // 第四行：參數設置
            var paramPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            
            confidenceTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 24, Width = 150, TickFrequency = 10 };
            confidenceValueLabel = new Label { Text = "0.24", Width = 50 };
            confidenceTrackBar.ValueChanged += (s, e) => confidenceValueLabel.Text = (confidenceTrackBar.Value / 100.0).ToString("F2");
            
            pixelConfidenceTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 50, Width = 150, TickFrequency = 10 };
            pixelConfidenceValueLabel = new Label { Text = "0.50", Width = 50 };
            pixelConfidenceTrackBar.ValueChanged += (s, e) => pixelConfidenceValueLabel.Text = (pixelConfidenceTrackBar.Value / 100.0).ToString("F2");
            
            iouTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 70, Width = 150, TickFrequency = 10 };
            iouValueLabel = new Label { Text = "0.70", Width = 50 };
            iouTrackBar.ValueChanged += (s, e) => iouValueLabel.Text = (iouTrackBar.Value / 100.0).ToString("F2");
            
            paramPanel.Controls.Add(new Label { Text = "Confidence:", Width = 100, AutoSize = false });
            paramPanel.Controls.Add(confidenceTrackBar);
            paramPanel.Controls.Add(confidenceValueLabel);
            paramPanel.Controls.Add(new Label { Text = "Pixel Confidence:", Width = 120, AutoSize = false, Margin = new Padding(20, 0, 0, 0) });
            paramPanel.Controls.Add(pixelConfidenceTrackBar);
            paramPanel.Controls.Add(pixelConfidenceValueLabel);
            paramPanel.Controls.Add(new Label { Text = "IoU:", Width = 50, AutoSize = false, Margin = new Padding(20, 0, 0, 0) });
            paramPanel.Controls.Add(iouTrackBar);
            paramPanel.Controls.Add(iouValueLabel);
            
            configTable.Controls.Add(paramPanel, 0, 3);
            configTable.SetColumnSpan(paramPanel, 2);
            
            configGroupBox.Controls.Add(configTable);
            this.Controls.Add(configGroupBox);
        }
        
        private void CreateControlButtons()
        {
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10)
            };
            
            startMonitorButton = new Button { Text = "開始監控", Width = 120, Height = 35, Font = new Font("Microsoft Sans Serif", 9F) };
            startMonitorButton.Click += StartMonitorButton_Click;
            
            stopMonitorButton = new Button { Text = "停止監控", Width = 120, Height = 35, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            stopMonitorButton.Click += StopMonitorButton_Click;
            
            startButton = new Button { Text = "開始檢測", Width = 120, Height = 35, Visible = false, Font = new Font("Microsoft Sans Serif", 9F) };
            startButton.Click += StartButton_Click;
            
            stopButton = new Button { Text = "停止檢測", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            stopButton.Click += StopButton_Click;
            
            processSingleFileButton = new Button { Text = "處理單文件", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            processSingleFileButton.Click += ProcessSingleFileButton_Click;
            
            processBatchButton = new Button { Text = "批量處理", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            processBatchButton.Click += ProcessBatchButton_Click;
            
            openOutputFolderButton = new Button { Text = "打開輸出文件夾", Width = 150, Height = 35, Font = new Font("Microsoft Sans Serif", 9F) };
            openOutputFolderButton.Click += OpenOutputFolderButton_Click;
            
            buttonPanel.Controls.Add(startMonitorButton);
            buttonPanel.Controls.Add(stopMonitorButton);
            buttonPanel.Controls.Add(startButton);
            buttonPanel.Controls.Add(stopButton);
            buttonPanel.Controls.Add(processSingleFileButton);
            buttonPanel.Controls.Add(processBatchButton);
            buttonPanel.Controls.Add(openOutputFolderButton);
            
            this.Controls.Add(buttonPanel);
        }
        
        private void CreateMainContent()
        {
            mainContentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            mainContentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainContentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5F));
            mainContentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            
            // 左側：圖片預覽區域
            imagePreviewGroupBox = new GroupBox
            {
                Text = "檢測結果預覽",
                Dock = DockStyle.Fill
            };
            
            imageContainerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            
            resultPictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill
            };
            
            noImageLabel = new Label
            {
                Text = "暫無圖片顯示",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft Sans Serif", 18F),
                ForeColor = Color.Gray
            };
            
            imageContainerPanel.Controls.Add(resultPictureBox);
            imageContainerPanel.Controls.Add(noImageLabel);
            noImageLabel.BringToFront();
            
            imageControlPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Visible = false,
                Padding = new Padding(10)
            };
            
            previousImageButton = new Button { Text = "◀ 上一張", Width = 100, Height = 30 };
            previousImageButton.Click += PreviousImageButton_Click;
            
            imageCounterLabel = new Label { Text = "0 / 0", Width = 80, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold) };
            
            nextImageButton = new Button { Text = "下一張 ▶", Width = 100, Height = 30 };
            nextImageButton.Click += NextImageButton_Click;
            
            imageControlPanel.Controls.Add(previousImageButton);
            imageControlPanel.Controls.Add(imageCounterLabel);
            imageControlPanel.Controls.Add(nextImageButton);
            
            var imagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            imagePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            imagePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            imagePanel.Controls.Add(imageContainerPanel, 0, 0);
            imagePanel.Controls.Add(imageControlPanel, 0, 1);
            
            imagePreviewGroupBox.Controls.Add(imagePanel);
            mainContentPanel.Controls.Add(imagePreviewGroupBox, 0, 0);
            
            // 中間：分隔線
            var splitter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray,
                Cursor = Cursors.VSplit
            };
            mainContentPanel.Controls.Add(splitter, 1, 0);
            
            // 右側：信息面板
            var infoPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3
            };
            infoPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            
            // 統計信息
            statisticsGroupBox = new GroupBox
            {
                Text = "統計信息",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };
            
            var statsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2 };
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            totalCountLabel = new Label { Text = "總處理數: 0", Dock = DockStyle.Fill };
            statsPanel.Controls.Add(totalCountLabel, 0, 0);
            statsPanel.SetColumnSpan(totalCountLabel, 2);
            
            // NG/OK 顯示框
            var ngPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(255, 230, 230), Padding = new Padding(10) };
            var ngLabel = new Label { Text = "NG", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Red, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            ngCountLabel = new Label { Text = "0", Font = new Font("Microsoft Sans Serif", 36F, FontStyle.Bold), ForeColor = Color.Red, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            var ngDescLabel = new Label { Text = "(檢測到目標)", Font = new Font("Microsoft Sans Serif", 9F), ForeColor = Color.Red, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter };
            ngPanel.Controls.Add(ngLabel);
            ngPanel.Controls.Add(ngCountLabel);
            ngPanel.Controls.Add(ngDescLabel);
            statsPanel.Controls.Add(ngPanel, 0, 1);
            
            var okPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(230, 255, 230), Padding = new Padding(10) };
            var okLabel = new Label { Text = "OK", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Green, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            okCountLabel = new Label { Text = "0", Font = new Font("Microsoft Sans Serif", 36F, FontStyle.Bold), ForeColor = Color.Green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            var okDescLabel = new Label { Text = "(未檢測到目標)", Font = new Font("Microsoft Sans Serif", 9F), ForeColor = Color.Green, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter };
            okPanel.Controls.Add(okLabel);
            okPanel.Controls.Add(okCountLabel);
            okPanel.Controls.Add(okDescLabel);
            statsPanel.Controls.Add(okPanel, 1, 1);
            
            var yieldPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(230, 243, 255), Padding = new Padding(10) };
            var yieldLabel = new Label { Text = "良率", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Blue, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            yieldRateLabel = new Label { Text = "0.00%", Font = new Font("Microsoft Sans Serif", 48F, FontStyle.Bold), ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            yieldPanel.Controls.Add(yieldLabel);
            yieldPanel.Controls.Add(yieldRateLabel);
            statsPanel.Controls.Add(yieldPanel, 0, 2);
            statsPanel.SetColumnSpan(yieldPanel, 2);
            
            currentMaterialLabel = new Label { Text = "當前料號: 無", Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 0) };
            currentFileLabel = new Label { Text = "當前文件: 無", Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 0) };
            var infoLabelsPanel = new Panel { Dock = DockStyle.Fill };
            infoLabelsPanel.Controls.Add(currentMaterialLabel);
            infoLabelsPanel.Controls.Add(currentFileLabel);
            statsPanel.Controls.Add(infoLabelsPanel, 0, 2);
            statsPanel.SetColumnSpan(infoLabelsPanel, 2);
            
            statisticsGroupBox.Controls.Add(statsPanel);
            infoPanel.Controls.Add(statisticsGroupBox, 0, 0);
            
            // 進度條（初始隱藏）
            progressGroupBox = new GroupBox
            {
                Text = "處理進度",
                Dock = DockStyle.Fill,
                Visible = false,
                Padding = new Padding(5)
            };
            
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20 };
            progressTextLabel = new Label { Text = "0 / 0", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 5, 0, 0) };
            processingSpeedLabel = new Label { Text = "處理速度: --", Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 0) };
            
            var progressPanel = new Panel { Dock = DockStyle.Fill };
            progressPanel.Controls.Add(progressBar);
            progressPanel.Controls.Add(progressTextLabel);
            progressPanel.Controls.Add(processingSpeedLabel);
            progressGroupBox.Controls.Add(progressPanel);
            infoPanel.Controls.Add(progressGroupBox, 0, 1);
            
            // 日誌
            logGroupBox = new GroupBox
            {
                Text = "日誌",
                Dock = DockStyle.Fill
            };
            
            logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(212, 212, 212)
            };
            
            logGroupBox.Controls.Add(logTextBox);
            infoPanel.Controls.Add(logGroupBox, 0, 2);
            
            mainContentPanel.Controls.Add(infoPanel, 2, 0);
            
            this.Controls.Add(mainContentPanel);
        }
        
        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip();
            
            statusLabel = new ToolStripStatusLabel("就緒");
            monitorStatusLabel = new ToolStripStatusLabel("監控狀態: 未啟動")
            {
                ForeColor = Color.Gray
            };
            
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true }); // 彈簧，推動下一個項目到右側
            statusStrip.Items.Add(monitorStatusLabel);
            
            this.Controls.Add(statusStrip);
        }
        
        private void SetupLayout()
        {
            // 設置控件層級順序（從上到下）
            configGroupBox.BringToFront();
            // 其他控件會自動按添加順序排列
        }
    }
}
```

### 2. 主窗體邏輯 (MainForm.cs)

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace IndustrySegSys.WinForms
{
    public partial class MainForm : Form
    {
        private Yolo? _yolo;
        private SegmentationDrawingOptions _drawingOptions = default!;
        private Bitmap? _currentResultBitmap;
        private List<Bitmap> _resultBitmaps = new List<Bitmap>();
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
        
        public MainForm()
        {
            InitializeComponent();
            InitializeDrawingOptions();
            InitializeDefaultPaths();
            SetupEventHandlers();
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
                    startMonitorButton.Visible = true;
                    stopMonitorButton.Visible = true;
                    startButton.Visible = false;
                    stopButton.Visible = false;
                    processSingleFileButton.Visible = false;
                    processBatchButton.Visible = false;
                    progressGroupBox.Visible = false;
                }
            };
            
            manualModeRadio.CheckedChanged += (s, e) =>
            {
                if (manualModeRadio.Checked)
                {
                    manualImagePanel.Visible = true;
                    startMonitorButton.Visible = false;
                    stopMonitorButton.Visible = false;
                    startButton.Visible = false;
                    stopButton.Visible = true;
                    processSingleFileButton.Visible = true;
                    processBatchButton.Visible = true;
                    progressGroupBox.Visible = true;
                    UpdateProcessButtonStates();
                }
            };
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
                totalCountLabel.Text = _totalCount.ToString();
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
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    modelPathTextBox.Text = dialog.FileName;
                    AddLog($"已選擇模型: {dialog.FileName}");
                    SavePathsToConfig();
                    UpdateProcessButtonStates();
                }
            }
        }
        
        private void BrowseWatchPathButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇監控目錄";
                
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
        
        private void BrowseImageButton_Click(object sender, EventArgs e)
        {
            if (singleFileRadio.Checked)
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "圖片文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件 (*.*)|*.*";
                    dialog.Title = "選擇圖片文件";
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        imagePathTextBox.Text = dialog.FileName;
                        singleFileRadio.Checked = true;
                        AddLog($"已選擇圖片: {dialog.FileName}");
                        SavePathsToConfig();
                        UpdateProcessButtonStates();
                    }
                }
            }
            else
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "選擇圖片目錄";
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        imagePathTextBox.Text = dialog.SelectedPath;
                        batchFileRadio.Checked = true;
                        AddLog($"已選擇圖片目錄: {dialog.SelectedPath}");
                        SavePathsToConfig();
                        UpdateProcessButtonStates();
                    }
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
            
            // 初始化 Yolo
            try
            {
                AddLog("正在初始化模型...");
                statusLabel.Text = "正在初始化模型...";
                
                _yolo?.Dispose();
                _yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(model: modelPathTextBox.Text),
                    ImageResize = ImageResize.Stretched,
                    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                });
                
                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                statusLabel.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    await ProcessMaterialDirectory(e.FullPath);
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
                        
                        await ProcessMaterialDirectory(materialDirPath);
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
        
        // ========== 圖像處理 ==========
        
        private async Task ProcessMaterialDirectory(string materialDirPath)
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
                        currentMaterialLabel.Text = materialDirName;
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
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            var fileName = Path.GetFileName(imagePath);
                            var relativePath = Path.GetRelativePath(materialDirPath, imagePath);
                            
                            InvokeUI(() =>
                            {
                                currentFileLabel.Text = $"{materialDirName}/{relativePath}";
                                AddLog($"  處理: {relativePath}");
                            });
                            
                            // 加載圖片
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
                            var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                            
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
                            
                            Interlocked.Increment(ref _totalCount);
                            
                            // 轉換為 Bitmap 並更新顯示
                            var bitmap = SKBitmapToBitmap(image);
                            InvokeUI(() =>
                            {
                                _resultBitmaps.Add(bitmap);
                                _currentImageIndex = _resultBitmaps.Count - 1;
                                ShowImageAtIndex(_currentImageIndex);
                                processingSpeedLabel.Text = $"{processingTime} ms";
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
            bool hasImagePath = !string.IsNullOrWhiteSpace(imagePathTextBox.Text);
            bool hasModelPath = !string.IsNullOrWhiteSpace(modelPathTextBox.Text) && File.Exists(modelPathTextBox.Text);
            bool hasOutputPath = !string.IsNullOrWhiteSpace(outputPathTextBox.Text);
            
            if (singleFileRadio.Checked)
            {
                bool isValidFile = hasImagePath && File.Exists(imagePathTextBox.Text) && !Directory.Exists(imagePathTextBox.Text);
                processSingleFileButton.Enabled = hasModelPath && hasOutputPath && isValidFile;
                processBatchButton.Enabled = false;
            }
            else if (batchFileRadio.Checked)
            {
                bool isValidDirectory = hasImagePath && Directory.Exists(imagePathTextBox.Text) && !File.Exists(imagePathTextBox.Text);
                processSingleFileButton.Enabled = false;
                processBatchButton.Enabled = hasModelPath && hasOutputPath && isValidDirectory;
            }
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
                            try
                            {
                                if (!Directory.Exists(config.OutputPath))
                                {
                                    Directory.CreateDirectory(config.OutputPath);
                                }
                                _outputFolder = config.OutputPath;
                                outputPathTextBox.Text = config.OutputPath;
                            }
                            catch
                            {
                                invalidPaths.Add($"輸出目錄路徑無效或無法創建: {config.OutputPath}");
                                _outputFolder = defaultOutputPath;
                                outputPathTextBox.Text = defaultOutputPath;
                            }
                        }
                        else
                        {
                            _outputFolder = defaultOutputPath;
                            outputPathTextBox.Text = defaultOutputPath;
                        }
                        
                        // 檢查並應用圖片路徑
                        if (!string.IsNullOrEmpty(config.ImagePath))
                        {
                            bool isFile = File.Exists(config.ImagePath) && !Directory.Exists(config.ImagePath);
                            bool isDirectory = Directory.Exists(config.ImagePath) && !File.Exists(config.ImagePath);
                            
                            if (isFile)
                            {
                                imagePathTextBox.Text = config.ImagePath;
                                singleFileRadio.Checked = true;
                            }
                            else if (isDirectory)
                            {
                                imagePathTextBox.Text = config.ImagePath;
                                batchFileRadio.Checked = true;
                            }
                            else
                            {
                                invalidPaths.Add($"圖片路徑無效: {config.ImagePath}");
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
                var config = new PathConfig
                {
                    ModelPath = modelPathTextBox.Text,
                    WatchPath = watchPathTextBox.Text,
                    OutputPath = outputPathTextBox.Text,
                    ImagePath = imagePathTextBox.Text
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
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(modelPathTextBox.Text) || !File.Exists(modelPathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的模型文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(imagePathTextBox.Text))
            {
                MessageBox.Show("請選擇圖片文件或目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (singleFileRadio.Checked && !File.Exists(imagePathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的圖片文件！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (batchFileRadio.Checked && !Directory.Exists(imagePathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的圖片目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            
            // 初始化 Yolo
            try
            {
                AddLog("正在初始化模型...");
                statusLabel.Text = "正在初始化模型...";
                
                _yolo?.Dispose();
                _yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(model: modelPathTextBox.Text),
                    ImageResize = ImageResize.Stretched,
                    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                });
                
                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                statusLabel.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            startButton.Enabled = false;
            stopButton.Enabled = true;
            progressBar.Value = 0;
            imageControlPanel.Visible = false;
            
            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 獲取參數值
            var confidence = confidenceTrackBar.Value / 100.0;
            var pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
            var iou = iouTrackBar.Value / 100.0;
            
            // 開始處理
            try
            {
                if (singleFileRadio.Checked)
                {
                    await ProcessSingleFile(imagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
                }
                else
                {
                    await ProcessBatchFiles(imagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
                }
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
                startButton.Enabled = true;
                stopButton.Enabled = false;
                statusLabel.Text = "就緒";
            }
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
            
            if (string.IsNullOrWhiteSpace(imagePathTextBox.Text) || !File.Exists(imagePathTextBox.Text))
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
            
            // 初始化 Yolo
            try
            {
                AddLog("正在初始化模型...");
                statusLabel.Text = "正在初始化模型...";
                
                _yolo?.Dispose();
                _yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(model: modelPathTextBox.Text),
                    ImageResize = ImageResize.Stretched,
                    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                });
                
                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                statusLabel.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            processSingleFileButton.Enabled = false;
            processBatchButton.Enabled = false;
            stopButton.Enabled = true;
            progressBar.Value = 0;
            imageControlPanel.Visible = false;
            
            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 獲取參數值
            var confidence = confidenceTrackBar.Value / 100.0;
            var pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
            var iou = iouTrackBar.Value / 100.0;
            
            // 開始處理單文件
            try
            {
                await ProcessSingleFile(imagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
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
            
            if (string.IsNullOrWhiteSpace(imagePathTextBox.Text) || !Directory.Exists(imagePathTextBox.Text))
            {
                MessageBox.Show("請選擇有效的圖片目錄！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            
            // 初始化 Yolo
            try
            {
                AddLog("正在初始化模型...");
                statusLabel.Text = "正在初始化模型...";
                
                _yolo?.Dispose();
                _yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(model: modelPathTextBox.Text),
                    ImageResize = ImageResize.Stretched,
                    SamplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.None)
                });
                
                AddLog($"模型加載成功: {_yolo.ModelInfo}");
                statusLabel.Text = "模型加載成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模型初始化失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            processSingleFileButton.Enabled = false;
            processBatchButton.Enabled = false;
            stopButton.Enabled = true;
            progressBar.Value = 0;
            imageControlPanel.Visible = false;
            
            // 創建取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 獲取參數值
            var confidence = confidenceTrackBar.Value / 100.0;
            var pixelConfidence = pixelConfidenceTrackBar.Value / 100.0;
            var iou = iouTrackBar.Value / 100.0;
            
            // 開始批量處理
            try
            {
                await ProcessBatchFiles(imagePathTextBox.Text, confidence, pixelConfidence, iou, _cancellationTokenSource.Token);
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
            
            await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var fileName = Path.GetFileName(imagePath);
                    InvokeUI(() =>
                    {
                        currentFileLabel.Text = fileName;
                        AddLog($"處理: {fileName}");
                        statusLabel.Text = $"正在處理: {fileName}";
                    });
                    
                    // 加載圖片
                    using var image = SKBitmap.Decode(imagePath);
                    if (image == null)
                    {
                        throw new Exception($"無法加載圖片: {imagePath}");
                    }
                    
                    // 運行檢測
                    var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                    
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
                    
                    // 轉換為 Bitmap 並更新顯示
                    var bitmap = SKBitmapToBitmap(image);
                    InvokeUI(() =>
                    {
                        _resultBitmaps.Add(bitmap);
                        _currentImageIndex = _resultBitmaps.Count - 1;
                        ShowImageAtIndex(_currentImageIndex);
                        processingSpeedLabel.Text = $"{processingTime} ms";
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
            
            await Task.Run(() =>
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
                            currentFileLabel.Text = fileName;
                            AddLog($"處理: {fileName}");
                            statusLabel.Text = $"正在處理: {fileName} ({processedCount + 1}/{imageFiles.Count})";
                        });
                        
                        // 加載圖片
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
                        var results = _yolo!.RunSegmentation(image, confidence: confidence, pixelConfedence: pixelConfidence, iou: iou);
                        
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
                        
                        processedCount++;
                        
                        // 轉換為 Bitmap 並更新顯示
                        var bitmap = SKBitmapToBitmap(image);
                        InvokeUI(() =>
                        {
                            _resultBitmaps.Add(bitmap);
                            _currentImageIndex = _resultBitmaps.Count - 1;
                            ShowImageAtIndex(_currentImageIndex);
                            processingSpeedLabel.Text = $"{processingTime} ms";
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
            _currentImageIndex = -1;
            
            InvokeUI(() =>
            {
                resultPictureBox.Image = null;
                noImageLabel.Visible = true;
                imageControlPanel.Visible = false;
            });
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
            
            base.OnFormClosing(e);
        }
    }
}
```

---

## 🔄 關鍵遷移點

### 1. 線程更新機制

**WPF:**
```csharp
_dispatcher.Invoke(() => {
    // 更新 UI
});
```

**Windows Forms:**
```csharp
if (InvokeRequired)
{
    Invoke(() => {
        // 更新 UI
    });
}
else
{
    // 更新 UI
}
```

### 2. 圖像顯示

**WPF:**
```csharp
// 直接使用 SKElement 渲染
<skia:SKElement x:Name="ResultImageElement"
               PaintSurface="ResultImageElement_PaintSurface"/>
```

**Windows Forms:**
```csharp
// 轉換 SKBitmap 為 Bitmap，然後顯示在 PictureBox
var bitmap = SKBitmapToBitmap(skBitmap);
resultPictureBox.Image = bitmap;
resultPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
```

### 3. 布局系統

**WPF:**
```xml
<Grid>
    <Grid.RowDefinitions>...</Grid.RowDefinitions>
    <Grid.ColumnDefinitions>...</Grid.ColumnDefinitions>
</Grid>
```

**Windows Forms:**
```csharp
var tableLayout = new TableLayoutPanel();
tableLayout.RowCount = 3;
tableLayout.ColumnCount = 2;
tableLayout.Controls.Add(control, column, row);
```

---

## 📦 項目配置

### IndustrySegSys.WinForms.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SkiaSharp" Version="3.119.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\YoloDotNet.ExecutionProvider.Cpu\YoloDotNet.ExecutionProvider.Cpu.csproj" />
    <ProjectReference Include="..\YoloDotNet\YoloDotNet.csproj" />
  </ItemGroup>

</Project>
```

---

## ✅ 遷移步驟

### 階段 1: 創建項目
1. 創建新的 Windows Forms 項目
2. 安裝必要的 NuGet 包
3. 設置項目引用

### 階段 2: UI 設計
1. 使用設計器創建窗體布局
2. 添加所有必要的控件
3. 設置控件屬性和事件處理程序

### 階段 3: 核心邏輯遷移
1. 遷移配置管理邏輯
2. 遷移文件監控邏輯
3. 遷移圖像處理邏輯
4. 實現線程安全更新

### 階段 4: 圖像顯示
1. 實現 SKBitmap 到 Bitmap 的轉換
2. 實現 PictureBox 顯示邏輯
3. 實現圖片導航功能

### 階段 5: 測試和優化
1. 測試所有功能
2. 優化性能
3. 修復問題

---

## ⚠️ 注意事項

### 1. 內存管理
- **重要**: Windows Forms 的 `Bitmap` 需要手動釋放
- 使用 `using` 語句或 `Dispose()` 方法
- 在窗體關閉時釋放所有 Bitmap

### 2. 線程安全
- 所有 UI 更新必須通過 `Invoke()` 或 `BeginInvoke()`
- 使用 `InvokeRequired` 檢查是否需要跨線程調用

### 3. 性能考慮
- SKBitmap 到 Bitmap 的轉換有性能開銷
- 考慮使用緩存或異步加載
- 大量圖片時考慮虛擬化顯示

### 4. 控件布局
- Windows Forms 的布局不如 WPF 靈活
- 考慮使用 `TableLayoutPanel` 或 `FlowLayoutPanel`
- 可能需要手動計算控件位置

---

## 📊 對比總結

| 特性 | WPF | Windows Forms | 遷移難度 |
|------|-----|---------------|---------|
| **線程更新** | `Dispatcher.Invoke()` | `Control.Invoke()` | ⭐ 簡單 |
| **圖像顯示** | `SKElement` 直接渲染 | `PictureBox` + 轉換 | ⭐⭐ 中等 |
| **布局** | XAML 聲明式 | 代碼/設計器 | ⭐⭐ 中等 |
| **數據綁定** | 內置支持 | 手動更新 | ⭐⭐ 中等 |
| **樣式** | 豐富的樣式系統 | 基本樣式 | ⭐⭐ 中等 |
| **整體難度** | - | - | ⭐⭐ **中等** |

---

## ✅ 結論

**Windows Forms 遷移完全可行**，且相對簡單：

1. ✅ **線程模型相似**: `Dispatcher` → `Control.Invoke()`
2. ✅ **異步處理相同**: `async/await` 可以直接使用
3. ✅ **FileSystemWatcher 相同**: 無需修改
4. ⚠️ **圖像顯示需要轉換**: SKBitmap → Bitmap
5. ⚠️ **布局需要重新設計**: 但邏輯相同

**建議**：
- 遷移難度：⭐⭐ (中等)
- 預計工作量：2-3 天
- 主要工作：UI 重新設計和圖像轉換實現

---

---

## 📝 完整代碼結構總結

### 主要文件清單

1. **MainForm.cs** - 主窗體邏輯（約 1500+ 行）
   - 初始化方法
   - 線程安全更新方法
   - 文件監控方法
   - 圖像處理方法
   - 事件處理方法

2. **MainForm.Designer.cs** - 窗體設計器（約 500+ 行）
   - 控件聲明
   - InitializeComponent() 方法
   - 控件創建方法

3. **MainForm.resx** - 資源文件
   - 窗體資源定義

4. **IndustrySegSys.WinForms.csproj** - 項目文件
   - 項目配置
   - NuGet 包引用
   - 項目引用

### 關鍵方法清單

| 方法名 | 功能 | 行數（估算） |
|--------|------|------------|
| `InitializeComponent()` | 初始化控件 | 200+ |
| `CreateConfigPanel()` | 創建配置面板 | 150+ |
| `CreateControlButtons()` | 創建控制按鈕 | 50+ |
| `CreateMainContent()` | 創建主內容區域 | 200+ |
| `CreateStatusBar()` | 創建狀態欄 | 30+ |
| `InitializeDefaultPaths()` | 初始化默認路徑 | 150+ |
| `SavePathsToConfig()` | 保存配置 | 30+ |
| `InvokeUI()` | 線程安全更新 | 10+ |
| `AddLog()` | 添加日誌 | 10+ |
| `UpdateStatistics()` | 更新統計 | 20+ |
| `SKBitmapToBitmap()` | 圖像轉換 | 10+ |
| `ShowImageAtIndex()` | 顯示圖片 | 20+ |
| `UpdateImageNavigation()` | 更新導航 | 15+ |
| `StartMonitorButton_Click()` | 開始監控 | 80+ |
| `StopMonitorButton_Click()` | 停止監控 | 40+ |
| `FileSystemWatcher_Created()` | 目錄創建事件 | 30+ |
| `CreateMaterialWatcher()` | 創建料號監控器 | 50+ |
| `MaterialWatcher_StationCreated()` | 工站創建事件 | 30+ |
| `ProcessMaterialDirectory()` | 處理料號目錄 | 200+ |
| `ProcessExistingDirectories()` | 處理現有目錄 | 30+ |
| `ProcessSingleFile()` | 處理單文件 | 100+ |
| `ProcessBatchFiles()` | 批量處理 | 150+ |
| `UpdateProcessButtonStates()` | 更新按鈕狀態 | 20+ |
| `ClearResultBitmaps()` | 清理圖片 | 30+ |

### 代碼行數估算

- **總代碼行數**: 約 2000+ 行
- **MainForm.cs**: 約 1500 行
- **MainForm.Designer.cs**: 約 500 行

---

## 🎯 快速開始指南

### 1. 創建項目

```bash
# 在解決方案中創建新項目
dotnet new winforms -n IndustrySegSys.WinForms -f net8.0-windows
cd IndustrySegSys.WinForms
```

### 2. 添加依賴

```bash
dotnet add package SkiaSharp --version 3.119.1
dotnet add reference ../YoloDotNet/YoloDotNet.csproj
dotnet add reference ../YoloDotNet.ExecutionProvider.Cpu/YoloDotNet.ExecutionProvider.Cpu.csproj
```

### 3. 修改項目文件

編輯 `IndustrySegSys.WinForms.csproj`，添加：
```xml
<PropertyGroup>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

### 4. 實現代碼

按照本方案中的代碼示例，逐步實現：
1. 先實現 `MainForm.Designer.cs` 中的控件創建方法
2. 再實現 `MainForm.cs` 中的業務邏輯
3. 測試每個功能模塊

### 5. 測試

1. 測試配置加載和保存
2. 測試文件監控功能
3. 測試圖像處理功能
4. 測試手動處理模式
5. 測試自動監控模式

---

## 🔍 常見問題解答

### Q1: SKBitmap 轉換性能問題
**A**: 如果遇到性能問題，可以使用 `SKBitmapToBitmapFast()` 方法，它直接複製像素數據，速度更快。

### Q2: 內存泄漏問題
**A**: 確保在窗體關閉時調用 `ClearResultBitmaps()`，並在處理完圖片後及時釋放 SKBitmap。

### Q3: 線程安全問題
**A**: 所有 UI 更新都必須通過 `InvokeUI()` 方法，確保線程安全。

### Q4: 控件布局問題
**A**: 使用 `TableLayoutPanel` 和 `FlowLayoutPanel` 可以簡化布局，必要時可以設置 `Dock` 屬性。

### Q5: 圖片顯示問題
**A**: 確保 `PictureBox.SizeMode` 設置為 `Zoom`，這樣可以保持寬高比並適應容器大小。

---

## 📚 參考資源

- [Windows Forms 文檔](https://learn.microsoft.com/dotnet/desktop/winforms/)
- [SkiaSharp 文檔](https://learn.microsoft.com/dotnet/api/skiasharp)
- [YoloDotNet 文檔](./README.md)

---

**最後更新**: 2025-01-XX
