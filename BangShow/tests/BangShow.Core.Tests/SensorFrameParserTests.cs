using System.Text;
using BangShow.Core.Protocol;

namespace BangShow.Core.Tests;

public class SensorFrameParserTests
{
    [Fact]
    public void Parse_SingleFrame_ReturnsParsedValueAndUnit()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        int parsed = parser.Parse(BuildFrame('+', "00123.45", (byte)'V'), frames);

        Assert.Equal(1, parsed);
        SensorFrame frame = Assert.Single(frames);
        Assert.True(frame.HasValue);
        Assert.Equal(123.45m, frame.Value);
        Assert.Equal("00123.45", frame.NumberText);
        Assert.Equal("V", frame.Unit);
        Assert.Equal((byte)'V', frame.UnitOrStatusByte);
        Assert.Equal((byte)'+', frame.SignByte);
        Assert.Equal(12, frame.Raw.Length);
        Assert.Equal("02 2B 30 30 31 32 33 2E 34 35 56 03", frame.RawHex);
        Assert.Equal(0, parser.PendingBytes);
    }

    [Fact]
    public void Parse_NegativeSignByte_AppliesMinusSign()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse(BuildFrame('-', "00000042", (byte)'A'), frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(-42m, frame.Value);
        Assert.Equal("-42 A", frame.ValueWithUnit);
    }

    [Theory]
    [InlineData(0x00, 100)]
    [InlineData(0x01, -100)]
    [InlineData(0xFF, -100)]
    public void Parse_BinarySignByte_IsSupported(byte signByte, int expected)
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse(BuildFrame(signByte, "00000100", (byte)'V'), frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(expected, frame.Value);
    }

    [Fact]
    public void Parse_SplitAcrossChunks_KeepsPartialFrameUntilComplete()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();
        byte[] raw = BuildFrame('+', "00001234", (byte)'V');

        int first = parser.Parse(raw.AsSpan(0, 5), frames);

        Assert.Equal(0, first);
        Assert.Empty(frames);
        Assert.Equal(5, parser.PendingBytes);

        int second = parser.Parse(raw.AsSpan(5), frames);

        Assert.Equal(1, second);
        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(1234m, frame.Value);
        Assert.Equal(0, parser.PendingBytes);
    }

    [Fact]
    public void Parse_MultipleFramesInOneChunk_ReturnsAllFrames()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        byte[] data = [.. BuildFrame('+', "00000001", (byte)'V'), .. BuildFrame('-', "00000002", (byte)'A')];

        int parsed = parser.Parse(data, frames);

        Assert.Equal(2, parsed);
        Assert.Equal(1m, frames[0].Value);
        Assert.Equal(-2m, frames[1].Value);
    }

    [Fact]
    public void Parse_NoiseBeforeFrame_IsCountedAsDiscarded()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();
        byte[] data = [0x11, 0x22, 0x33, .. BuildFrame('+', "00000007", (byte)'V')];

        parser.Parse(data, frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(7m, frame.Value);
        Assert.Equal(3L, parser.DiscardedBytes);
    }

    [Fact]
    public void Parse_BadTailByte_ResyncsToNextValidFrame()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        byte[] badFrame = BuildFrame('+', "00000001", (byte)'V');
        badFrame[^1] = 0x00;
        byte[] data = [.. badFrame, .. BuildFrame('+', "00000002", (byte)'V')];

        parser.Parse(data, frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(2m, frame.Value);
        Assert.Equal(12L, parser.DiscardedBytes);
    }

    [Fact]
    public void Parse_NonPrintableUnitByte_LeavesUnitNull()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse(BuildFrame('+', "00000005", 0x00), frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Null(frame.Unit);
        Assert.Equal((byte)0x00, frame.UnitOrStatusByte);
        Assert.Equal("5", frame.ValueText);
    }

    [Fact]
    public void Parse_AsciiNumberWithSpaces_IsTrimmed()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse(BuildFrame('+', " 123.45 ", (byte)'V'), frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(123.45m, frame.Value);
    }

    [Fact]
    public void Parse_UnparseableNumber_KeepsHexTextAndNullValue()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse(BuildFrame('+', "ABCDEFGH", (byte)'V'), frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.False(frame.HasValue);
        Assert.Null(frame.Value);
        Assert.Equal("ABCDEFGH", frame.NumberText);
        Assert.Equal("41 42 43 44 45 46 47 48", frame.NumberHex);
    }

    [Fact]
    public void Parse_BcdNumber_IsDecoded()
    {
        var parser = new SensorFrameParser { NumericFormat = NumericFormat.Bcd };
        var frames = new List<SensorFrame>();

        byte[] raw = BuildFrame('+', "00000000", (byte)'V');
        byte[] bcd = [0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56];
        bcd.CopyTo(raw, 2);

        parser.Parse(raw, frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(1234567890123456m, frame.Value);
        Assert.Equal("1234567890123456", frame.NumberText);
    }

    [Fact]
    public void Parse_EmptyInput_ProducesNothing()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        int parsed = parser.Parse(ReadOnlySpan<byte>.Empty, frames);

        Assert.Equal(0, parsed);
        Assert.Empty(frames);
        Assert.Equal(0, parser.PendingBytes);
    }

    [Fact]
    public void Reset_ClearsPendingHalfFrame()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();
        byte[] raw = BuildFrame('+', "00000001", (byte)'V');

        parser.Parse(raw.AsSpan(0, 6), frames);
        Assert.Equal(6, parser.PendingBytes);

        parser.Reset();

        Assert.Equal(0, parser.PendingBytes);
        Assert.Empty(frames);
    }

    [Fact]
    public void SensorFrameParse_WrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => SensorFrame.Parse(new byte[SensorFrameParser.FrameLength - 1]));
    }

    [Fact]
    public void SensorFrameParse_WrongStartByte_Throws()
    {
        byte[] raw = BuildFrame('+', "00000001", (byte)'V');
        raw[0] = 0x01;

        Assert.Throws<ArgumentException>(() => SensorFrame.Parse(raw));
    }

    [Fact]
    public void Parse_TwoCorruptedFrames_SkipsBothAndKeepsCounting()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        byte[] firstBad = BuildFrame('+', "00000001", (byte)'V');
        firstBad[^1] = 0x00;
        byte[] secondBad = BuildFrame('+', "00000002", (byte)'V');
        secondBad[^1] = 0x00;
        byte[] data = [.. firstBad, .. secondBad, .. BuildFrame('+', "00000003", (byte)'V')];

        parser.Parse(data, frames);

        SensorFrame frame = Assert.Single(frames);
        Assert.Equal(3m, frame.Value);
        Assert.Equal(24L, parser.DiscardedBytes);
    }

    [Fact]
    public void Parse_DataWithoutStartByte_IsDiscardedEntirely()
    {
        var parser = new SensorFrameParser();
        var frames = new List<SensorFrame>();

        parser.Parse([0x41, 0x42, 0x43, 0x03, 0x44], frames);

        Assert.Empty(frames);
        Assert.Equal(5L, parser.DiscardedBytes);
        Assert.Equal(0, parser.PendingBytes);
    }

    private static byte[] BuildFrame(char sign, string number, byte unit) =>
        BuildFrame((byte)sign, number, unit);

    private static byte[] BuildFrame(byte signByte, string number, byte unit)
    {
        Assert.True(number.Length <= SensorFrameParser.NumberLength);

        var frame = new byte[SensorFrameParser.FrameLength];
        frame[0] = SensorFrameParser.StartByte;
        frame[1] = signByte;
        Encoding.ASCII
            .GetBytes(number.PadLeft(SensorFrameParser.NumberLength))
            .CopyTo(frame, 2);
        frame[SensorFrameParser.FrameLength - 2] = unit;
        frame[^1] = SensorFrameParser.EndByte;
        return frame;
    }
}
