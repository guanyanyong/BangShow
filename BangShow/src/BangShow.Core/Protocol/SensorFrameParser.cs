namespace BangShow.Core.Protocol;

/// <summary>
/// 传感器帧解析器：从连续字节流中提取
/// <c>0x02 + 符号(1) + 数值(8) + 单位/状态(1) + 0x03</c> 的定长帧。
/// 支持粘包、半包，以及帧头/帧尾错位后的自动重新同步。
/// </summary>
/// <remarks>
/// 该类型不是线程安全的。约定由同一个解析线程调用 <see cref="Parse"/>，
/// 串口接收线程只负责把原始字节写入缓存。
/// </remarks>
public sealed class SensorFrameParser
{
    public const byte StartByte = 0x02;
    public const byte EndByte = 0x03;

    public const int SignLength = 1;
    public const int NumberLength = 8;
    public const int UnitLength = 1;

    /// <summary>帧头与帧尾之间的固定负载长度（10 字节）。</summary>
    public const int PayloadLength = SignLength + NumberLength + UnitLength;

    /// <summary>完整帧长度（12 字节）。</summary>
    public const int FrameLength = PayloadLength + 2;

    private readonly List<byte> _pending = new(FrameLength * 4);
    private long _discardedBytes;

    /// <summary>数值字段的编码方式，默认 ASCII。</summary>
    public NumericFormat NumericFormat { get; set; } = NumericFormat.Ascii;

    /// <summary>因帧头/帧尾不匹配而被丢弃的累计字节数。</summary>
    public long DiscardedBytes => Interlocked.Read(ref _discardedBytes);

    /// <summary>当前尚未组成完整帧、等待后续数据的字节数。</summary>
    public int PendingBytes => _pending.Count;

    /// <summary>把新收到的字节追加到解析器，并尽可能多地拆出完整帧。</summary>
    /// <param name="data">本次从缓存读到的原始字节。</param>
    /// <param name="frames">解析出的完整帧会添加到该集合中。</param>
    /// <returns>本次调用解析出的完整帧数。</returns>
    public int Parse(ReadOnlySpan<byte> data, ICollection<SensorFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        for (int i = 0; i < data.Length; i++)
        {
            _pending.Add(data[i]);
        }

        int parsed = 0;
        while (TryTakeFrame(out SensorFrame? frame))
        {
            frames.Add(frame);
            parsed++;
        }

        return parsed;
    }

    /// <summary>清空未完成的半包数据（不影响丢弃计数）。</summary>
    public void Reset() => _pending.Clear();

    private bool TryTakeFrame(out SensorFrame frame)
    {
        frame = null!;

        while (true)
        {
            if (_pending.Count == 0)
            {
                return false;
            }

            // 1. 丢弃帧头之前的无效字节
            int start = _pending.IndexOf(StartByte);
            if (start < 0)
            {
                Interlocked.Add(ref _discardedBytes, _pending.Count);
                _pending.Clear();
                return false;
            }

            if (start > 0)
            {
                Interlocked.Add(ref _discardedBytes, start);
                _pending.RemoveRange(0, start);
            }

            // 2. 数据不足一帧：保留为半包，等待下次数据到达
            if (_pending.Count < FrameLength)
            {
                return false;
            }

            // 3. 帧尾校验失败：丢弃当前帧头，直接滑动到下一个帧头
            //    （逐字节 RemoveAt(0) 在大块数据下是 O(n²)，这里一次性跳过）
            if (_pending[FrameLength - 1] != EndByte)
            {
                int nextStart = _pending.IndexOf(StartByte, 1);
                int drop = nextStart >= 0 ? nextStart : _pending.Count;
                _pending.RemoveRange(0, drop);
                Interlocked.Add(ref _discardedBytes, drop);
                continue;
            }

            byte[] raw = _pending.GetRange(0, FrameLength).ToArray();
            _pending.RemoveRange(0, FrameLength);
            frame = SensorFrame.Parse(raw, NumericFormat);
            return true;
        }
    }
}
