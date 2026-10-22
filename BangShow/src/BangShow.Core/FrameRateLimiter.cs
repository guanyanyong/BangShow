using System.Diagnostics;

namespace BangShow.Core;

/// <summary>
/// 固定最小间隔的限流器：两次允许通过之间至少间隔 <c>1 / maxPerSecond</c> 秒。
/// <para>
/// 用于把高频数据限制到界面可接受的刷新频率：未通过限流的调用可选择丢弃，
/// 并通过 <see cref="FilteredCount"/> 记录被过滤的次数。
/// </para>
/// </summary>
/// <remarks>该类型不是线程安全的，约定由同一个界面线程调用。</remarks>
public sealed class FrameRateLimiter
{
    private readonly long _minIntervalTicks;
    private long _lastPassTicks;
    private long _filteredCount;
    private bool _hasPassed;

    /// <param name="maxPerSecond">每秒最多允许通过的次数（必须大于 0）。</param>
    public FrameRateLimiter(int maxPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPerSecond);

        MaxPerSecond = maxPerSecond;
        // Stopwatch 的刻度频率与 Stopwatch.GetTimestamp() 一致
        _minIntervalTicks = Math.Max(1, Stopwatch.Frequency / maxPerSecond);
    }

    /// <summary>每秒最多允许通过的次数。</summary>
    public int MaxPerSecond { get; }

    /// <summary>两次允许通过之间的最小间隔（以 <see cref="Stopwatch"/> 刻度为单位）。</summary>
    public long MinIntervalTicks => _minIntervalTicks;

    /// <summary>被限流拒绝的累计次数。</summary>
    public long FilteredCount => Interlocked.Read(ref _filteredCount);

    /// <summary>判断当前时刻是否允许通过；未通过时累加 <see cref="FilteredCount"/>。</summary>
    /// <param name="timestamp">当前时刻，取值来自 <see cref="Stopwatch.GetTimestamp"/>。</param>
    public bool TryAcquire(long timestamp)
    {
        if (!_hasPassed || timestamp - _lastPassTicks >= _minIntervalTicks)
        {
            _hasPassed = true;
            _lastPassTicks = timestamp;
            return true;
        }

        Interlocked.Increment(ref _filteredCount);
        return false;
    }

    /// <summary>清空限流状态与过滤计数（重新开始接收时调用）。</summary>
    public void Reset()
    {
        _hasPassed = false;
        _lastPassTicks = 0;
        Interlocked.Exchange(ref _filteredCount, 0);
    }
}
