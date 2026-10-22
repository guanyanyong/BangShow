using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using BangShow.Core.Buffers;
using BangShow.Core.Protocol;

namespace BangShow.Core.Serial;

/// <summary>
/// 串口数据采集服务。
/// <para>
/// 工作流程：串口 <see cref="SerialPort.DataReceived"/> 只把收到的原始字节写入
/// <see cref="ByteQueueBuffer"/>；独立的后台解析线程从缓存读取字节流，
/// 由 <see cref="SensorFrameParser"/> 拆出完整帧后放入线程安全队列，
/// 界面通过 <see cref="TryDequeueFrame"/> 取出并显示。
/// </para>
/// </summary>
public sealed class SerialPortService : IDisposable
{
    private readonly object _stateLock = new();
    private readonly object _readLock = new();
    private readonly SerialPort _serialPort;
    private readonly ByteQueueBuffer _receiveBuffer;
    private readonly SensorFrameParser _parser;
    private readonly ConcurrentQueue<SensorFrame> _frames = new();
    private readonly AutoResetEvent _dataArrived = new(false);
    private readonly byte[] _readChunk = new byte[8 * 1024];
    private readonly byte[] _pumpChunk = new byte[8 * 1024];
    private readonly List<SensorFrame> _parsedFrames = new(32);

    private Thread? _pumpThread;
    private volatile bool _running;
    private long _totalBytesReceived;
    private long _totalFrames;
    private bool _disposed;

    /// <param name="receiveCacheCapacity">接收缓存容量（字节）。</param>
    public SerialPortService(int receiveCacheCapacity = 64 * 1024)
    {
        _receiveBuffer = new ByteQueueBuffer(receiveCacheCapacity);
        _parser = new SensorFrameParser();
        _serialPort = new SerialPort();
    }

    /// <summary>串口读取/解析过程中的异常通知（在后台线程触发）。</summary>
    public event EventHandler<SerialPortFaultEventArgs>? Faulted;

    public bool IsOpen
    {
        get
        {
            lock (_stateLock)
            {
                return !_disposed && _serialPort.IsOpen;
            }
        }
    }

    /// <summary>当前生效的串口参数（未打开时为 <c>null</c>）。</summary>
    public SerialPortSettings? Settings { get; private set; }

    /// <summary>累计从串口读取的原始字节数。</summary>
    public long TotalBytesReceived => Interlocked.Read(ref _totalBytesReceived);

    /// <summary>累计解析出的有效帧数。</summary>
    public long TotalFrames => Interlocked.Read(ref _totalFrames);

    /// <summary>解析过程中丢弃的无效字节数（帧头/帧尾不匹配）。</summary>
    public long DiscardedBytes => _parser.DiscardedBytes;

    /// <summary>接收缓存因写满而覆盖的字节数。</summary>
    public long OverwrittenBytes => _receiveBuffer.OverwrittenBytes;

    /// <summary>接收缓存中当前剩余的字节数。</summary>
    public int BufferedBytes => _receiveBuffer.Count;

    /// <summary>数值字段编码方式（需在 <see cref="Open"/> 前设置）。</summary>
    public NumericFormat NumericFormat
    {
        get => _parser.NumericFormat;
        set => _parser.NumericFormat = value;
    }

    /// <summary>打开串口并启动后台解析线程。</summary>
    public void Open(SerialPortSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_serialPort.IsOpen)
            {
                throw new InvalidOperationException("串口已经打开。");
            }

            ResetQueuesAndCounters();
            _parser.NumericFormat = settings.NumericFormat;

            _serialPort.PortName = settings.PortName;
            _serialPort.BaudRate = settings.BaudRate;
            _serialPort.DataBits = settings.DataBits;
            _serialPort.Parity = settings.Parity;
            _serialPort.StopBits = settings.StopBits;
            _serialPort.Handshake = settings.Handshake;
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;

            try
            {
                _serialPort.Open();
            }
            catch
            {
                _serialPort.DataReceived -= OnDataReceived;
                _serialPort.ErrorReceived -= OnErrorReceived;
                throw;
            }

            Settings = settings;
            _running = true;
            _pumpThread = new Thread(PumpLoop)
            {
                IsBackground = true,
                Name = "BangShow.SerialPump",
            };
            _pumpThread.Start();
        }
    }

    /// <summary>关闭串口并停止后台解析线程。可重复调用。</summary>
    public void Close()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _running = false;
            _dataArrived.Set();

            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.ErrorReceived -= OnErrorReceived;

            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                RaiseFault($"关闭串口失败：{ex.Message}", ex);
            }

            Thread? pumpThread = _pumpThread;
            _pumpThread = null;
            if (pumpThread is not null && pumpThread != Thread.CurrentThread)
            {
                pumpThread.Join(TimeSpan.FromSeconds(2));
            }

            Settings = null;
        }
    }

    /// <summary>尝试从队列取出一帧已解析的数据。</summary>
    public bool TryDequeueFrame([NotNullWhen(true)] out SensorFrame? frame) => _frames.TryDequeue(out frame);

    /// <summary>丢弃所有尚未显示的历史帧。</summary>
    public void ClearFrames()
    {
        while (_frames.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
            _serialPort.Dispose();
            _dataArrived.Dispose();
        }
    }

    private void ResetQueuesAndCounters()
    {
        _receiveBuffer.Clear();
        _receiveBuffer.ResetOverwrittenCounter();
        _parser.Reset();
        ClearFrames();
        Interlocked.Exchange(ref _totalBytesReceived, 0);
        Interlocked.Exchange(ref _totalFrames, 0);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            lock (_readLock)
            {
                while (_serialPort.IsOpen)
                {
                    int available = _serialPort.BytesToRead;
                    if (available <= 0)
                    {
                        break;
                    }

                    int read = _serialPort.Read(_readChunk, 0, Math.Min(available, _readChunk.Length));
                    if (read <= 0)
                    {
                        break;
                    }

                    _receiveBuffer.Write(_readChunk.AsSpan(0, read));
                    Interlocked.Add(ref _totalBytesReceived, read);
                }
            }

            _dataArrived.Set();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException
                                       or UnauthorizedAccessException or ObjectDisposedException)
        {
            RaiseFault($"读取串口数据失败：{ex.Message}", ex);
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e) =>
        RaiseFault($"串口错误：{e.EventType}");

    private void PumpLoop()
    {
        try
        {
            while (_running)
            {
                _dataArrived.WaitOne(100);
                if (!_running)
                {
                    break;
                }

                // 把缓存中所有可用数据消费掉，避免积压
                while (_running)
                {
                    int read = _receiveBuffer.Read(_pumpChunk);
                    if (read == 0)
                    {
                        break;
                    }

                    _parsedFrames.Clear();
                    _parser.Parse(_pumpChunk.AsSpan(0, read), _parsedFrames);
                    foreach (SensorFrame frame in _parsedFrames)
                    {
                        _frames.Enqueue(frame);
                        Interlocked.Increment(ref _totalFrames);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RaiseFault($"解析数据时发生异常：{ex.Message}", ex);
        }
    }

    private void RaiseFault(string message, Exception? exception = null) =>
        Faulted?.Invoke(this, new SerialPortFaultEventArgs(message, exception));
}
