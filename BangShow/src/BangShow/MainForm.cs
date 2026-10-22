using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using BangShow.Core;
using BangShow.Core.Api;
using BangShow.Core.Protocol;
using BangShow.Core.Serial;

namespace BangShow;

public partial class MainForm : Form
{
    private const int MaxLogLines = 400;
    private const int MaxDisplayFramesPerSecond = 10;

    /// <summary>服务端 weight 为 decimal(12,0)，超出该范围的值不上报。</summary>
    private const decimal MaxReportWeight = 999_999_999_999m;
    private const decimal MinReportWeight = -999_999_999_999m;

    /// <summary>
    /// 设备数值以 1/100 为单位（即“分”）发送，地磅数值大屏显示时除以该值并舍去小数。
    /// 上报到服务的数值仍按原始值（不除以该值）。
    /// </summary>
    private const long DisplayWeightDivisor = 100;

    /// <summary>地磅数值大屏固定显示的单位（不跟随设备帧里的单位字节）。</summary>
    private const string DisplayWeightUnit = "kg";

    private readonly SerialPortService _serialPort = new();
    private readonly ConcurrentQueue<string> _pendingErrors = new();
    private readonly ConcurrentQueue<WeightReportedEventArgs> _pendingReports = new();
    private readonly FrameRateLimiter _frameRateLimiter = new(MaxDisplayFramesPerSecond);
    private WeightReporter? _weightReporter;
    private WeightDisplayForm? _weightDisplay;
    private int _logLineCount;
    private long _displayedFrames;
    private long _filteredFrames;
    private long _reportedFrames;
    private long _reportFailureCount;
    private bool _reportFailureActive;

    public MainForm()
    {
        InitializeComponent();
        InitializeSerialOptions();
        RefreshPortList();
        UpdateConnectionState();
        InitializeWeightReporting();

        _serialPort.Faulted += OnSerialPortFaulted;
        timerPoll.Start();
        AppendLog("程序已启动：请选择 COM 口与通信参数，然后点击“打开串口”。");
        AppendLog("协议：0x02 + 符号(1) + 数值(8) + 单位/状态(1) + 0x03，共 12 字节。");
        AppendLog("数值格式默认 ASCII 文本；若设备发送 BCD 编码，请先切换到“BCD 编码”再打开串口。");
        AppendLog($"界面最多每秒刷新 {MaxDisplayFramesPerSecond} 帧（约 100ms 一次），超出部分会被过滤并计入统计。");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timerPoll.Stop();
        _serialPort.Faulted -= OnSerialPortFaulted;
        _serialPort.Dispose();

        if (_weightDisplay is { IsDisposed: false } display)
        {
            display.Close();
            display.Dispose();
        }

        _weightDisplay = null;

        if (_weightReporter is not null)
        {
            _weightReporter.Reported -= OnWeightReported;
            _weightReporter.Dispose();
        }

        base.OnFormClosed(e);
    }

    private void InitializeSerialOptions()
    {
        cboBaudRate.Items.AddRange(new object[]
        {
            1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600,
        });
        cboBaudRate.Text = "9600";

        foreach (int bits in new[] { 5, 6, 7, 8 })
        {
            cboDataBits.Items.Add(bits);
        }

        cboDataBits.SelectedItem = 8;

        cboParity.Items.AddRange(new object[]
        {
            new ComboItem<Parity>("无 (None)", Parity.None),
            new ComboItem<Parity>("奇校验 (Odd)", Parity.Odd),
            new ComboItem<Parity>("偶校验 (Even)", Parity.Even),
            new ComboItem<Parity>("标记 (Mark)", Parity.Mark),
            new ComboItem<Parity>("空格 (Space)", Parity.Space),
        });
        cboParity.SelectedIndex = 0;

        cboStopBits.Items.AddRange(new object[]
        {
            new ComboItem<StopBits>("1", StopBits.One),
            new ComboItem<StopBits>("1.5", StopBits.OnePointFive),
            new ComboItem<StopBits>("2", StopBits.Two),
        });
        cboStopBits.SelectedIndex = 0;

        cboNumericFormat.Items.AddRange(new object[]
        {
            new ComboItem<NumericFormat>("ASCII 文本", NumericFormat.Ascii),
            new ComboItem<NumericFormat>("BCD 编码", NumericFormat.Bcd),
        });
        cboNumericFormat.SelectedIndex = 0;
    }

    private void RefreshPortList()
    {
        if (_serialPort.IsOpen)
        {
            return;
        }

        string? previous = cboPort.SelectedItem as string;
        string[] ports;
        try
        {
            ports = SerialPort.GetPortNames()
                .Select(name => name.TrimEnd('\0'))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PortNumber)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            ports = [];
            AppendLog($"读取串口列表失败：{ex.Message}");
        }

        cboPort.BeginUpdate();
        try
        {
            cboPort.Items.Clear();
            foreach (string port in ports)
            {
                cboPort.Items.Add(port);
            }

            if (ports.Length == 0)
            {
                cboPort.SelectedIndex = -1;
                cboPort.Text = string.Empty;
            }
            else if (previous is not null && cboPort.Items.Contains(previous))
            {
                cboPort.SelectedItem = previous;
            }
            else
            {
                cboPort.SelectedIndex = 0;
            }
        }
        finally
        {
            cboPort.EndUpdate();
        }
    }

    private static int PortNumber(string name) =>
        name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(name.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : int.MaxValue;

    private void btnRefreshPorts_Click(object? sender, EventArgs e)
    {
        RefreshPortList();
        AppendLog($"已刷新串口列表，共 {cboPort.Items.Count} 个可用串口。");
    }

    private void btnOpen_Click(object? sender, EventArgs e)
    {
        if (_serialPort.IsOpen)
        {
            return;
        }

        if (cboPort.SelectedItem is not string portName || string.IsNullOrWhiteSpace(portName))
        {
            ShowWarning("没有可用的串口。请连接设备后点击“刷新端口”。");
            return;
        }

        if (!int.TryParse(cboBaudRate.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int baudRate)
            || baudRate <= 0)
        {
            ShowWarning("请输入有效的波特率（正整数）。");
            cboBaudRate.Focus();
            return;
        }

        if (cboDataBits.SelectedItem is not int dataBits
            || cboParity.SelectedItem is not ComboItem<Parity> parity
            || cboStopBits.SelectedItem is not ComboItem<StopBits> stopBits
            || cboNumericFormat.SelectedItem is not ComboItem<NumericFormat> numericFormat)
        {
            ShowWarning("请完整选择数据位、校验位、停止位和数值格式。");
            return;
        }

        var settings = new SerialPortSettings
        {
            PortName = portName,
            BaudRate = baudRate,
            DataBits = dataBits,
            Parity = parity.Value,
            StopBits = stopBits.Value,
            NumericFormat = numericFormat.Value,
        };

        try
        {
            _serialPort.Open(settings);
            ResetFrameDisplay();
            ResetFrameRateCounters();
            _weightDisplay?.ResetWeight();
            AppendLog($"串口已打开：{settings.PortName}，{settings.BaudRate} bps，{settings.DataBits} 数据位，{settings.Parity}，停止位 {settings.StopBits}，数值格式 {settings.NumericFormat}");
            UpdateConnectionState();
        }
        catch (Exception ex)
        {
            AppendLog($"【错误】打开串口失败：{ex.Message}");
            ShowError($"打开串口失败：{ex.Message}");
            UpdateConnectionState();
        }
    }

    private void btnClose_Click(object? sender, EventArgs e)
    {
        try
        {
            _serialPort.Close();
            AppendLog("串口已关闭。");
        }
        catch (Exception ex)
        {
            AppendLog($"【错误】关闭串口失败：{ex.Message}");
        }

        UpdateConnectionState();
    }

    private void btnClearLog_Click(object? sender, EventArgs e)
    {
        txtLog.Clear();
        _logLineCount = 0;
    }

    private void timerPoll_Tick(object? sender, EventArgs e)
    {
        // 后台线程产生的错误日志（避免跨线程直接操作控件）
        while (_pendingErrors.TryDequeue(out string? error))
        {
            AppendLog($"【错误】{error}");
        }

        // 上报结果（成功 / 失败）同样由后台线程产生，这里统一写入日志并更新统计
        while (_pendingReports.TryDequeue(out WeightReportedEventArgs? report))
        {
            string line = DescribeReport(report);
            if (line.Length > 0)
            {
                AppendLog(line);
            }
        }

        // 每次轮询都把队列清空，只保留最新一帧用于显示，避免高频数据积压和界面刷新跟不上
        SensorFrame? latest = null;
        int received = 0;
        while (_serialPort.TryDequeueFrame(out SensorFrame? frame))
        {
            latest = frame;
            received++;
        }

        if (latest is not null)
        {
            // 限流：每秒最多处理 MaxDisplayFramesPerSecond 帧，未到间隔的整批帧直接过滤
            if (_frameRateLimiter.TryAcquire(Stopwatch.GetTimestamp()))
            {
                ShowFrame(latest);
                _displayedFrames++;
                _filteredFrames += received - 1;

                string suffix = received > 1 ? $"（本周期收到 {received} 帧，过滤 {received - 1} 帧）" : string.Empty;
                AppendLog($"RX {latest.ValueWithUnit} ｜ {latest.RawHex}{suffix}");
            }
            else
            {
                _filteredFrames += received;
            }
        }

        // 由定时器驱动上报：有新数值且距上次发送 >= 500ms 时才真正发请求（异步，不阻塞界面）
        _weightReporter?.Pump();

        UpdateStatistics();
    }

    private void ShowFrame(SensorFrame frame)
    {
        lblValue.Text = frame.HasValue ? frame.ValueWithUnit : frame.NumberText;
        lblValue.ForeColor = frame.HasValue ? Color.FromArgb(21, 101, 192) : Color.Firebrick;

        lblUnit.Text = "单位：" + (frame.Unit ?? "—");

        string signText = frame.SignByte is >= 0x20 and < 0x7F
            ? ((char)frame.SignByte).ToString()
            : "—";
        lblStatus.Text = $"符号：{signText} (0x{frame.SignByte:X2}) ｜ 单位/状态：0x{frame.UnitOrStatusByte:X2}";

        lblRawFrame.Text = "原始帧：" + frame.RawHex;

        ReportWeight(frame);
        UpdateWeightDisplay(frame);
    }

    /// <summary>
    /// 数值发生变化时交给上报器发送到 Python 服务。
    /// 服务端 weight 为 decimal(12,0)，这里四舍五入取整；超出范围的值不上报。
    /// </summary>
    private void ReportWeight(SensorFrame frame)
    {
        if (_weightReporter is null || !TryGetRoundedWeight(frame, out long weight))
        {
            return;
        }

        _weightReporter.Report(weight);
    }

    /// <summary>
    /// 取四舍五入后的整数数值（服务端与地磅大屏都按整数显示）；
    /// 无法解析或超出 decimal(12,0) 范围时返回 <c>false</c>。
    /// </summary>
    private static bool TryGetRoundedWeight(SensorFrame frame, out long weight)
    {
        weight = 0;
        if (frame.Value is not { } value)
        {
            return false;
        }

        decimal rounded = decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        if (rounded is > MaxReportWeight or < MinReportWeight)
        {
            return false;
        }

        weight = (long)rounded;
        return true;
    }

    /// <summary>打开（或切回）地磅数值大屏：只显示数值 + 数值下方的绿色指示球。</summary>
    private void btnWeightDisplay_Click(object? sender, EventArgs e)
    {
        if (_weightDisplay is { IsDisposed: false } existing)
        {
            if (!existing.Visible)
            {
                existing.Show();
            }

            existing.Activate();
            AppendLog("地磅数值显示窗口已切到前台（按 Esc 或双击窗口退出）。");
            return;
        }

        var display = new WeightDisplayForm(Screen.FromControl(this));
        display.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_weightDisplay, display))
            {
                _weightDisplay = null;
            }
        };

        _weightDisplay = display;
        display.Show(this);
        AppendLog("已打开地磅数值大屏：只显示数值（原始值 ÷ 100，不保留小数）；数值变化时绿色指示球 0.1 秒闪一次、共 3 次，数值稳定后常亮（Esc 或双击退出，F11 切换屏幕）。");
    }

    /// <summary>
    /// 把最新数值转发给地磅数值大屏（窗口未打开时直接忽略）。
    /// <para>大屏只显示“原始值 ÷ 100”后的整数（不保留小数）；显示结果不变时不会重复触发闪烁。</para>
    /// </summary>
    private void UpdateWeightDisplay(SensorFrame frame)
    {
        if (_weightDisplay is not { IsDisposed: false } display || !TryGetRoundedWeight(frame, out long rawWeight))
        {
            return;
        }

        // 设备按 1/100 单位发送（例如 2836000 表示 28360.00），大屏直接舍去小数部分；
        // 单位固定显示 kg（不跟随帧里的单位字节）
        display.UpdateWeight(rawWeight / DisplayWeightDivisor, DisplayWeightUnit);
    }

    /// <summary>读取 appsettings.json 并创建上报器；配置缺失/非法时禁用上报，不影响串口功能。</summary>
    private void InitializeWeightReporting()
    {
        string path = AppSettings.DefaultPath;

        try
        {
            WeightServiceOptions options = AppSettings.Load(path).Service;
            options.Validate();

            var apiClient = new WeightApiClient(
                options.BaseUrl,
                TimeSpan.FromSeconds(options.TimeoutSeconds));

            _weightReporter = new WeightReporter(apiClient, options.BangName, options.MaxReportsPerSecond);
            _weightReporter.Reported += OnWeightReported;

            AppendLog(
                $"数值上报已启用：{options.BaseUrl}，bangname = {options.BangName}，"
                + $"数值变化时最多 {options.MaxReportsPerSecond} 次/秒。");
        }
        catch (Exception ex)
        {
            AppendLog($"【警告】服务配置读取失败（{path}）：{ex.Message}；数值上报已禁用，串口功能不受影响。");
        }
    }

    /// <summary>上报完成后在后台线程触发：只入队，界面线程负责写日志（避免跨线程访问控件）。</summary>
    private void OnWeightReported(object? sender, WeightReportedEventArgs e) => _pendingReports.Enqueue(e);

    /// <summary>把上报结果转成日志文本（在界面线程执行，可安全更新计数器）；返回空串表示不写日志。</summary>
    private string DescribeReport(WeightReportedEventArgs report)
    {
        if (!report.Succeeded)
        {
            _reportFailureCount++;
            if (_reportFailureActive)
            {
                return string.Empty; // 已提示过，避免服务长时间不可用时刷屏；恢复时一并报告失败次数
            }

            _reportFailureActive = true;
            return $"【警告】上报 weight={report.Weight:N0} 失败：{report.Error}"
                + "（服务未启动？相同错误不再重复提示，恢复时会给出累计次数）";
        }

        _reportedFrames++;
        string note = report.ServerUpdated ? "服务端已更新" : "服务端数值未变，未修改";
        string recovery = _reportFailureActive ? $"（此前失败 {_reportFailureCount} 次，已恢复）" : string.Empty;

        _reportFailureActive = false;
        _reportFailureCount = 0;

        return $"↑ 已上报 weight={report.Weight:N0}，{note}{recovery}";
    }

    private void ResetFrameDisplay()
    {
        lblValue.Text = "—";
        lblValue.ForeColor = Color.FromArgb(21, 101, 192);
        lblUnit.Text = "单位：—";
        lblStatus.Text = "符号：— ｜ 单位/状态：—";
        lblRawFrame.Text = "原始帧：—";
    }

    private void ResetFrameRateCounters()
    {
        _frameRateLimiter.Reset();
        _displayedFrames = 0;
        _filteredFrames = 0;
    }

    private void UpdateStatistics()
    {
        lblStatistics.Text =
            $"已接收 {_serialPort.TotalBytesReceived:N0} 字节 ｜ 有效帧 {_serialPort.TotalFrames:N0}"
            + $" ｜ 显示 {_displayedFrames:N0} ｜ 过滤 {_filteredFrames:N0}"
            + $" ｜ 上报 {_reportedFrames:N0}"
            + $" ｜ 丢弃 {_serialPort.DiscardedBytes:N0} 字节 ｜ 缓存 {_serialPort.BufferedBytes:N0}"
            + $" ｜ 溢出 {_serialPort.OverwrittenBytes:N0}";
    }

    private void UpdateConnectionState()
    {
        bool isOpen = _serialPort.IsOpen;

        cboPort.Enabled = !isOpen;
        cboBaudRate.Enabled = !isOpen;
        cboDataBits.Enabled = !isOpen;
        cboParity.Enabled = !isOpen;
        cboStopBits.Enabled = !isOpen;
        cboNumericFormat.Enabled = !isOpen;
        btnRefreshPorts.Enabled = !isOpen;
        btnOpen.Enabled = !isOpen;
        btnClose.Enabled = isOpen;

        if (isOpen && _serialPort.Settings is { } settings)
        {
            lblConnection.Text = $"已连接 {settings.PortName} @ {settings.BaudRate} bps";
            lblConnection.ForeColor = Color.SeaGreen;
        }
        else
        {
            lblConnection.Text = "未连接";
            lblConnection.ForeColor = Color.DimGray;
        }
    }

    private void OnSerialPortFaulted(object? sender, SerialPortFaultEventArgs e) =>
        _pendingErrors.Enqueue(e.Message);

    private void AppendLog(string message)
    {
        string line = message.ReplaceLineEndings(" ");

        if (txtLog.TextLength > 0)
        {
            txtLog.AppendText(Environment.NewLine);
        }

        txtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {line}");
        _logLineCount++;

        // 超过上限时删除最早的行，防止长时间运行时日志占用过多内存
        if (_logLineCount > MaxLogLines)
        {
            int removeEnd = txtLog.GetFirstCharIndexFromLine(_logLineCount - MaxLogLines);
            if (removeEnd > 0)
            {
                txtLog.Select(0, removeEnd);
                txtLog.SelectedText = string.Empty;
            }

            _logLineCount = MaxLogLines;
        }

        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>下拉框中带显示名称的枚举选项。</summary>
    private sealed record ComboItem<T>(string Text, T Value)
    {
        public override string ToString() => Text;
    }
}
