namespace BangShow.Core.Protocol;

/// <summary>帧内 8 字节数值字段的编码方式。</summary>
public enum NumericFormat
{
    /// <summary>ASCII 十进制文本，例如 "00123.45"、"-0000123"。</summary>
    Ascii = 0,

    /// <summary>紧凑 BCD，8 字节对应 16 位十进制数字。</summary>
    Bcd = 1,
}
