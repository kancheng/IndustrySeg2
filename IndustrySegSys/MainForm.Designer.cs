using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace IndustrySegSys
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
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
        private RadioButton cameraModeRadio;
        private Panel manualImagePanel;
        private TextBox singleFileTextBox;
        private Button browseSingleFileButton;
        private TextBox batchFileTextBox;
        private Button browseBatchFileButton;
        private TrackBar confidenceTrackBar;
        private Label confidenceValueLabel;
        private TrackBar pixelConfidenceTrackBar;
        private Label pixelConfidenceValueLabel;
        private TrackBar iouTrackBar;
        private Label iouValueLabel;
        private RadioButton generateJsonRadio;
        private RadioButton noJsonRadio;

        // 控制按鈕
        private Button startMonitorButton;
        private Button stopMonitorButton;
        private Button startButton;
        private Button stopButton;
        private Button processSingleFileButton;
        private Button processBatchButton;
        private Button openOutputFolderButton;
        private FlowLayoutPanel buttonPanel;
        
        // 相機模式控件
        private Panel cameraModePanel;
        private ComboBox cmbCameras;
        private Button btnConnectCamera;
        private Button btnCaptureCamera;
        private Button btnBurstCapture;
        private NumericUpDown numBurstCount;
        private NumericUpDown numCaptureDelay;
        private PictureBox cameraPreviewBox;
        private Label lblCameraStatus;

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
        private GroupBox jsonInfoGroupBox;
        private TextBox jsonInfoTextBox;

        // 狀態欄
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel monitorStatusLabel;
        
        // SplitContainer 控件
        private SplitContainer mainSplitContainer;
        private SplitContainer rightSplitContainer;
        private SplitContainer cameraPreviewSplitContainer; // 相機模式：左右分割（預覽 + 檢視）
        private GroupBox cameraPreviewGroupBox; // 相機預覽區域
        private Panel cameraPreviewContainerPanel; // 相機預覽容器
        private Label cameraPreviewNoImageLabel; // 相機預覽無畫面標籤

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        // 現代化設計常量
        private static class ModernUI
        {
            // 顏色主題
            public static readonly Color BackgroundPrimary = Color.FromArgb(245, 247, 250);
            public static readonly Color BackgroundSecondary = Color.White;
            public static readonly Color BackgroundCard = Color.White;
            public static readonly Color BorderColor = Color.FromArgb(230, 234, 240);
            public static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
            public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
            public static readonly Color AccentPrimary = Color.FromArgb(59, 130, 246); // Blue
            public static readonly Color AccentSuccess = Color.FromArgb(34, 197, 94); // Green
            public static readonly Color AccentDanger = Color.FromArgb(239, 68, 68); // Red
            public static readonly Color AccentWarning = Color.FromArgb(251, 191, 36); // Yellow
            
            // 按鈕樣式
            public static readonly Color ButtonPrimary = AccentPrimary;
            public static readonly Color ButtonPrimaryHover = Color.FromArgb(37, 99, 235);
            public static readonly Color ButtonSuccess = AccentSuccess;
            public static readonly Color ButtonDanger = AccentDanger;
            public static readonly Color ButtonSecondary = Color.FromArgb(241, 245, 249);
            public static readonly Color ButtonSecondaryHover = Color.FromArgb(226, 232, 240);
            
            // 間距
            public const int PaddingSmall = 8;
            public const int PaddingMedium = 12;
            public const int PaddingLarge = 16;
            public const int PaddingXLarge = 24;
            public const int BorderRadius = 8;
            public const int CardElevation = 2;
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1600, 1000);
            this.Text = "工業檢測系統";
            this.BackColor = ModernUI.BackgroundPrimary;
            this.MinimumSize = new System.Drawing.Size(1280, 720);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 創建控件
            CreateConfigPanel();
            CreateControlButtons();
            CreateMainContent();
            CreateStatusBar();

            // 設置布局
            SetupLayout();
            
            // 添加響應式布局處理
            this.Resize += MainForm_Resize;
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 響應式布局調整
            AdjustResponsiveLayout();
        }
        
        private void AdjustResponsiveLayout()
        {
            // 根據窗口大小調整布局
            int width = this.ClientSize.Width;
            
            // 小屏幕：單列布局
            if (width < 1400)
            {
                if (configGroupBox != null)
                {
                    // 調整配置面板為單列
                    var table = configGroupBox.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
                    if (table != null && table.ColumnCount > 1)
                    {
                        // 可以動態調整列寬
                    }
                }
            }
        }

        // 創建現代化按鈕樣式
        private Button CreateModernButton(string text, Color? backColor = null, Color? foreColor = null, int width = 120, int height = 36)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor ?? ModernUI.ButtonPrimary,
                ForeColor = foreColor ?? Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = backColor == ModernUI.ButtonPrimary ? ModernUI.ButtonPrimaryHover : ModernUI.ButtonSecondaryHover;
            return btn;
        }
        
        // 創建現代化文本框
        private TextBox CreateModernTextBox()
        {
            return new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ModernUI.BackgroundSecondary,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                MinimumSize = new Size(200, 28),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(8, 4, 8, 4)
            };
        }
        
        // 創建現代化標籤
        private Label CreateModernLabel(string text, int? width = null)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = width == null,
                Width = width ?? 0,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 4, 0, 0)
            };
            return label;
        }

        private void CreateConfigPanel()
        {
            // 創建卡片式配置面板
            configGroupBox = new GroupBox
            {
                Text = "⚙️ 系統配置",
                Dock = DockStyle.Top,
                Padding = new Padding(ModernUI.PaddingLarge),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(ModernUI.PaddingMedium)
            };

            var configTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                AutoSize = true,
                Padding = new Padding(0)
            };
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int i = 0; i < 5; i++)
            {
                configTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            // 創建統一的 FlowLayoutPanel 樣式
            FlowLayoutPanel CreateFlowPanel(int marginTop = ModernUI.PaddingSmall) => new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.LeftToRight, 
                Margin = new Padding(ModernUI.PaddingMedium, marginTop, ModernUI.PaddingMedium, 0),
                WrapContents = false,
                AutoSize = true
            };

            // 第一行：模型文件和監控目錄
            modelPathTextBox = CreateModernTextBox();
            browseModelButton = CreateModernButton("瀏覽", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 80, 28);
            browseModelButton.Click += BrowseModelButton_Click;

            var modelPanel = CreateFlowPanel();
            modelPanel.Controls.Add(CreateModernLabel("模型文件:", 100));
            modelPanel.Controls.Add(modelPathTextBox);
            modelPanel.Controls.Add(browseModelButton);
            configTable.Controls.Add(modelPanel, 0, 0);

            watchPathTextBox = CreateModernTextBox();
            browseWatchPathButton = CreateModernButton("瀏覽", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 80, 28);
            browseWatchPathButton.Click += BrowseWatchPathButton_Click;

            var watchPanel = CreateFlowPanel();
            watchPanel.Controls.Add(CreateModernLabel("監控目錄:", 100));
            watchPanel.Controls.Add(watchPathTextBox);
            watchPanel.Controls.Add(browseWatchPathButton);
            configTable.Controls.Add(watchPanel, 1, 0);

            // 第二行：輸出目錄和工作模式
            outputPathTextBox = CreateModernTextBox();
            browseOutputButton = CreateModernButton("瀏覽", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 80, 28);
            browseOutputButton.Click += BrowseOutputButton_Click;

            var outputPanel = CreateFlowPanel();
            outputPanel.Controls.Add(CreateModernLabel("輸出目錄:", 100));
            outputPanel.Controls.Add(outputPathTextBox);
            outputPanel.Controls.Add(browseOutputButton);
            configTable.Controls.Add(outputPanel, 0, 1);

            // 工作模式選擇 - 使用現代化樣式
            monitorModeRadio = new RadioButton 
            { 
                Text = "📁 自動監控", 
                Checked = true, 
                AutoSize = true,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(ModernUI.PaddingSmall, 0, ModernUI.PaddingMedium, 0)
            };
            manualModeRadio = new RadioButton 
            { 
                Text = "🖱️ 手動處理", 
                AutoSize = true,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(ModernUI.PaddingSmall, 0, ModernUI.PaddingMedium, 0)
            };
            cameraModeRadio = new RadioButton 
            { 
                Text = "📷 相機模式", 
                AutoSize = true,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(ModernUI.PaddingSmall, 0, 0, 0)
            };

            var modePanel = CreateFlowPanel();
            modePanel.Controls.Add(CreateModernLabel("工作模式:", 100));
            modePanel.Controls.Add(monitorModeRadio);
            modePanel.Controls.Add(manualModeRadio);
            modePanel.Controls.Add(cameraModeRadio);
            configTable.Controls.Add(modePanel, 1, 1);

            // 第三行：手動模式圖片選擇（初始隱藏）
            manualImagePanel = new Panel 
            { 
                Dock = DockStyle.Top, 
                Visible = false, 
                AutoSize = true, 
                AutoSizeMode = AutoSizeMode.GrowAndShrink, 
                MinimumSize = new Size(0, 100),
                Padding = new Padding(ModernUI.PaddingMedium)
            };
            
            var manualImageTable = new TableLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                RowCount = 2, 
                ColumnCount = 1,
                AutoSize = true
            };
            manualImageTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            manualImageTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            
            // 單文件路徑
            singleFileTextBox = CreateModernTextBox();
            browseSingleFileButton = CreateModernButton("瀏覽", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 80, 28);
            browseSingleFileButton.Click += BrowseSingleFileButton_Click;
            
            var singleFilePanel = CreateFlowPanel(ModernUI.PaddingSmall);
            singleFilePanel.Controls.Add(CreateModernLabel("單文件路徑:", 100));
            singleFilePanel.Controls.Add(singleFileTextBox);
            singleFilePanel.Controls.Add(browseSingleFileButton);
            manualImageTable.Controls.Add(singleFilePanel, 0, 0);
            
            // 批量處理路徑
            batchFileTextBox = CreateModernTextBox();
            browseBatchFileButton = CreateModernButton("瀏覽", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 80, 28);
            browseBatchFileButton.Click += BrowseBatchFileButton_Click;
            
            var batchFilePanel = CreateFlowPanel(ModernUI.PaddingSmall);
            batchFilePanel.Controls.Add(CreateModernLabel("批量處理目錄:", 100));
            batchFilePanel.Controls.Add(batchFileTextBox);
            batchFilePanel.Controls.Add(browseBatchFileButton);
            manualImageTable.Controls.Add(batchFilePanel, 0, 1);
            
            manualImagePanel.Controls.Add(manualImageTable);
            configTable.Controls.Add(manualImagePanel, 0, 2);
            configTable.SetColumnSpan(manualImagePanel, 2);

            // 第四行（相機模式面板）：相機選擇和控制（初始隱藏）
            cameraModePanel = new Panel 
            { 
                Dock = DockStyle.Top, 
                Visible = false, 
                AutoSize = true, 
                AutoSizeMode = AutoSizeMode.GrowAndShrink, 
                MinimumSize = new Size(0, 140),
                Padding = new Padding(ModernUI.PaddingMedium)
            };
            
            var cameraModeTable = new TableLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                RowCount = 4, 
                ColumnCount = 2,
                AutoSize = true
            };
            for (int i = 0; i < 4; i++)
            {
                cameraModeTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            cameraModeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            cameraModeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            // 相機選擇
            cmbCameras = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                MinimumSize = new Size(200, 28),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = ModernUI.BackgroundSecondary,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            
            var cameraSelectPanel = CreateFlowPanel(ModernUI.PaddingSmall);
            cameraSelectPanel.Controls.Add(CreateModernLabel("選擇相機:", 100));
            cameraSelectPanel.Controls.Add(cmbCameras);
            btnConnectCamera = CreateModernButton("連接相機", ModernUI.ButtonPrimary, Color.White, 100, 28);
            btnConnectCamera.Click += BtnConnectCamera_Click;
            cameraSelectPanel.Controls.Add(btnConnectCamera);
            cameraModeTable.Controls.Add(cameraSelectPanel, 0, 0);
            cameraModeTable.SetColumnSpan(cameraSelectPanel, 2);
            
            // 拍照延遲和連拍數量
            numCaptureDelay = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 60,
                Value = 0,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Width = 100,
                Height = 28,
                Anchor = AnchorStyles.Left,
                BackColor = ModernUI.BackgroundSecondary,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var delayPanel = CreateFlowPanel(ModernUI.PaddingSmall);
            delayPanel.Controls.Add(CreateModernLabel("拍照延遲(秒):", 120));
            delayPanel.Controls.Add(numCaptureDelay);
            cameraModeTable.Controls.Add(delayPanel, 0, 1);
            
            numBurstCount = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 30,
                Value = 1,
                DecimalPlaces = 0,
                Increment = 1,
                Width = 100,
                Height = 28,
                Anchor = AnchorStyles.Left,
                BackColor = ModernUI.BackgroundSecondary,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var burstPanel = CreateFlowPanel(ModernUI.PaddingSmall);
            burstPanel.Controls.Add(CreateModernLabel("連拍數量:", 100));
            burstPanel.Controls.Add(numBurstCount);
            cameraModeTable.Controls.Add(burstPanel, 1, 1);
            
            // 相機控制按鈕
            btnCaptureCamera = CreateModernButton("📷 拍照檢測", ModernUI.ButtonPrimary, Color.White, 150, 36);
            btnCaptureCamera.Enabled = false;
            btnCaptureCamera.Click += BtnCaptureCamera_Click;
            btnBurstCapture = CreateModernButton("⚡ 連拍檢測", ModernUI.ButtonSuccess, Color.White, 150, 36);
            btnBurstCapture.Enabled = false;
            btnBurstCapture.Click += BtnBurstCapture_Click;
            
            var cameraButtonPanel = CreateFlowPanel(ModernUI.PaddingSmall);
            cameraButtonPanel.Controls.Add(btnCaptureCamera);
            cameraButtonPanel.Controls.Add(new Label { Width = ModernUI.PaddingSmall }); // 間距
            cameraButtonPanel.Controls.Add(btnBurstCapture);
            cameraModeTable.Controls.Add(cameraButtonPanel, 0, 2);
            cameraModeTable.SetColumnSpan(cameraButtonPanel, 2);
            
            // 相機狀態標籤
            lblCameraStatus = new Label
            {
                Text = "相機狀態: 未連接",
                Dock = DockStyle.Top,
                Margin = new Padding(ModernUI.PaddingMedium, ModernUI.PaddingSmall, ModernUI.PaddingMedium, 0),
                ForeColor = ModernUI.TextSecondary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            cameraModeTable.Controls.Add(lblCameraStatus, 0, 3);
            cameraModeTable.SetColumnSpan(lblCameraStatus, 2);
            
            cameraModePanel.Controls.Add(cameraModeTable);
            configTable.Controls.Add(cameraModePanel, 0, 3);
            configTable.SetColumnSpan(cameraModePanel, 2);

            // 第五行：參數設置
            var paramPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.LeftToRight, 
                Margin = new Padding(ModernUI.PaddingMedium, ModernUI.PaddingMedium, ModernUI.PaddingMedium, 0),
                WrapContents = false,
                AutoSize = true
            };

            confidenceTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 24, 
                Width = 180, 
                Height = 45,
                TickFrequency = 10,
                AutoSize = false,
                BackColor = ModernUI.BackgroundCard
            };
            confidenceValueLabel = new Label 
            { 
                Text = "0.24", 
                Width = 50, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ModernUI.PaddingSmall, 4, 0, 0),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            pixelConfidenceTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 50, 
                Width = 180, 
                Height = 45,
                TickFrequency = 10,
                AutoSize = false,
                BackColor = ModernUI.BackgroundCard
            };
            pixelConfidenceValueLabel = new Label 
            { 
                Text = "0.50", 
                Width = 50, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ModernUI.PaddingSmall, 4, 0, 0),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            iouTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 70, 
                Width = 180, 
                Height = 45,
                TickFrequency = 10,
                AutoSize = false,
                BackColor = ModernUI.BackgroundCard
            };
            iouValueLabel = new Label 
            { 
                Text = "0.70", 
                Width = 50, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ModernUI.PaddingSmall, 4, 0, 0),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            // 參數標籤
            paramPanel.Controls.Add(CreateModernLabel("Confidence:", 100));
            paramPanel.Controls.Add(confidenceTrackBar);
            paramPanel.Controls.Add(confidenceValueLabel);
            
            paramPanel.Controls.Add(new Label { Width = ModernUI.PaddingLarge });  // 間距
            
            paramPanel.Controls.Add(CreateModernLabel("Pixel Confidence:", 120));
            paramPanel.Controls.Add(pixelConfidenceTrackBar);
            paramPanel.Controls.Add(pixelConfidenceValueLabel);
            
            paramPanel.Controls.Add(new Label { Width = ModernUI.PaddingLarge });  // 間距
            
            paramPanel.Controls.Add(CreateModernLabel("IoU:", 60));
            paramPanel.Controls.Add(iouTrackBar);
            paramPanel.Controls.Add(iouValueLabel);

            configTable.Controls.Add(paramPanel, 0, 4);
            configTable.SetColumnSpan(paramPanel, 2);

            configGroupBox.Controls.Add(configTable);
            this.Controls.Add(configGroupBox);
        }

        private void CreateControlButtons()
        {
            buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(ModernUI.PaddingLarge),
                BackColor = ModernUI.BackgroundCard,
                Margin = new Padding(ModernUI.PaddingMedium, 0, ModernUI.PaddingMedium, ModernUI.PaddingSmall)
            };

            startMonitorButton = CreateModernButton("▶ 開始監控", ModernUI.ButtonSuccess, Color.White, 130, 38);
            startMonitorButton.Click += StartMonitorButton_Click;

            stopMonitorButton = CreateModernButton("⏹ 停止監控", ModernUI.ButtonDanger, Color.White, 130, 38);
            stopMonitorButton.Enabled = false;
            stopMonitorButton.Click += StopMonitorButton_Click;

            startButton = new Button { Text = "開始檢測", Width = 120, Height = 35, Visible = false };
            // StartButton 已移除，現在使用獨立的處理按鈕

            stopButton = CreateModernButton("⏹ 停止檢測", ModernUI.ButtonDanger, Color.White, 130, 38);
            stopButton.Visible = false;
            stopButton.Enabled = false;
            stopButton.Click += StopButton_Click;

            processSingleFileButton = CreateModernButton("📄 處理單文件", ModernUI.ButtonPrimary, Color.White, 140, 38);
            processSingleFileButton.Visible = false;
            processSingleFileButton.Enabled = false;
            processSingleFileButton.Click += ProcessSingleFileButton_Click;

            processBatchButton = CreateModernButton("📁 批量處理", ModernUI.ButtonPrimary, Color.White, 130, 38);
            processBatchButton.Visible = false;
            processBatchButton.Enabled = false;
            processBatchButton.Click += ProcessBatchButton_Click;

            openOutputFolderButton = CreateModernButton("📂 打開輸出文件夾", ModernUI.ButtonSecondary, ModernUI.TextPrimary, 160, 38);
            openOutputFolderButton.Click += OpenOutputFolderButton_Click;

            // JSON 產生選項
            generateJsonRadio = new RadioButton 
            { 
                Text = "產生 JSON", 
                Checked = true, 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F),
                ForeColor = ModernUI.TextPrimary,
                Padding = new Padding(ModernUI.PaddingSmall, 0, ModernUI.PaddingMedium, 0)
            };
            noJsonRadio = new RadioButton 
            { 
                Text = "不產生 JSON", 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F),
                ForeColor = ModernUI.TextPrimary
            };

            buttonPanel.Controls.Add(startMonitorButton);
            buttonPanel.Controls.Add(new Label { Width = ModernUI.PaddingSmall }); // 間距
            buttonPanel.Controls.Add(stopMonitorButton);
            buttonPanel.Controls.Add(new Label { Width = ModernUI.PaddingSmall }); // 間距
            buttonPanel.Controls.Add(startButton);
            buttonPanel.Controls.Add(stopButton);
            buttonPanel.Controls.Add(processSingleFileButton);
            buttonPanel.Controls.Add(processBatchButton);
            buttonPanel.Controls.Add(new Label { Width = ModernUI.PaddingLarge }); // 間距
            buttonPanel.Controls.Add(openOutputFolderButton);
            buttonPanel.Controls.Add(new Label { Width = ModernUI.PaddingLarge }); // 間距
            buttonPanel.Controls.Add(new Label 
            { 
                Text = "JSON 選項:", 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ModernUI.TextPrimary,
                Padding = new Padding(0, 10, ModernUI.PaddingSmall, 0) 
            });
            buttonPanel.Controls.Add(generateJsonRadio);
            buttonPanel.Controls.Add(noJsonRadio);

            this.Controls.Add(buttonPanel);
        }

        private void CreateMainContent()
        {
            // 主 SplitContainer：上方（圖片）和下方（終端+JSON）
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                // 上下分割：上方(圖片) + 下方(終端 + JSON)
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                Panel1MinSize = 0,
                Panel2MinSize = 0, // set later after layout to avoid InvalidOperationException during init
                FixedPanel = FixedPanel.None
            };
            mainSplitContainer.SplitterMoved += (s, e) => SavePathsToConfig();
            
            // 在首次顯示時設置合理的初始值
            bool mainSplitInitialized = false;
            mainSplitContainer.Resize += (s, e) =>
            {
                if (!mainSplitInitialized && mainSplitContainer.Height > 0)
                {
                    var minDistance = mainSplitContainer.Panel1MinSize;
                    var maxDistance = mainSplitContainer.Height - mainSplitContainer.Panel2MinSize;
                    if (maxDistance > minDistance)
                    {
                        var safeDistance = System.Math.Max(minDistance, System.Math.Min(mainSplitContainer.Height / 3, maxDistance));
                        try
                        {
                            mainSplitContainer.SplitterDistance = safeDistance;
                            mainSplitInitialized = true;
                        }
                        catch { }
                    }
                }
            };

            // 上方：圖片預覽區域（包含相機預覽和檢視畫面）
            // 創建相機模式的左右分割容器（初始隱藏，僅在相機模式時顯示）
            cameraPreviewSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                Panel1MinSize = 100,  // 降低最小尺寸，避免初始寬度不足時出錯
                Panel2MinSize = 100,  // 降低最小尺寸，避免初始寬度不足時出錯
                FixedPanel = FixedPanel.None,
                Visible = false
            };
            
            // 在首次顯示時設置合理的初始值，避免 SplitterDistance 錯誤
            bool cameraSplitInitialized = false;
            cameraPreviewSplitContainer.Resize += (s, e) =>
            {
                if (!cameraSplitInitialized && cameraPreviewSplitContainer.Visible && cameraPreviewSplitContainer.Width > 0)
                {
                    var minDistance = cameraPreviewSplitContainer.Panel1MinSize;
                    var maxDistance = cameraPreviewSplitContainer.Width - cameraPreviewSplitContainer.Panel2MinSize;
                    if (maxDistance > minDistance)
                    {
                        var safeDistance = System.Math.Max(minDistance, System.Math.Min(cameraPreviewSplitContainer.Width / 2, maxDistance));
                        try
                        {
                            cameraPreviewSplitContainer.SplitterDistance = safeDistance;
                            cameraSplitInitialized = true;
                        }
                        catch { }
                    }
                }
            };
            
            // 在控件添加到父容器後，設置一個安全的初始 SplitterDistance
            cameraPreviewSplitContainer.HandleCreated += (s, e) =>
            {
                try
                {
                    if (cameraPreviewSplitContainer.Width > 0)
                    {
                        var minDistance = cameraPreviewSplitContainer.Panel1MinSize;
                        var maxDistance = cameraPreviewSplitContainer.Width - cameraPreviewSplitContainer.Panel2MinSize;
                        if (maxDistance > minDistance)
                        {
                            var safeDistance = System.Math.Max(minDistance, System.Math.Min(cameraPreviewSplitContainer.Width / 2, maxDistance));
                            cameraPreviewSplitContainer.SplitterDistance = safeDistance;
                        }
                    }
                }
                catch { }
            };

            // 左側：相機預覽區域
            cameraPreviewGroupBox = new GroupBox
            {
                Text = "📷 相機預覽",
                Dock = DockStyle.Fill,
                Padding = new Padding(ModernUI.PaddingLarge),
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            cameraPreviewContainerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            cameraPreviewBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill
            };

            cameraPreviewNoImageLabel = new Label
            {
                Text = "相機未連接",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft Sans Serif", 18F),
                ForeColor = Color.Gray
            };

            cameraPreviewContainerPanel.Controls.Add(cameraPreviewBox);
            cameraPreviewContainerPanel.Controls.Add(cameraPreviewNoImageLabel);
            cameraPreviewNoImageLabel.BringToFront();

            cameraPreviewGroupBox.Controls.Add(cameraPreviewContainerPanel);
            cameraPreviewSplitContainer.Panel1.Controls.Add(cameraPreviewGroupBox);

            // 右側：檢測結果檢視區域
            imagePreviewGroupBox = new GroupBox
            {
                Text = "🖼️ 檢測結果檢視",
                Dock = DockStyle.Fill,
                Padding = new Padding(ModernUI.PaddingLarge),
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
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
            
            // 初始狀態：非相機模式，直接顯示檢視區域（覆蓋整個 Panel1）
            mainSplitContainer.Panel1.Controls.Add(imagePreviewGroupBox);
            mainSplitContainer.Panel1.Controls.Add(cameraPreviewSplitContainer);
            
            // 注意：相機模式時，imagePreviewGroupBox 的 Parent 會動態切換到 cameraPreviewSplitContainer.Panel2

            // 下方 SplitContainer：終端顯示和 JSON 檢視
            rightSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                // 左右分割：左側(終端/統計/日誌) + 右側(JSON)
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                Panel1MinSize = 0,
                Panel2MinSize = 0, // set later after layout to avoid InvalidOperationException during init
                FixedPanel = FixedPanel.None
            };
            rightSplitContainer.SplitterMoved += (s, e) => SavePathsToConfig();
            
            // 在首次顯示時設置合理的初始值
            bool rightSplitInitialized = false;
            rightSplitContainer.Resize += (s, e) =>
            {
                if (!rightSplitInitialized && rightSplitContainer.Width > 0)
                {
                    var minDistance = rightSplitContainer.Panel1MinSize;
                    var maxDistance = rightSplitContainer.Width - rightSplitContainer.Panel2MinSize;
                    if (maxDistance > minDistance)
                    {
                        var safeDistance = System.Math.Max(minDistance, System.Math.Min(rightSplitContainer.Width / 2, maxDistance));
                        try
                        {
                            rightSplitContainer.SplitterDistance = safeDistance;
                            rightSplitInitialized = true;
                        }
                        catch { }
                    }
                }
            };

            // 左側：終端顯示區域
            var infoPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3
            };
            infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 統計信息
            statisticsGroupBox = new GroupBox
            {
                Text = "📊 統計信息",
                Dock = DockStyle.Fill,
                Padding = new Padding(ModernUI.PaddingMedium),
                Margin = new Padding(0, 0, 0, ModernUI.PaddingSmall),
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            var statsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 2 };
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            totalCountLabel = new Label 
            { 
                Text = "總處理數: 0", 
                Dock = DockStyle.Fill, 
                Margin = new Padding(0, 0, 0, ModernUI.PaddingSmall),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            statsPanel.Controls.Add(totalCountLabel, 0, 0);
            statsPanel.SetColumnSpan(totalCountLabel, 2);

            // NG/OK 顯示框 - 現代化卡片樣式
            var ngPanel = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BorderStyle = BorderStyle.FixedSingle, 
                BackColor = Color.FromArgb(254, 242, 242), 
                Padding = new Padding(ModernUI.PaddingMedium), 
                Margin = new Padding(ModernUI.PaddingSmall)
            };
            var ngLabel = new Label 
            { 
                Text = "❌ NG", 
                Font = new Font("Segoe UI", 18F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentDanger, 
                Dock = DockStyle.Top, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            ngCountLabel = new Label 
            { 
                Text = "0", 
                Font = new Font("Segoe UI", 32F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentDanger, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            var ngDescLabel = new Label 
            { 
                Text = "(檢測到目標)", 
                Font = new Font("Segoe UI", 8F), 
                ForeColor = ModernUI.TextSecondary, 
                Dock = DockStyle.Bottom, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            ngPanel.Controls.Add(ngLabel);
            ngPanel.Controls.Add(ngCountLabel);
            ngPanel.Controls.Add(ngDescLabel);
            statsPanel.Controls.Add(ngPanel, 0, 1);

            var okPanel = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BorderStyle = BorderStyle.FixedSingle, 
                BackColor = Color.FromArgb(240, 253, 244), 
                Padding = new Padding(ModernUI.PaddingMedium), 
                Margin = new Padding(ModernUI.PaddingSmall) 
            };
            var okLabel = new Label 
            { 
                Text = "✅ OK", 
                Font = new Font("Segoe UI", 18F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentSuccess, 
                Dock = DockStyle.Top, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            okCountLabel = new Label 
            { 
                Text = "0", 
                Font = new Font("Segoe UI", 32F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentSuccess, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            var okDescLabel = new Label 
            { 
                Text = "(未檢測到目標)", 
                Font = new Font("Segoe UI", 8F), 
                ForeColor = ModernUI.TextSecondary, 
                Dock = DockStyle.Bottom, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            okPanel.Controls.Add(okLabel);
            okPanel.Controls.Add(okCountLabel);
            okPanel.Controls.Add(okDescLabel);
            statsPanel.Controls.Add(okPanel, 1, 1);

            var yieldPanel = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BorderStyle = BorderStyle.FixedSingle, 
                BackColor = Color.FromArgb(239, 246, 255), 
                Padding = new Padding(ModernUI.PaddingMedium), 
                Margin = new Padding(ModernUI.PaddingSmall) 
            };
            var yieldLabel = new Label 
            { 
                Text = "📈 良率", 
                Font = new Font("Segoe UI", 18F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentPrimary, 
                Dock = DockStyle.Top, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            yieldRateLabel = new Label 
            { 
                Text = "0.00%", 
                Font = new Font("Segoe UI", 40F, FontStyle.Bold), 
                ForeColor = ModernUI.AccentPrimary, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            yieldPanel.Controls.Add(yieldLabel);
            yieldPanel.Controls.Add(yieldRateLabel);
            statsPanel.Controls.Add(yieldPanel, 0, 2);
            statsPanel.SetColumnSpan(yieldPanel, 2);

            currentMaterialLabel = new Label 
            { 
                Text = "當前料號: 無", 
                Dock = DockStyle.Top, 
                Margin = new Padding(0, ModernUI.PaddingSmall, 0, ModernUI.PaddingSmall / 2),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F)
            };
            currentFileLabel = new Label 
            { 
                Text = "當前文件: 無", 
                Dock = DockStyle.Top, 
                Margin = new Padding(0, ModernUI.PaddingSmall / 2, 0, 0),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F)
            };
            var infoLabelsPanel = new Panel { Dock = DockStyle.Fill };
            infoLabelsPanel.Controls.Add(currentMaterialLabel);
            infoLabelsPanel.Controls.Add(currentFileLabel);
            statsPanel.Controls.Add(infoLabelsPanel, 0, 3);
            statsPanel.SetColumnSpan(infoLabelsPanel, 2);

            statisticsGroupBox.Controls.Add(statsPanel);
            infoPanel.Controls.Add(statisticsGroupBox, 0, 0);

            // 進度條（初始隱藏）
            progressGroupBox = new GroupBox
            {
                Text = "⏳ 處理進度",
                Dock = DockStyle.Fill,
                Visible = false,
                Padding = new Padding(ModernUI.PaddingMedium),
                Margin = new Padding(0, 0, 0, ModernUI.PaddingSmall),
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            progressBar = new ProgressBar 
            { 
                Dock = DockStyle.Top, 
                Height = 24,
                Style = ProgressBarStyle.Continuous
            };
            progressTextLabel = new Label 
            { 
                Text = "0 / 0", 
                Dock = DockStyle.Top, 
                TextAlign = ContentAlignment.MiddleCenter, 
                Margin = new Padding(0, ModernUI.PaddingSmall, 0, 0),
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            processingSpeedLabel = new Label 
            { 
                Text = "處理速度: --", 
                Dock = DockStyle.Top, 
                Margin = new Padding(0, ModernUI.PaddingSmall / 2, 0, 0),
                ForeColor = ModernUI.TextSecondary,
                Font = new Font("Segoe UI", 8F)
            };

            var progressPanel = new Panel { Dock = DockStyle.Fill };
            progressPanel.Controls.Add(progressBar);
            progressPanel.Controls.Add(progressTextLabel);
            progressPanel.Controls.Add(processingSpeedLabel);
            progressGroupBox.Controls.Add(progressPanel);
            infoPanel.Controls.Add(progressGroupBox, 0, 1);

            // 日誌
            logGroupBox = new GroupBox
            {
                Text = "📝 日誌",
                Dock = DockStyle.Fill,
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Padding(ModernUI.PaddingMedium)
            };

            logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(212, 212, 212),
                BorderStyle = BorderStyle.FixedSingle
            };

            logGroupBox.Controls.Add(logTextBox);
            infoPanel.Controls.Add(logGroupBox, 0, 2);

            // 終端顯示區域（左側）
            rightSplitContainer.Panel1.Controls.Add(infoPanel);

            // JSON 資訊顯示（右側）
            jsonInfoGroupBox = new GroupBox
            {
                Text = "📄 JSON 資訊",
                Dock = DockStyle.Fill,
                Padding = new Padding(ModernUI.PaddingLarge),
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            jsonInfoTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = ModernUI.BackgroundSecondary,
                ForeColor = ModernUI.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "查無該 JSON 訊息"
            };

            jsonInfoGroupBox.Controls.Add(jsonInfoTextBox);
            rightSplitContainer.Panel2.Controls.Add(jsonInfoGroupBox);

            // 將下方 SplitContainer 添加到主 SplitContainer 的下方
            mainSplitContainer.Panel2.Controls.Add(rightSplitContainer);

            this.Controls.Add(mainSplitContainer);
        }

        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip
            {
                BackColor = ModernUI.BackgroundCard,
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F)
            };

            statusLabel = new ToolStripStatusLabel("✅ 就緒")
            {
                ForeColor = ModernUI.TextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            monitorStatusLabel = new ToolStripStatusLabel("📊 監控狀態: 未啟動")
            {
                ForeColor = ModernUI.TextSecondary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
            statusStrip.Items.Add(monitorStatusLabel);

            this.Controls.Add(statusStrip);
        }

        private void SetupLayout()
        {
            // Dock 佈局在 WinForms 會受 Z-order 影響：Fill 若在最前面可能蓋住 Top/Bottom。
            // 固定順序：mainSplitContainer 在最底（先佔剩餘空間），上方是 configGroupBox，底部是 buttonPanel 和 statusStrip。
            mainSplitContainer?.SendToBack();
            configGroupBox?.BringToFront();
            buttonPanel?.BringToFront();
            statusStrip?.BringToFront();
        }

        #endregion
    }
}
