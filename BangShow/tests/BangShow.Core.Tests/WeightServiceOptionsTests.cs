using BangShow.Core.Api;

namespace BangShow.Core.Tests;

public class WeightServiceOptionsTests
{
    [Fact]
    public void DefaultOptions_AreValid()
    {
        var options = new WeightServiceOptions { BangName = "bang1" };

        options.Validate();
        Assert.Equal("http://127.0.0.1:8765", options.BaseUrl);
        Assert.Equal(2, options.MaxReportsPerSecond);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://127.0.0.1")]
    [InlineData("not-a-url")]
    public void InvalidBaseUrl_Throws(string baseUrl)
    {
        var options = new WeightServiceOptions { BaseUrl = baseUrl, BangName = "bang1" };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyBangName_Throws(string bangName)
    {
        var options = new WeightServiceOptions { BangName = bangName };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidReportsPerSecond_Throws(int maxPerSecond)
    {
        var options = new WeightServiceOptions { BangName = "bang1", MaxReportsPerSecond = maxPerSecond };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void InvalidTimeout_Throws(int timeoutSeconds)
    {
        var options = new WeightServiceOptions { BangName = "bang1", TimeoutSeconds = timeoutSeconds };

        Assert.Throws<ArgumentException>(options.Validate);
    }
}
