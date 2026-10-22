#nullable enable

namespace BangShow;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

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
        components = new System.ComponentModel.Container();
        groupBoxSettings = new GroupBox();
        labelPort = new Label();
        cboPort = new ComboBox();
        btnRefreshPorts = new Button();
        labelBaudRate = new Label();
        cboBaudRate = new ComboBox();
        labelDataBits = new Label();
        cboDataBits = new ComboBox();
        labelParity = new Label();
        cboParity = new ComboBox();
        labelStopBits = new Label();
        cboStopBits = new ComboBox();
        btnOpen = new Button();
        btnClose = new Button();
        lblConnection = new Label();
        labelNumericFormat = new Label();
        cboNumericFormat = new ComboBox();
        groupBoxDisplay = new GroupBox();
        labelValueCaption = new Label();
        lblValue = new Label();
        lblUnit = new Label();
        lblStatus = new Label();
        lblStatistics = new Label();
        lblRawFrame = new Label();
        btnWeightDisplay = new Button();
        groupBoxLog = new GroupBox();
        txtLog = new RichTextBox();
        btnClearLog = new Button();
        timerPoll = new System.Windows.Forms.Timer(components);
        groupBoxSettings.SuspendLayout();
        groupBoxDisplay.SuspendLayout();
        groupBoxLog.SuspendLayout();
        SuspendLayout();

        // groupBoxSettings
        groupBoxSettings.Controls.Add(labelPort);
        groupBoxSettings.Controls.Add(cboPort);
        groupBoxSettings.Controls.Add(btnRefreshPorts);
        groupBoxSettings.Controls.Add(labelBaudRate);
        groupBoxSettings.Controls.Add(cboBaudRate);
        groupBoxSettings.Controls.Add(labelDataBits);
        groupBoxSettings.Controls.Add(cboDataBits);
        groupBoxSettings.Controls.Add(labelParity);
        groupBoxSettings.Controls.Add(cboParity);
        groupBoxSettings.Controls.Add(labelStopBits);
        groupBoxSettings.Controls.Add(cboStopBits);
        groupBoxSettings.Controls.Add(btnOpen);
        groupBoxSettings.Controls.Add(btnClose);
        groupBoxSettings.Controls.Add(lblConnection);
        groupBoxSettings.Controls.Add(labelNumericFormat);
        groupBoxSettings.Controls.Add(cboNumericFormat);
        groupBoxSettings.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupBoxSettings.Location = new Point(12, 12);
        groupBoxSettings.Name = "groupBoxSettings";
        groupBoxSettings.Size = new Size(916, 118);
        groupBoxSettings.TabIndex = 0;
        groupBoxSettings.TabStop = false;
        groupBoxSettings.Text = "串口设置";

        // labelPort
        labelPort.AutoSize = true;
        labelPort.Location = new Point(18, 36);
        labelPort.Name = "labelPort";
        labelPort.Size = new Size(56, 17);
        labelPort.TabIndex = 0;
        labelPort.Text = "COM 口：";

        // cboPort
        cboPort.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPort.FormattingEnabled = true;
        cboPort.Location = new Point(85, 32);
        cboPort.Name = "cboPort";
        cboPort.Size = new Size(170, 25);
        cboPort.TabIndex = 1;

        // btnRefreshPorts
        btnRefreshPorts.Location = new Point(264, 31);
        btnRefreshPorts.Name = "btnRefreshPorts";
        btnRefreshPorts.Size = new Size(80, 27);
        btnRefreshPorts.TabIndex = 2;
        btnRefreshPorts.Text = "刷新端口";
        btnRefreshPorts.UseVisualStyleBackColor = true;
        btnRefreshPorts.Click += btnRefreshPorts_Click;

        // labelBaudRate
        labelBaudRate.AutoSize = true;
        labelBaudRate.Location = new Point(368, 36);
        labelBaudRate.Name = "labelBaudRate";
        labelBaudRate.Size = new Size(56, 17);
        labelBaudRate.TabIndex = 3;
        labelBaudRate.Text = "波特率：";

        // cboBaudRate
        cboBaudRate.FormattingEnabled = true;
        cboBaudRate.Location = new Point(435, 32);
        cboBaudRate.Name = "cboBaudRate";
        cboBaudRate.Size = new Size(110, 25);
        cboBaudRate.TabIndex = 4;

        // labelDataBits
        labelDataBits.AutoSize = true;
        labelDataBits.Location = new Point(572, 36);
        labelDataBits.Name = "labelDataBits";
        labelDataBits.Size = new Size(56, 17);
        labelDataBits.TabIndex = 5;
        labelDataBits.Text = "数据位：";

        // cboDataBits
        cboDataBits.DropDownStyle = ComboBoxStyle.DropDownList;
        cboDataBits.FormattingEnabled = true;
        cboDataBits.Location = new Point(639, 32);
        cboDataBits.Name = "cboDataBits";
        cboDataBits.Size = new Size(80, 25);
        cboDataBits.TabIndex = 6;

        // btnOpen
        btnOpen.Location = new Point(749, 30);
        btnOpen.Name = "btnOpen";
        btnOpen.Size = new Size(76, 30);
        btnOpen.TabIndex = 7;
        btnOpen.Text = "打开串口";
        btnOpen.UseVisualStyleBackColor = true;
        btnOpen.Click += btnOpen_Click;

        // btnClose
        btnClose.Enabled = false;
        btnClose.Location = new Point(831, 30);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(76, 30);
        btnClose.TabIndex = 8;
        btnClose.Text = "关闭串口";
        btnClose.UseVisualStyleBackColor = true;
        btnClose.Click += btnClose_Click;

        // labelParity
        labelParity.AutoSize = true;
        labelParity.Location = new Point(18, 82);
        labelParity.Name = "labelParity";
        labelParity.Size = new Size(56, 17);
        labelParity.TabIndex = 9;
        labelParity.Text = "校验位：";

        // cboParity
        cboParity.DropDownStyle = ComboBoxStyle.DropDownList;
        cboParity.FormattingEnabled = true;
        cboParity.Location = new Point(85, 78);
        cboParity.Name = "cboParity";
        cboParity.Size = new Size(170, 25);
        cboParity.TabIndex = 10;

        // labelStopBits
        labelStopBits.AutoSize = true;
        labelStopBits.Location = new Point(368, 82);
        labelStopBits.Name = "labelStopBits";
        labelStopBits.Size = new Size(56, 17);
        labelStopBits.TabIndex = 11;
        labelStopBits.Text = "停止位：";

        // cboStopBits
        cboStopBits.DropDownStyle = ComboBoxStyle.DropDownList;
        cboStopBits.FormattingEnabled = true;
        cboStopBits.Location = new Point(435, 78);
        cboStopBits.Name = "cboStopBits";
        cboStopBits.Size = new Size(110, 25);
        cboStopBits.TabIndex = 12;

        // lblConnection
        lblConnection.AutoSize = true;
        lblConnection.ForeColor = Color.DimGray;
        lblConnection.Location = new Point(572, 82);
        lblConnection.Name = "lblConnection";
        lblConnection.Size = new Size(44, 17);
        lblConnection.TabIndex = 13;
        lblConnection.Text = "未连接";

        // labelNumericFormat
        labelNumericFormat.AutoSize = true;
        labelNumericFormat.Location = new Point(690, 82);
        labelNumericFormat.Name = "labelNumericFormat";
        labelNumericFormat.Size = new Size(70, 17);
        labelNumericFormat.TabIndex = 14;
        labelNumericFormat.Text = "数值格式：";

        // cboNumericFormat
        cboNumericFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cboNumericFormat.FormattingEnabled = true;
        cboNumericFormat.Location = new Point(760, 78);
        cboNumericFormat.Name = "cboNumericFormat";
        cboNumericFormat.Size = new Size(150, 25);
        cboNumericFormat.TabIndex = 15;

        groupBoxSettings.ResumeLayout(false);
        groupBoxSettings.PerformLayout();

        // groupBoxDisplay
        groupBoxDisplay.Controls.Add(labelValueCaption);
        groupBoxDisplay.Controls.Add(lblValue);
        groupBoxDisplay.Controls.Add(lblUnit);
        groupBoxDisplay.Controls.Add(lblStatus);
        groupBoxDisplay.Controls.Add(lblStatistics);
        groupBoxDisplay.Controls.Add(lblRawFrame);
        groupBoxDisplay.Controls.Add(btnWeightDisplay);
        groupBoxDisplay.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupBoxDisplay.Location = new Point(12, 144);
        groupBoxDisplay.Name = "groupBoxDisplay";
        groupBoxDisplay.Size = new Size(916, 150);
        groupBoxDisplay.TabIndex = 1;
        groupBoxDisplay.TabStop = false;
        groupBoxDisplay.Text = "实时数据";

        // labelValueCaption
        labelValueCaption.AutoSize = true;
        labelValueCaption.Location = new Point(20, 36);
        labelValueCaption.Name = "labelValueCaption";
        labelValueCaption.Size = new Size(44, 17);
        labelValueCaption.TabIndex = 0;
        labelValueCaption.Text = "数值：";

        // lblValue
        lblValue.AutoEllipsis = true;
        lblValue.Font = new Font("Consolas", 26F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lblValue.ForeColor = Color.FromArgb(21, 101, 192);
        lblValue.Location = new Point(80, 14);
        lblValue.Name = "lblValue";
        lblValue.Size = new Size(330, 56);
        lblValue.TabIndex = 1;
        lblValue.Text = "—";
        lblValue.TextAlign = ContentAlignment.MiddleLeft;

        // lblUnit
        lblUnit.AutoSize = true;
        lblUnit.Location = new Point(430, 30);
        lblUnit.Name = "lblUnit";
        lblUnit.Size = new Size(70, 17);
        lblUnit.TabIndex = 2;
        lblUnit.Text = "单位：—";

        // lblStatus
        lblStatus.AutoEllipsis = true;
        lblStatus.Location = new Point(430, 60);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(460, 17);
        lblStatus.TabIndex = 3;
        lblStatus.Text = "符号：— ｜ 单位/状态：—";

        // lblStatistics
        lblStatistics.AutoEllipsis = true;
        lblStatistics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblStatistics.Location = new Point(20, 96);
        lblStatistics.Name = "lblStatistics";
        lblStatistics.Size = new Size(876, 17);
        lblStatistics.TabIndex = 4;
        lblStatistics.Text = "已接收：0 字节 ｜ 有效帧：0 ｜ 丢弃：0 字节 ｜ 缓存：0 字节";

        // lblRawFrame
        lblRawFrame.AutoEllipsis = true;
        lblRawFrame.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblRawFrame.Location = new Point(20, 122);
        lblRawFrame.Name = "lblRawFrame";
        lblRawFrame.Size = new Size(876, 17);
        lblRawFrame.TabIndex = 5;
        lblRawFrame.Text = "原始帧：—";

        // btnWeightDisplay
        btnWeightDisplay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnWeightDisplay.BackColor = Color.FromArgb(21, 101, 192);
        btnWeightDisplay.Cursor = Cursors.Hand;
        btnWeightDisplay.FlatAppearance.BorderSize = 0;
        btnWeightDisplay.FlatAppearance.MouseDownBackColor = Color.FromArgb(13, 71, 161);
        btnWeightDisplay.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 136, 229);
        btnWeightDisplay.FlatStyle = FlatStyle.Flat;
        btnWeightDisplay.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 134);
        btnWeightDisplay.ForeColor = Color.White;
        btnWeightDisplay.Location = new Point(716, 14);
        btnWeightDisplay.Name = "btnWeightDisplay";
        btnWeightDisplay.Size = new Size(180, 42);
        btnWeightDisplay.TabIndex = 6;
        btnWeightDisplay.Text = "显示数值";
        btnWeightDisplay.UseVisualStyleBackColor = false;
        btnWeightDisplay.Click += btnWeightDisplay_Click;

        groupBoxDisplay.ResumeLayout(false);
        groupBoxDisplay.PerformLayout();

        // groupBoxLog
        groupBoxLog.Controls.Add(txtLog);
        groupBoxLog.Controls.Add(btnClearLog);
        groupBoxLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        groupBoxLog.Location = new Point(12, 306);
        groupBoxLog.Name = "groupBoxLog";
        groupBoxLog.Size = new Size(916, 296);
        groupBoxLog.TabIndex = 2;
        groupBoxLog.TabStop = false;
        groupBoxLog.Text = "通信日志";

        // txtLog
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.BackColor = Color.White;
        txtLog.DetectUrls = false;
        txtLog.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        txtLog.Location = new Point(14, 24);
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        txtLog.Size = new Size(888, 226);
        txtLog.TabIndex = 0;
        txtLog.WordWrap = false;

        // btnClearLog
        btnClearLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClearLog.Location = new Point(812, 256);
        btnClearLog.Name = "btnClearLog";
        btnClearLog.Size = new Size(90, 28);
        btnClearLog.TabIndex = 1;
        btnClearLog.Text = "清空日志";
        btnClearLog.UseVisualStyleBackColor = true;
        btnClearLog.Click += btnClearLog_Click;

        groupBoxLog.ResumeLayout(false);

        // timerPoll
        timerPoll.Interval = 50;
        timerPoll.Tick += timerPoll_Tick;

        // MainForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(940, 618);
        Controls.Add(groupBoxSettings);
        Controls.Add(groupBoxDisplay);
        Controls.Add(groupBoxLog);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
        MinimumSize = new Size(956, 657);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "BangShow 串口数据接收";
        ResumeLayout(false);
    }

    #endregion

    private GroupBox groupBoxSettings = null!;
    private Label labelPort = null!;
    private ComboBox cboPort = null!;
    private Button btnRefreshPorts = null!;
    private Label labelBaudRate = null!;
    private ComboBox cboBaudRate = null!;
    private Label labelDataBits = null!;
    private ComboBox cboDataBits = null!;
    private Label labelParity = null!;
    private ComboBox cboParity = null!;
    private Label labelStopBits = null!;
    private ComboBox cboStopBits = null!;
    private Button btnOpen = null!;
    private Button btnClose = null!;
    private Label lblConnection = null!;
    private Label labelNumericFormat = null!;
    private ComboBox cboNumericFormat = null!;
    private GroupBox groupBoxDisplay = null!;
    private Label labelValueCaption = null!;
    private Label lblValue = null!;
    private Label lblUnit = null!;
    private Label lblStatus = null!;
    private Label lblStatistics = null!;
    private Label lblRawFrame = null!;
    private Button btnWeightDisplay = null!;
    private GroupBox groupBoxLog = null!;
    private RichTextBox txtLog = null!;
    private Button btnClearLog = null!;
    private System.Windows.Forms.Timer timerPoll = null!;
}
