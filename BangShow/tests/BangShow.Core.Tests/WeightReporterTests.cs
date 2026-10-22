using BangShow.Core;
using BangShow.Core.Api;

namespace BangShow.Core.Tests;

public class WeightReporterTests
{
    /// <summary>2 次/秒对应的最小间隔（Stopwatch 刻度）。</summary>
    private static readonly long Interval = new FrameRateLimiter(2).MinIntervalTicks;

    [Fact]
    public async Task Report_ThenPump_SendsValue()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        reporter.Report(100);
        Assert.True(reporter.HasPending);

        Task? send = reporter.Pump();

        Assert.NotNull(send);
        await send!;
        Assert.Single(api.Calls);
        Assert.Equal(("bang1", 100L), api.Calls[0]);
        Assert.Equal(100L, reporter.LastSentWeight);
        Assert.False(reporter.HasPending);
    }

    [Fact]
    public async Task Report_SameValue_IsNotSentTwice()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        reporter.Report(100);
        await reporter.Pump()!;

        now += Interval;
        reporter.Report(100);

        Assert.Null(reporter.Pump());
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task ChangedValue_WithinInterval_WaitsForNextSlot()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        reporter.Report(100);
        await reporter.Pump()!;

        // 距上次发送不足最小间隔：不发，但数值保留
        now += Interval / 4;
        reporter.Report(200);
        Assert.Null(reporter.Pump());
        Assert.True(reporter.HasPending);
        Assert.Single(api.Calls);

        // 到达最小间隔后补发最新值
        now += Interval;
        Task? send = reporter.Pump();
        Assert.NotNull(send);
        await send!;

        Assert.Equal(2, api.Calls.Count);
        Assert.Equal(200L, api.Calls[^1].Weight);
        Assert.Equal(200L, reporter.LastSentWeight);
    }

    [Fact]
    public async Task AtMostTwoReportsPerSecond()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        // 第 1 个间隔窗口：t=0 发第 1 次
        reporter.Report(1);
        await reporter.Pump()!;

        // 半个间隔后值变化，被限流
        now = Interval / 2;
        reporter.Report(2);
        Assert.Null(reporter.Pump());

        // 到达间隔时发第 2 次（发送当前累积的最新值）
        now = Interval;
        Task? second = reporter.Pump();
        Assert.NotNull(second);
        await second!;

        // 紧接着再变化：仍被限流
        now = Interval + 1;
        reporter.Report(3);
        Assert.Null(reporter.Pump());

        // 进入下一个间隔后允许发送
        now = 2 * Interval;
        Task? third = reporter.Pump();
        Assert.NotNull(third);
        await third!;

        Assert.Equal(3, api.Calls.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, api.Calls.Select(call => call.Weight).ToArray());
    }

    [Fact]
    public async Task SendFailure_KeepsValueAndRetries()
    {
        var api = new FakeWeightApiClient { Failure = new HttpRequestException("服务未启动") };
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        var results = new List<WeightReportedEventArgs>();
        reporter.Reported += (_, e) => results.Add(e);

        reporter.Report(50);
        await reporter.Pump()!;

        Assert.Single(results);
        Assert.False(results[0].Succeeded);
        Assert.Equal(50L, results[0].Weight);
        Assert.True(reporter.HasPending);
        Assert.Null(reporter.LastSentWeight);

        // 服务恢复后自动补发
        api.Failure = null;
        now += Interval;
        Task? retry = reporter.Pump();
        Assert.NotNull(retry);
        await retry!;

        Assert.Equal(2, api.Calls.Count);
        Assert.Equal(50L, reporter.LastSentWeight);
        Assert.True(results[^1].Succeeded);
        Assert.True(results[^1].ServerUpdated);
    }

    [Fact]
    public async Task WhileSending_NewerValueWaitsForNextSlot()
    {
        var gate = new TaskCompletionSource<WeightUpdateResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeWeightApiClient { SendOverride = (_, _) => gate.Task };
        long now = 0;
        using var reporter = new WeightReporter(api, "bang1", 2, () => now);

        reporter.Report(10);
        Task? first = reporter.Pump();
        Assert.NotNull(first);

        // 请求进行中：新值只入队，不会并发发起第二个请求
        reporter.Report(20);
        Assert.Null(reporter.Pump());

        gate.SetResult(new WeightUpdateResult("bang1", 10, true, null));
        await first!;
        Assert.Equal(10L, reporter.LastSentWeight);

        now += Interval;
        Task? second = reporter.Pump();
        Assert.NotNull(second);
        await second!;

        Assert.Equal(new long[] { 10, 20 }, api.Calls.Select(call => call.Weight).ToArray());
        Assert.Equal(20L, reporter.LastSentWeight);
    }

    [Fact]
    public async Task BangnameIsTrimmed()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        using var reporter = new WeightReporter(api, "  bang1  ", 2, () => now);

        reporter.Report(1);
        await reporter.Pump()!;

        Assert.Equal("bang1", api.Calls[0].BangName);
    }

    [Fact]
    public void ReportAfterDispose_DoesNotSend()
    {
        var api = new FakeWeightApiClient();
        long now = 0;
        var reporter = new WeightReporter(api, "bang1", 2, () => now);

        reporter.Dispose();
        reporter.Report(1);

        Assert.Null(reporter.Pump());
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new WeightReporter(null!, "bang1", 2));
        Assert.Throws<ArgumentException>(() => new WeightReporter(new FakeWeightApiClient(), "  ", 2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WeightReporter(new FakeWeightApiClient(), "bang1", 0));
    }

    private sealed class FakeWeightApiClient : IWeightApiClient
    {
        public List<(string BangName, long Weight)> Calls { get; } = [];

        public Exception? Failure { get; set; }

        public Func<string, long, Task<WeightUpdateResult?>>? SendOverride { get; set; }

        public Task<WeightUpdateResult?> UpsertAsync(
            string bangName,
            long weight,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((bangName, weight));

            if (SendOverride is not null)
            {
                return SendOverride(bangName, weight);
            }

            return Failure is null
                ? Task.FromResult<WeightUpdateResult?>(new WeightUpdateResult(bangName, weight, true, null))
                : Task.FromException<WeightUpdateResult?>(Failure);
        }

        public Task<WeightQueryResult?> GetAsync(string bangName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
