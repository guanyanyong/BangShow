using System.Diagnostics;
using BangShow.Core;

namespace BangShow.Core.Tests;

public class FrameRateLimiterTests
{
    [Fact]
    public void FirstCall_AlwaysPasses()
    {
        var limiter = new FrameRateLimiter(10);

        Assert.True(limiter.TryAcquire(0));
        Assert.Equal(0L, limiter.FilteredCount);
    }

    [Fact]
    public void Interval_MatchesRequestedRate()
    {
        var limiter = new FrameRateLimiter(10);

        // 10 次/秒 => 100ms；允许 1 个刻度的取整误差
        Assert.InRange(limiter.MinIntervalTicks, Stopwatch.Frequency / 10, Stopwatch.Frequency / 10 + 1);
        Assert.Equal(10, limiter.MaxPerSecond);
    }

    [Fact]
    public void CallsWithinInterval_AreFiltered()
    {
        var limiter = new FrameRateLimiter(10);
        long interval = limiter.MinIntervalTicks;

        Assert.True(limiter.TryAcquire(0));
        Assert.False(limiter.TryAcquire(interval - 1));
        Assert.Equal(1L, limiter.FilteredCount);

        Assert.True(limiter.TryAcquire(interval));
        Assert.Equal(1L, limiter.FilteredCount);
    }

    [Fact]
    public void SpanningOneSecond_AllowsTenFrames()
    {
        var limiter = new FrameRateLimiter(10);
        long interval = limiter.MinIntervalTicks;
        long step = Math.Max(1, interval / 4);
        int passed = 0;

        // 每秒窗口内均匀调用 40 次，只有 10 次能通过
        for (int i = 0; i < 40; i++)
        {
            if (limiter.TryAcquire(i * step))
            {
                passed++;
            }
        }

        Assert.Equal(10, passed);
        Assert.Equal(30L, limiter.FilteredCount);
    }

    [Fact]
    public void HighFrequencyBurst_OnlyKeepsFirstFrame()
    {
        var limiter = new FrameRateLimiter(10);
        int passed = 0;

        // 同一时刻涌入 500 帧，只允许 1 帧通过
        for (int i = 0; i < 500; i++)
        {
            if (limiter.TryAcquire(123_456_789))
            {
                passed++;
            }
        }

        Assert.Equal(1, passed);
        Assert.Equal(499L, limiter.FilteredCount);
    }

    [Fact]
    public void Reset_ClearsStateAndCounter()
    {
        var limiter = new FrameRateLimiter(10);
        limiter.TryAcquire(0);
        limiter.TryAcquire(0);
        Assert.Equal(1L, limiter.FilteredCount);

        limiter.Reset();

        Assert.Equal(0L, limiter.FilteredCount);
        Assert.True(limiter.TryAcquire(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidRate_Throws(int maxPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateLimiter(maxPerSecond));
    }
}
