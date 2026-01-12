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

            var modelPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5) };
            modelPanel.Controls.Add(new Label { Text = "模型文件:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
            modelPanel.Controls.Add(modelPathTextBox);
            modelPanel.Controls.Add(browseModelButton);
            configTable.Controls.Add(modelPanel, 0, 0);

            watchPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseWatchPathButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseWatchPathButton.Click += BrowseWatchPathButton_Click;

            var watchPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5) };
            watchPanel.Controls.Add(new Label { Text = "監控目錄:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
            watchPanel.Controls.Add(watchPathTextBox);
            watchPanel.Controls.Add(browseWatchPathButton);
            configTable.Controls.Add(watchPanel, 1, 0);

            // 第二行：輸出目錄和工作模式
            outputPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            browseOutputButton = new Button { Text = "瀏覽...", Width = 80, Anchor = AnchorStyles.Left };
            browseOutputButton.Click += BrowseOutputButton_Click;

            var outputPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5) };
            outputPanel.Controls.Add(new Label { Text = "輸出目錄:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
            outputPanel.Controls.Add(outputPathTextBox);
            outputPanel.Controls.Add(browseOutputButton);
            configTable.Controls.Add(outputPanel, 0, 1);

            monitorModeRadio = new RadioButton { Text = "自動監控模式", Checked = true };
            manualModeRadio = new RadioButton { Text = "手動處理模式" };

            var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5) };
            modePanel.Controls.Add(new Label { Text = "工作模式:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
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

            var imagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5) };
            imagePanel.Controls.Add(new Label { Text = "圖片路徑:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
            imagePanel.Controls.Add(imagePathTextBox);
            imagePanel.Controls.Add(browseImageButton);
            imagePanel.Controls.Add(new Label { Text = "處理模式:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(20, 0, 0, 0) });
            imagePanel.Controls.Add(singleFileRadio);
            imagePanel.Controls.Add(batchFileRadio);
            manualImagePanel.Controls.Add(imagePanel);
            configTable.Controls.Add(manualImagePanel, 0, 2);
            configTable.SetColumnSpan(manualImagePanel, 2);

            // 第四行：參數設置
            var paramPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0) };

            confidenceTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 24, Width = 150, TickFrequency = 10 };
            confidenceValueLabel = new Label { Text = "0.24", Width = 50, TextAlign = ContentAlignment.MiddleLeft };

            pixelConfidenceTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 50, Width = 150, TickFrequency = 10 };
            pixelConfidenceValueLabel = new Label { Text = "0.50", Width = 50, TextAlign = ContentAlignment.MiddleLeft };

            iouTrackBar = new TrackBar { Minimum = 10, Maximum = 100, Value = 70, Width = 150, TickFrequency = 10 };
            iouValueLabel = new Label { Text = "0.70", Width = 50, TextAlign = ContentAlignment.MiddleLeft };

            paramPanel.Controls.Add(new Label { Text = "Confidence:", Width = 100, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
            paramPanel.Controls.Add(confidenceTrackBar);
            paramPanel.Controls.Add(confidenceValueLabel);
            paramPanel.Controls.Add(new Label { Text = "Pixel Confidence:", Width = 120, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(20, 0, 0, 0) });
            paramPanel.Controls.Add(pixelConfidenceTrackBar);
            paramPanel.Controls.Add(pixelConfidenceValueLabel);
            paramPanel.Controls.Add(new Label { Text = "IoU:", Width = 50, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(20, 0, 0, 0) });
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
                Padding = new Padding(5),
                Margin = new Padding(0, 0, 0, 10)
            };

            var statsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 2 };
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Auto));
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
            statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
            statusStrip.Items.Add(monitorStatusLabel);

            this.Controls.Add(statusStrip);
        }

        private void SetupLayout()
        {
            // 設置控件層級順序（從上到下）
            configGroupBox.BringToFront();
        }

        #endregion
    }
}
