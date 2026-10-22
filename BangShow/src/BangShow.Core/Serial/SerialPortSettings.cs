using System.IO.Ports;
using BangShow.Core.Protocol;

namespace BangShow.Core.Serial;

/// <summary>串口连接参数。</summary>
public sealed record SerialPortSettings
{
    /// <summary>串口名称，例如 "COM3"。</summary>
    public required string PortName { get; init; }

    public int BaudRate { get; init; } = 9600;

    public int DataBits { get; init; } = 8;

    public Parity Parity { get; init; } = Parity.None;

    public StopBits StopBits { get; init; } = StopBits.One;

    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>数值字段编码方式。</summary>
    public NumericFormat NumericFormat { get; init; } = NumericFormat.Ascii;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PortName))
        {
            throw new ArgumentException("串口名称不能为空。", nameof(PortName));
        }

        if (BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaudRate), "波特率必须大于 0。");
        }

        if (DataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(DataBits), "数据位必须在 5 到 8 之间。");
        }

        if (StopBits == StopBits.None)
        {
            throw new ArgumentOutOfRangeException(nameof(StopBits), "停止位不能为 None。");
        }
    }
}
