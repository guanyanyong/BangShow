using System.IO.Ports;
using BangShow.Core.Protocol;
using BangShow.Core.Serial;

namespace BangShow.Core.Tests;

public class SerialPortSettingsTests
{
    [Fact]
    public void Defaults_MatchProtocolExpectations()
    {
        var settings = new SerialPortSettings { PortName = "COM3" };

        Assert.Equal(9600, settings.BaudRate);
        Assert.Equal(8, settings.DataBits);
        Assert.Equal(Parity.None, settings.Parity);
        Assert.Equal(StopBits.One, settings.StopBits);
        Assert.Equal(Handshake.None, settings.Handshake);
        Assert.Equal(NumericFormat.Ascii, settings.NumericFormat);
        Assert.Null(Record.Exception(settings.Validate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPortName_Throws(string? portName)
    {
        var settings = new SerialPortSettings { PortName = portName! };

        Assert.Throws<ArgumentException>(settings.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-9600)]
    public void Validate_NonPositiveBaudRate_Throws(int baudRate)
    {
        var settings = new SerialPortSettings { PortName = "COM3", BaudRate = baudRate };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void Validate_DataBitsOutOfRange_Throws(int dataBits)
    {
        var settings = new SerialPortSettings { PortName = "COM3", DataBits = dataBits };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void Validate_StopBitsNone_Throws()
    {
        var settings = new SerialPortSettings { PortName = "COM3", StopBits = StopBits.None };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }
}

public class SerialPortServiceTests
{
    [Fact]
    public void NewService_IsClosedAndEmpty()
    {
        using var service = new SerialPortService(1024);

        Assert.False(service.IsOpen);
        Assert.Null(service.Settings);
        Assert.Equal(0L, service.TotalBytesReceived);
        Assert.Equal(0L, service.TotalFrames);
        Assert.Equal(0L, service.DiscardedBytes);
        Assert.Equal(0L, service.OverwrittenBytes);
        Assert.Equal(0, service.BufferedBytes);
        Assert.False(service.TryDequeueFrame(out _));
    }

    [Fact]
    public void NumericFormat_IsForwardedToParser()
    {
        using var service = new SerialPortService(1024);

        Assert.Equal(NumericFormat.Ascii, service.NumericFormat);

        service.NumericFormat = NumericFormat.Bcd;
        Assert.Equal(NumericFormat.Bcd, service.NumericFormat);
    }

    [Fact]
    public void Open_InvalidSettings_ThrowsBeforeTouchingPort()
    {
        using var service = new SerialPortService(1024);

        Assert.Throws<ArgumentException>(
            () => service.Open(new SerialPortSettings { PortName = " " }));
        Assert.False(service.IsOpen);
    }

    [Fact]
    public void Open_NullSettings_Throws()
    {
        using var service = new SerialPortService(1024);

        Assert.Throws<ArgumentNullException>(() => service.Open(null!));
    }
}
