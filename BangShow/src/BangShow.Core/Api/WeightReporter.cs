using System.Diagnostics;

namespace BangShow.Core.Api;

/// <summary>一次数值上报的结果。</summary>
public sealed class WeightReportedEventArgs : EventArgs
{
    public WeightReportedEventArgs(long weight, bool serverUpdated, string? error)
    {
        Weight = weight;
        ServerUpdated = serverUpdated;
        Error = error;
    }

    /// <summary>本次上报的数值。</summary>
    public long Weight { get; }

    /// <summary>服务端是否确认修改了数值（数值未变动时为 false）。</summary>
    public bool ServerUpdated { get; }

    /// <summary>失败原因；成功时为 null。</summary>
    public string? Error { get; }

    public bool Succeeded => Error is null;
}

/// <summary>
/// 数值上报器：数值发生变化才上报，且每秒最多发送 <c>maxReportsPerSecond</c> 次。
/// <para>
/// 用法：界面拿到新数值后调用 <see cref="Report"/>，再由界面定时器周期性调用 <see cref="Pump"/>；
/// HTTP 请求异步执行，不阻塞界面线程。
/// </para>
/// </summary>
public sealed class WeightReporter : IDisposable
{
    private readonly IWeightApiClient _apiClient;
    private readonly string _bangName;
    private readonly FrameRateLimiter _limiter;
    private readonly Func<long> _timestampProvider;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _sync = new();

    private long? _pendingWeight;
    private long? _lastQueuedWeight;
    private long? _lastSentWeight;
    private bool _sending;
    private bool _disposed;

    /// <param name="apiClient">接口客户端。</param>
    /// <param name="bangName">榜名称（来自配置文件）。</param>
    /// <param name="maxReportsPerSecond">每秒最多上报次数。</param>
    /// <param name="timestampProvider">时间戳来源（默认 Stopwatch 高精度计时），测试时可注入。</param>
    public WeightReporter(
        IWeightApiClient apiClient,
        string bangName,
        int maxReportsPerSecond,
        Func<long>? timestampProvider = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(bangName);

        _apiClient = apiClient;
        _bangName = bangName.Trim();
        _limiter = new FrameRateLimiter(maxReportsPerSecond);
        _timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
    }

    /// <summary>一次上报结束（成功或失败）时触发，可能在后台线程触发。</summary>
    public event EventHandler<WeightReportedEventArgs>? Reported;

    /// <summary>最近一次上报成功的数值；尚未成功上报时为 null。</summary>
    public long? LastSentWeight
    {
        get
        {
            lock (_sync)
            {
                return _lastSentWeight;
            }
        }
    }

    /// <summary>是否有等待上报的新数值。</summary>
    public bool HasPending
    {
        get
        {
            lock (_sync)
            {
                return _pendingWeight is not null;
            }
        }
    }

    /// <summary>
    /// 记录界面最新取得的数值。与上次入队的值相同则忽略，避免重复上报。
    /// </summary>
    public void Report(long weight)
    {
        lock (_sync)
        {
            if (_disposed || _lastQueuedWeight == weight)
            {
                return;
            }

            _lastQueuedWeight = weight;
            _pendingWeight = weight;
        }
    }

    /// <summary>
    /// 由界面定时器周期性调用：满足最小间隔且有新数值时发送请求。
    /// </summary>
    /// <returns>本次发起的请求任务；没有发送（无新值 / 被限流 / 正在发送）时返回 null。</returns>
    public Task? Pump()
    {
        long weight;

        lock (_sync)
        {
            if (_disposed || _sending)
            {
                return null;
            }

            if (_pendingWeight is not { } pending)
            {
                return null;
            }

            // 与上次成功上报的值相同：无需发送
            if (pending == _lastSentWeight)
            {
                _pendingWeight = null;
                return null;
            }

            if (!_limiter.TryAcquire(_timestampProvider()))
            {
                return null;
            }

            _pendingWeight = null;
            _sending = true;
            weight = pending;
        }

        return SendAsync(weight);
    }

    private async Task SendAsync(long weight)
    {
        try
        {
            WeightUpdateResult? result = await _apiClient
                .UpsertAsync(_bangName, weight, _cts.Token)
                .ConfigureAwait(false);

            lock (_sync)
            {
                _lastSentWeight = weight;
            }

            Raise(new WeightReportedEventArgs(weight, result?.Updated ?? false, null));
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // 窗体关闭时取消，不再重试
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                // 失败（含超时）：期间没有更新的值则保留本次值，等待下一次重试
                _pendingWeight ??= weight;
            }

            Raise(new WeightReportedEventArgs(weight, false, ex.Message));
        }
        finally
        {
            lock (_sync)
            {
                _sending = false;
            }
        }
    }

    private void Raise(WeightReportedEventArgs args)
    {
        EventHandler<WeightReportedEventArgs>? handler;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            handler = Reported;
        }

        handler?.Invoke(this, args);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingWeight = null;
        }

        _cts.Cancel();
        _apiClient.Dispose();
        _cts.Dispose();
    }
}
