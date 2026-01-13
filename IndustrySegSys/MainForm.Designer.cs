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
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 900);
            this.Text = "工業檢測系統 - 自動監控模式";
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            this.MinimumSize = new System.Drawing.Size(1200, 700);

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
            // 統一的尺寸常量
            const int LABEL_WIDTH = 110;           // 標籤寬度
            const int BUTTON_WIDTH = 80;           // 按鈕寬度
            const int TEXTBOX_MIN_WIDTH = 200;     // TextBox 最小寬度
            const int TRACKBAR_WIDTH = 150;        // TrackBar 寬度
            const int VALUE_LABEL_WIDTH = 50;      // 數值標籤寬度
            const int ROW_MARGIN_TOP = 8;          // 行間距
            const int PARAM_SPACING = 25;          // 參數間距

            configGroupBox = new GroupBox
            {
                Text = "配置",
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var configTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true
            };
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            configTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            configTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            configTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            configTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 創建統一的標籤樣式
            Label CreateLabel(string text) => new Label 
            { 
                Text = text, 
                Width = LABEL_WIDTH, 
                AutoSize = false, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 2, 0, 0)  // 微調垂直對齊
            };

            // 創建統一的 TextBox 樣式
            TextBox CreateTextBox() => new TextBox 
            { 
                ReadOnly = true, 
                MinimumSize = new Size(TEXTBOX_MIN_WIDTH, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            // 創建統一的按鈕樣式
            Button CreateBrowseButton() => new Button 
            { 
                Text = "瀏覽...", 
                Width = BUTTON_WIDTH,
                Height = 23,  // 統一按鈕高度
                Anchor = AnchorStyles.Left
            };

            // 創建統一的 FlowLayoutPanel 樣式
            FlowLayoutPanel CreateFlowPanel() => new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.LeftToRight, 
                Margin = new Padding(0, ROW_MARGIN_TOP, 0, 0),
                WrapContents = false,
                AutoSize = true
            };

            // 第一行：模型文件和監控目錄
            modelPathTextBox = CreateTextBox();
            browseModelButton = CreateBrowseButton();
            browseModelButton.Click += BrowseModelButton_Click;

            var modelPanel = CreateFlowPanel();
            modelPanel.Controls.Add(CreateLabel("模型文件:"));
            modelPanel.Controls.Add(modelPathTextBox);
            modelPanel.Controls.Add(browseModelButton);
            configTable.Controls.Add(modelPanel, 0, 0);

            watchPathTextBox = CreateTextBox();
            browseWatchPathButton = CreateBrowseButton();
            browseWatchPathButton.Click += BrowseWatchPathButton_Click;

            var watchPanel = CreateFlowPanel();
            watchPanel.Controls.Add(CreateLabel("監控目錄:"));
            watchPanel.Controls.Add(watchPathTextBox);
            watchPanel.Controls.Add(browseWatchPathButton);
            configTable.Controls.Add(watchPanel, 1, 0);

            // 第二行：輸出目錄和工作模式
            outputPathTextBox = CreateTextBox();
            browseOutputButton = CreateBrowseButton();
            browseOutputButton.Click += BrowseOutputButton_Click;

            var outputPanel = CreateFlowPanel();
            outputPanel.Controls.Add(CreateLabel("輸出目錄:"));
            outputPanel.Controls.Add(outputPathTextBox);
            outputPanel.Controls.Add(browseOutputButton);
            configTable.Controls.Add(outputPanel, 0, 1);

            monitorModeRadio = new RadioButton { Text = "自動監控模式", Checked = true, AutoSize = true };
            manualModeRadio = new RadioButton { Text = "手動處理模式", AutoSize = true };

            var modePanel = CreateFlowPanel();
            modePanel.Controls.Add(CreateLabel("工作模式:"));
            modePanel.Controls.Add(monitorModeRadio);
            modePanel.Controls.Add(new Label { Width = 15 });  // 間距
            modePanel.Controls.Add(manualModeRadio);
            configTable.Controls.Add(modePanel, 1, 1);

            // 第三行：手動模式圖片選擇（初始隱藏）
            // 手動模式的面板使用 AutoSize + Dock=Top，避免在 GroupBox 固定高度下被裁切
            manualImagePanel = new Panel { Dock = DockStyle.Top, Visible = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, 90) };
            
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
            singleFileTextBox = CreateTextBox();
            browseSingleFileButton = CreateBrowseButton();
            browseSingleFileButton.Click += BrowseSingleFileButton_Click;
            
            var singleFilePanel = CreateFlowPanel();
            singleFilePanel.Margin = new Padding(0, ROW_MARGIN_TOP, 0, 0);
            singleFilePanel.Controls.Add(CreateLabel("單文件路徑:"));
            singleFilePanel.Controls.Add(singleFileTextBox);
            singleFilePanel.Controls.Add(browseSingleFileButton);
            manualImageTable.Controls.Add(singleFilePanel, 0, 0);
            
            // 批量處理路徑
            batchFileTextBox = CreateTextBox();
            browseBatchFileButton = CreateBrowseButton();
            browseBatchFileButton.Click += BrowseBatchFileButton_Click;
            
            var batchFilePanel = CreateFlowPanel();
            batchFilePanel.Margin = new Padding(0, ROW_MARGIN_TOP, 0, 0);
            batchFilePanel.Controls.Add(CreateLabel("批量處理目錄:"));
            batchFilePanel.Controls.Add(batchFileTextBox);
            batchFilePanel.Controls.Add(browseBatchFileButton);
            manualImageTable.Controls.Add(batchFilePanel, 0, 1);
            
            manualImagePanel.Controls.Add(manualImageTable);
            configTable.Controls.Add(manualImagePanel, 0, 2);
            configTable.SetColumnSpan(manualImagePanel, 2);

            // 第四行：參數設置
            var paramPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.LeftToRight, 
                Margin = new Padding(0, ROW_MARGIN_TOP + 5, 0, 0),
                WrapContents = false,
                AutoSize = true
            };

            confidenceTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 24, 
                Width = TRACKBAR_WIDTH, 
                TickFrequency = 10,
                AutoSize = false
            };
            confidenceValueLabel = new Label 
            { 
                Text = "0.24", 
                Width = VALUE_LABEL_WIDTH, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 2, 0, 0)
            };

            pixelConfidenceTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 50, 
                Width = TRACKBAR_WIDTH, 
                TickFrequency = 10,
                AutoSize = false
            };
            pixelConfidenceValueLabel = new Label 
            { 
                Text = "0.50", 
                Width = VALUE_LABEL_WIDTH, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 2, 0, 0)
            };

            iouTrackBar = new TrackBar 
            { 
                Minimum = 10, 
                Maximum = 100, 
                Value = 70, 
                Width = TRACKBAR_WIDTH, 
                TickFrequency = 10,
                AutoSize = false
            };
            iouValueLabel = new Label 
            { 
                Text = "0.70", 
                Width = VALUE_LABEL_WIDTH, 
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 2, 0, 0)
            };

            // 參數標籤統一寬度
            paramPanel.Controls.Add(CreateLabel("Confidence:"));
            paramPanel.Controls.Add(confidenceTrackBar);
            paramPanel.Controls.Add(confidenceValueLabel);
            
            paramPanel.Controls.Add(new Label { Width = PARAM_SPACING });  // 間距
            
            paramPanel.Controls.Add(CreateLabel("Pixel Confidence:"));
            paramPanel.Controls.Add(pixelConfidenceTrackBar);
            paramPanel.Controls.Add(pixelConfidenceValueLabel);
            
            paramPanel.Controls.Add(new Label { Width = PARAM_SPACING });  // 間距
            
            paramPanel.Controls.Add(CreateLabel("IoU:"));
            paramPanel.Controls.Add(iouTrackBar);
            paramPanel.Controls.Add(iouValueLabel);

            configTable.Controls.Add(paramPanel, 0, 3);
            configTable.SetColumnSpan(paramPanel, 2);

            configGroupBox.Controls.Add(configTable);
            this.Controls.Add(configGroupBox);
        }

        private void CreateControlButtons()
        {
            buttonPanel = new FlowLayoutPanel
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
            // StartButton 已移除，現在使用獨立的處理按鈕

            stopButton = new Button { Text = "停止檢測", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            stopButton.Click += StopButton_Click;

            processSingleFileButton = new Button { Text = "處理單文件", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            processSingleFileButton.Click += ProcessSingleFileButton_Click;

            processBatchButton = new Button { Text = "批量處理", Width = 120, Height = 35, Visible = false, Enabled = false, Font = new Font("Microsoft Sans Serif", 9F) };
            processBatchButton.Click += ProcessBatchButton_Click;

            openOutputFolderButton = new Button { Text = "打開輸出文件夾", Width = 150, Height = 35, Font = new Font("Microsoft Sans Serif", 9F) };
            openOutputFolderButton.Click += OpenOutputFolderButton_Click;

            // JSON 產生選項
            generateJsonRadio = new RadioButton { Text = "產生 JSON", Checked = true, AutoSize = true, Font = new Font("Microsoft Sans Serif", 9F) };
            noJsonRadio = new RadioButton { Text = "不產生 JSON", AutoSize = true, Font = new Font("Microsoft Sans Serif", 9F) };

            buttonPanel.Controls.Add(startMonitorButton);
            buttonPanel.Controls.Add(stopMonitorButton);
            buttonPanel.Controls.Add(startButton);
            buttonPanel.Controls.Add(stopButton);
            buttonPanel.Controls.Add(processSingleFileButton);
            buttonPanel.Controls.Add(processBatchButton);
            buttonPanel.Controls.Add(openOutputFolderButton);
            buttonPanel.Controls.Add(new Label { Width = 20 });  // 間距
            buttonPanel.Controls.Add(new Label { Text = "JSON 選項:", AutoSize = true, Font = new Font("Microsoft Sans Serif", 9F), Padding = new Padding(0, 8, 0, 0) });
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

            // 上方：圖片預覽區域
            imagePreviewGroupBox = new GroupBox
            {
                Text = "檢測結果預覽",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
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
            mainSplitContainer.Panel1.Controls.Add(imagePreviewGroupBox);

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
                Text = "統計信息",
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                Margin = new Padding(0, 0, 0, 10)
            };

            var statsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 2 };
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            totalCountLabel = new Label { Text = "總處理數: 0", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
            statsPanel.Controls.Add(totalCountLabel, 0, 0);
            statsPanel.SetColumnSpan(totalCountLabel, 2);

            // NG/OK 顯示框
            var ngPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(255, 230, 230), Padding = new Padding(10), Margin = new Padding(5) };
            var ngLabel = new Label { Text = "NG", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Red, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            ngCountLabel = new Label { Text = "0", Font = new Font("Microsoft Sans Serif", 36F, FontStyle.Bold), ForeColor = Color.Red, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            var ngDescLabel = new Label { Text = "(檢測到目標)", Font = new Font("Microsoft Sans Serif", 9F), ForeColor = Color.Red, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter };
            ngPanel.Controls.Add(ngLabel);
            ngPanel.Controls.Add(ngCountLabel);
            ngPanel.Controls.Add(ngDescLabel);
            statsPanel.Controls.Add(ngPanel, 0, 1);

            var okPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(230, 255, 230), Padding = new Padding(10), Margin = new Padding(5) };
            var okLabel = new Label { Text = "OK", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Green, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            okCountLabel = new Label { Text = "0", Font = new Font("Microsoft Sans Serif", 36F, FontStyle.Bold), ForeColor = Color.Green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            var okDescLabel = new Label { Text = "(未檢測到目標)", Font = new Font("Microsoft Sans Serif", 9F), ForeColor = Color.Green, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter };
            okPanel.Controls.Add(okLabel);
            okPanel.Controls.Add(okCountLabel);
            okPanel.Controls.Add(okDescLabel);
            statsPanel.Controls.Add(okPanel, 1, 1);

            var yieldPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(230, 243, 255), Padding = new Padding(10), Margin = new Padding(5) };
            var yieldLabel = new Label { Text = "良率", Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold), ForeColor = Color.Blue, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            yieldRateLabel = new Label { Text = "0.00%", Font = new Font("Microsoft Sans Serif", 48F, FontStyle.Bold), ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            yieldPanel.Controls.Add(yieldLabel);
            yieldPanel.Controls.Add(yieldRateLabel);
            statsPanel.Controls.Add(yieldPanel, 0, 2);
            statsPanel.SetColumnSpan(yieldPanel, 2);

            currentMaterialLabel = new Label { Text = "當前料號: 無", Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 5) };
            currentFileLabel = new Label { Text = "當前文件: 無", Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 0) };
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
                Text = "處理進度",
                Dock = DockStyle.Fill,
                Visible = false,
                Padding = new Padding(5),
                Margin = new Padding(0, 0, 0, 10)
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

            // 終端顯示區域（左側）
            rightSplitContainer.Panel1.Controls.Add(infoPanel);

            // JSON 資訊顯示（右側）
            jsonInfoGroupBox = new GroupBox
            {
                Text = "JSON 資訊",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            jsonInfoTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(250, 250, 250),
                ForeColor = Color.FromArgb(50, 50, 50),
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
            statusStrip = new StatusStrip();

            statusLabel = new ToolStripStatusLabel("就緒");
            monitorStatusLabel = new ToolStripStatusLabel("監控狀態: 未啟動")
            {
                ForeColor = Color.Gray
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
