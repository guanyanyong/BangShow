using BangShow.Core.Display;

namespace BangShow.Core.Tests;

public class BlinkIndicatorTests
{
    [Fact]
    public void NewIndicator_IsSteadyLit()
    {
        var indicator = new BlinkIndicator(3);

        Assert.Equal(3, indicator.BlinkCount);
        Assert.True(indicator.IsLit);
        Assert.False(indicator.IsBlinking);
        Assert.False(indicator.Advance());
        Assert.True(indicator.IsLit);
    }

    [Fact]
    public void Start_TurnsOffAndBeginsBlinking()
    {
        var indicator = new BlinkIndicator(3);

        indicator.Start();

        Assert.True(indicator.IsBlinking);
        Assert.False(indicator.IsLit);
    }

    [Fact]
    public void BlinkCountThree_ProducesThreePulsesThenStaysLit()
    {
        var indicator = new BlinkIndicator(3);
        indicator.Start();

        var states = new List<bool>();
        // 3 次闪烁 = 5 个节拍（灭 → 亮 → 灭 → 亮 → 灭 → 亮）
        for (int i = 0; i < 5; i++)
        {
            Assert.True(indicator.Advance());
            states.Add(indicator.IsLit);
        }

        Assert.Equal([true, false, true, false, true], states);
        Assert.False(indicator.IsBlinking);
        Assert.True(indicator.IsLit);

        // 闪烁结束后继续推进不再产生变化
        Assert.False(indicator.Advance());
        Assert.True(indicator.IsLit);
    }

    [Fact]
    public void Start_RestartsSequenceWhileBlinking()
    {
        var indicator = new BlinkIndicator(2);
        indicator.Start();

        Assert.True(indicator.Advance());
        Assert.True(indicator.IsLit);

        // 数值再次变化：重新从熄灭开始，并且仍能完整闪完两次
        indicator.Start();
        Assert.True(indicator.IsBlinking);
        Assert.False(indicator.IsLit);

        var states = new List<bool>();
        for (int i = 0; i < 3; i++)
        {
            Assert.True(indicator.Advance());
            states.Add(indicator.IsLit);
        }

        Assert.Equal([true, false, true], states);
        Assert.False(indicator.IsBlinking);
        Assert.True(indicator.IsLit);
    }

    [Fact]
    public void Stop_EndsBlinkingAndTurnsLit()
    {
        var indicator = new BlinkIndicator(5);
        indicator.Start();
        indicator.Advance();

        indicator.Stop();

        Assert.False(indicator.IsBlinking);
        Assert.True(indicator.IsLit);
        Assert.False(indicator.Advance());
        Assert.True(indicator.IsLit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveBlinkCount(int blinkCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlinkIndicator(blinkCount));
    }
}
