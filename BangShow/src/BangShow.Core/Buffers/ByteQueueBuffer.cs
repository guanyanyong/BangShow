namespace BangShow.Core.Buffers;

/// <summary>
/// 固定容量的线程安全环形字节缓存（FIFO）。
/// 串口接收线程只负责 <see cref="Write"/>，解析线程只负责 <see cref="Read"/>；
/// 缓存写满时会丢弃最旧的数据，并通过 <see cref="OverwrittenBytes"/> 记录丢弃量。
/// </summary>
public sealed class ByteQueueBuffer
{
    private readonly object _sync = new();
    private readonly byte[] _buffer;
    private int _head;
    private int _count;
    private long _overwrittenBytes;

    /// <param name="capacity">缓存容量（字节），默认 64 KB。</param>
    public ByteQueueBuffer(int capacity = 64 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new byte[capacity];
    }

    public int Capacity => _buffer.Length;

    /// <summary>当前缓存中待读取的字节数。</summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    /// <summary>因缓存写满而被覆盖的累计字节数。</summary>
    public long OverwrittenBytes => Interlocked.Read(ref _overwrittenBytes);

    /// <summary>把数据写入缓存尾部；空间不足时覆盖最旧的数据。</summary>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        lock (_sync)
        {
            // 单次写入就超过整个缓存容量：只保留最后 Capacity 个字节
            if (data.Length >= _buffer.Length)
            {
                int skipped = data.Length - _buffer.Length + _count;
                Interlocked.Add(ref _overwrittenBytes, skipped);
                data[^_buffer.Length..].CopyTo(_buffer);
                _head = 0;
                _count = _buffer.Length;
                return;
            }

            int free = _buffer.Length - _count;
            if (data.Length > free)
            {
                int dropped = data.Length - free;
                _head = (_head + dropped) % _buffer.Length;
                _count -= dropped;
                Interlocked.Add(ref _overwrittenBytes, dropped);
            }

            int tail = (_head + _count) % _buffer.Length;
            int firstPart = Math.Min(data.Length, _buffer.Length - tail);
            data[..firstPart].CopyTo(_buffer.AsSpan(tail, firstPart));
            if (firstPart < data.Length)
            {
                data[firstPart..].CopyTo(_buffer);
            }

            _count += data.Length;
        }
    }

    /// <summary>从缓存头部读取最多 <paramref name="destination"/> 长度的数据。</summary>
    /// <returns>实际读取的字节数；缓存为空时返回 0。</returns>
    public int Read(Span<byte> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        lock (_sync)
        {
            int length = Math.Min(destination.Length, _count);
            if (length == 0)
            {
                return 0;
            }

            int firstPart = Math.Min(length, _buffer.Length - _head);
            _buffer.AsSpan(_head, firstPart).CopyTo(destination);
            if (firstPart < length)
            {
                _buffer.AsSpan(0, length - firstPart).CopyTo(destination[firstPart..]);
            }

            _head = (_head + length) % _buffer.Length;
            _count -= length;
            return length;
        }
    }

    /// <summary>清空缓存（不影响累计的溢出统计）。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _head = 0;
            _count = 0;
        }
    }

    /// <summary>清零溢出字节计数。</summary>
    public void ResetOverwrittenCounter() => Interlocked.Exchange(ref _overwrittenBytes, 0);
}
