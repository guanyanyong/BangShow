using BangShow.Core.Buffers;

namespace BangShow.Core.Tests;

public class ByteQueueBufferTests
{
    [Fact]
    public void WriteThenRead_ReturnsBytesInOrder()
    {
        var buffer = new ByteQueueBuffer(16);

        buffer.Write([1, 2, 3, 4]);

        Assert.Equal(4, buffer.Count);

        var destination = new byte[4];
        int read = buffer.Read(destination);

        Assert.Equal(4, read);
        Assert.Equal(new byte[] {1, 2, 3, 4}, destination);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Read_RespectsDestinationLength()
    {
        var buffer = new ByteQueueBuffer(16);
        buffer.Write([1, 2, 3, 4, 5]);

        var first = new byte[2];
        var second = new byte[10];

        Assert.Equal(2, buffer.Read(first));
        Assert.Equal(new byte[] {1, 2}, first);
        Assert.Equal(3, buffer.Read(second));
        Assert.Equal(new byte[] {3, 4, 5}, second.AsSpan(0, 3).ToArray());
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void WriteAfterRead_WrapsAroundCapacity()
    {
        var buffer = new ByteQueueBuffer(8);

        buffer.Write([1, 2, 3, 4, 5, 6]);
        var consume = new byte[4];
        Assert.Equal(4, buffer.Read(consume));

        // 鍐欏叆 6 瀛楄妭锛氬熬閮ㄥ墿浣?2 瀛楄妭 + 澶撮儴 4 瀛楄妭锛岃Е鍙戠幆褰㈠洖缁?
        buffer.Write([7, 8, 9, 10, 11, 12]);

        var destination = new byte[8];
        int read = buffer.Read(destination);

        Assert.Equal(8, read);
        Assert.Equal(new byte[] {5, 6, 7, 8, 9, 10, 11, 12}, destination);
        Assert.Equal(0, buffer.OverwrittenBytes);
    }

    [Fact]
    public void WriteOverCapacity_DropsOldestBytes()
    {
        var buffer = new ByteQueueBuffer(4);

        buffer.Write([1, 2, 3]);
        buffer.Write([4, 5]);

        Assert.Equal(4, buffer.Count);
        Assert.Equal(1L, buffer.OverwrittenBytes);

        var destination = new byte[4];
        buffer.Read(destination);

        Assert.Equal(new byte[] {2, 3, 4, 5}, destination);
    }

    [Fact]
    public void WriteLargerThanCapacity_KeepsLastBytes()
    {
        var buffer = new ByteQueueBuffer(4);

        buffer.Write([1, 2, 3, 4, 5, 6]);

        Assert.Equal(4, buffer.Count);
        Assert.Equal(2L, buffer.OverwrittenBytes);

        var destination = new byte[4];
        buffer.Read(destination);

        Assert.Equal(new byte[] {3, 4, 5, 6}, destination);
    }

    [Fact]
    public void Clear_RemovesAllBufferedBytes()
    {
        var buffer = new ByteQueueBuffer(8);
        buffer.Write([1, 2, 3]);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Read(new byte[4]));
    }

    [Fact]
    public async Task ConcurrentWriteAndRead_DoesNotLoseBytes()
    {
        var buffer = new ByteQueueBuffer(65_536);
        const int total = 50_000;
        int produced = 0;
        int consumed = 0;

        var producer = Task.Run(() =>
        {
            for (int i = 0; i < total; i++)
            {
                buffer.Write([(byte)(i % 256)]);
            }

            Volatile.Write(ref produced, total);
        });

        var consumer = Task.Run(() =>
        {
            var chunk = new byte[64];
            while (Volatile.Read(ref produced) == 0 || buffer.Count > 0)
            {
                int read = buffer.Read(chunk);
                consumed += read;
                if (read == 0)
                {
                    Thread.Yield();
                }
            }
        });

        await Task.WhenAll(producer, consumer);

        Assert.Equal(total, consumed);
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0L, buffer.OverwrittenBytes);
    }
}

