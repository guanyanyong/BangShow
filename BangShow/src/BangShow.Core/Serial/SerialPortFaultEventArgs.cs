namespace BangShow.Core.Serial;

/// <summary>串口异步错误（读取失败、帧错误、奇偶校验错误、设备拔出等）。</summary>
public sealed class SerialPortFaultEventArgs : EventArgs
{
    public SerialPortFaultEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }

    public Exception? Exception { get; }
}
