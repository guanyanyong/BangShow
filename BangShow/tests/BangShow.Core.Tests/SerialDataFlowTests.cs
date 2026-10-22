using BangShow.Core.Buffers;
using BangShow.Core.Protocol;

namespace BangShow.Core.Tests;

/// <summary>
/// 模拟串口接收链路：原始字节 -> 环形缓存 -> 解析线程读取 -> 完整帧。
/// </summary>
public class SerialDataFlowTests
{
    [Fact]
    public void BufferToParser_WithFragmentedSerialData_ProducesAllFrames()
    {
        var buffer = new ByteQueueBuffer(256);
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();
        byte[] first = BuildFrame('+', "00123.45", (byte)'V');
        byte[] second = BuildFrame('-', "00000678", (byte)'A');
        byte[] stream = [0xFE, .. first, .. second];

        // 模拟 DataReceived 分三次到达：半包、跨帧、帧尾
        buffer.Write(stream.AsSpan(0, 7));
        Drain(buffer, parser, frames);
        Assert.Empty(frames);

        buffer.Write(stream.AsSpan(7, 10));
        Drain(buffer, parser, frames);
        Assert.Single(frames);
        Assert.Equal(123.45m, frames[0].Value);

        buffer.Write(stream.AsSpan(17));
        Drain(buffer, parser, frames);
        Assert.Equal(2, frames.Count);
        Assert.Equal(-678m, frames[1].Value);
        Assert.Equal(1L, parser.DiscardedBytes);
    }

    private static void Drain(ByteQueueBuffer buffer, SensorFrameParser parser, List<SensorFrame> frames)
    {
        var chunk = new byte[32];
        int read;
        while ((read = buffer.Read(chunk)) > 0)
        {
            parser.Parse(chunk.AsSpan(0, read), frames);
        }
    }

    private static byte[] BuildFrame(char sign, string number, byte unit)
    {
        var frame = new byte[SensorFrameParser.FrameLength];
        frame[0] = SensorFrameParser.StartByte;
        frame[1] = (byte)sign;
        System.Text.Encoding.ASCII.GetBytes(number).CopyTo(frame, 2);
        frame[SensorFrameParser.FrameLength - 2] = unit;
        frame[^1] = SensorFrameParser.EndByte;
        return frame;
    }
}
