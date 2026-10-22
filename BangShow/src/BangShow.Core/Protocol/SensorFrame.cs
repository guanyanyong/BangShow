using System.Globalization;
using System.Text;

namespace BangShow.Core.Protocol;

/// <summary>
/// 一帧传感器数据：<c>0x02 + 符号(1) + 数值(8) + 单位/状态(1) + 0x03</c>。
/// </summary>
public sealed class SensorFrame
{
    private SensorFrame(
        byte[] raw,
        DateTime timestamp,
        string numberText,
        decimal? value,
        byte unitOrStatus,
        string? unit)
    {
        Raw = raw;
        Timestamp = timestamp;
        NumberText = numberText;
        Value = value;
        UnitOrStatusByte = unitOrStatus;
        Unit = unit;
    }

    /// <summary>完整原始帧（12 字节，含帧头帧尾）。</summary>
    public byte[] Raw { get; }

    /// <summary>接收（解析完成）时刻。</summary>
    public DateTime Timestamp { get; }

    /// <summary>符号字节原始值，例如 0x2B('+')、0x2D('-')、0x00、0x01。</summary>
    public byte SignByte => Raw[1];

    /// <summary>数值字段的解码文本（解析失败时为十六进制文本）。</summary>
    public string NumberText { get; }

    /// <summary>解析出的数值（已合并符号）；无法解析时为 <c>null</c>。</summary>
    public decimal? Value { get; }

    /// <summary>单位/状态字节原始值。</summary>
    public byte UnitOrStatusByte { get; }

    /// <summary>可打印字符时作为单位；否则为 <c>null</c>（该字节表示状态码）。</summary>
    public string? Unit { get; }

    public bool HasValue => Value.HasValue;

    /// <summary>数值显示文本（含符号，无单位）。</summary>
    public string ValueText => Value.HasValue
        ? Value.Value.ToString(CultureInfo.InvariantCulture)
        : NumberText;

    /// <summary>数值 + 单位显示文本。</summary>
    public string ValueWithUnit => Unit is null ? ValueText : $"{ValueText} {Unit}";

    /// <summary>整帧十六进制文本，例如 "02 2B 30 30 ... 03"。</summary>
    public string RawHex => ToHex(Raw);

    /// <summary>数值字段（8 字节）的十六进制文本。</summary>
    public string NumberHex => ToHex(Raw.AsSpan(2, SensorFrameParser.NumberLength));

    /// <summary>解析一帧完整数据。</summary>
    /// <param name="raw">必须正好 12 字节，且以 0x02 开始、0x03 结束。</param>
    /// <param name="numericFormat">数值字段编码方式。</param>
    public static SensorFrame Parse(ReadOnlySpan<byte> raw, NumericFormat numericFormat = NumericFormat.Ascii)
    {
        if (raw.Length != SensorFrameParser.FrameLength)
        {
            throw new ArgumentException($"帧长度必须为 {SensorFrameParser.FrameLength} 字节，实际为 {raw.Length} 字节。", nameof(raw));
        }

        if (raw[0] != SensorFrameParser.StartByte)
        {
            throw new ArgumentException($"帧必须以 0x{SensorFrameParser.StartByte:X2} 开头。", nameof(raw));
        }

        if (raw[^1] != SensorFrameParser.EndByte)
        {
            throw new ArgumentException($"帧必须以 0x{SensorFrameParser.EndByte:X2} 结尾。", nameof(raw));
        }

        byte signByte = raw[1];
        ReadOnlySpan<byte> numberBytes = raw.Slice(2, SensorFrameParser.NumberLength);
        byte unitOrStatusByte = raw[SensorFrameParser.FrameLength - 2];

        string numberText = DecodeNumberText(numberBytes, numericFormat);
        decimal? value = null;
        if (TryDecodeNumber(numberBytes, numericFormat, out decimal parsed))
        {
            // 协议规定符号由独立字节给出，因此以符号字节为准
            value = TryDecodeSign(signByte, out int sign) ? sign * Math.Abs(parsed) : parsed;
        }

        string? unit = unitOrStatusByte is > 0x20 and < 0x7F
            ? ((char)unitOrStatusByte).ToString()
            : null;

        return new SensorFrame(raw.ToArray(), DateTime.Now, numberText, value, unitOrStatusByte, unit);
    }

    public override string ToString() =>
        $"{Timestamp:HH:mm:ss.fff} 值={ValueWithUnit} 符号=0x{SignByte:X2} 单位/状态=0x{UnitOrStatusByte:X2} 原始={RawHex}";

    private static bool TryDecodeSign(byte signByte, out int sign)
    {
        switch (signByte)
        {
            case (byte)'+':
            case 0x00:
                sign = 1;
                return true;

            case (byte)'-':
            case 0x01:
            case 0xFF:
                sign = -1;
                return true;

            default:
                sign = 1;
                return false;
        }
    }

    private static string DecodeNumberText(ReadOnlySpan<byte> bytes, NumericFormat format)
    {
        if (format == NumericFormat.Ascii)
        {
            string text = Encoding.ASCII.GetString(bytes).Trim('\0', ' ');
            return text.Length > 0 ? text : ToHex(bytes);
        }

        // BCD：8 字节对应 16 位十进制数字
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            int high = value >> 4;
            int low = value & 0x0F;
            if (high > 9 || low > 9)
            {
                return ToHex(bytes);
            }

            builder.Append((char)('0' + high));
            builder.Append((char)('0' + low));
        }

        return builder.ToString();
    }

    private static bool TryDecodeNumber(ReadOnlySpan<byte> bytes, NumericFormat format, out decimal value)
    {
        value = 0m;
        string text = DecodeNumberText(bytes, format);
        if (text.Length == 0)
        {
            return false;
        }

        if (format == NumericFormat.Bcd)
        {
            return decimal.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(bytes.Length * 3 - 1);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
