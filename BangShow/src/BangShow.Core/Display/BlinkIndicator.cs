namespace BangShow.Core.Display;

/// <summary>
/// 指示球闪烁状态机：数值变化时按固定间隔“灭 / 亮”交替，闪烁指定次数后恢复常亮。
/// <para>
/// 由界面定时器在每个间隔（默认 100ms）调用一次 <see cref="Advance"/> 驱动，
/// 因此可以脱离 WinForms 单元测试，也便于调整闪烁节奏。
/// </para>
/// </summary>
public sealed class BlinkIndicator
{
    private int _remainingTicks;

    /// <param name="blinkCount">闪烁次数（必须大于 0），每次闪烁包含一次熄灭与一次点亮。</param>
    public BlinkIndicator(int blinkCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blinkCount);

        BlinkCount = blinkCount;
    }

    /// <summary>每次变化需要闪烁的次数。</summary>
    public int BlinkCount { get; }

    /// <summary>指示球当前是否点亮；未在闪烁时恒为 <c>true</c>（数值稳定 → 常亮）。</summary>
    public bool IsLit { get; private set; } = true;

    /// <summary>是否正在闪烁。</summary>
    public bool IsBlinking => _remainingTicks > 0;

    /// <summary>
    /// 数值变化时调用：立刻熄灭并重新开始计时。
    /// <para>连续变化会在每次变化时重新计数，即数值持续变动期间一直闪烁。</para>
    /// </summary>
    public void Start()
    {
        // 每次闪烁包含“灭 + 亮”两个节拍，最后一次点亮后直接结束，因此节拍数为 2 * n - 1
        _remainingTicks = (2 * BlinkCount) - 1;
        IsLit = false;
    }

    /// <summary>定时器到点调用：切换明暗并减少剩余节拍，节拍用完后保持常亮。</summary>
    /// <returns>状态是否改变（用于决定是否需要重绘）。</returns>
    public bool Advance()
    {
        if (_remainingTicks <= 0)
        {
            return false;
        }

        _remainingTicks--;
        IsLit = _remainingTicks == 0 || !IsLit;
        return true;
    }

    /// <summary>结束闪烁并恢复常亮（例如重新打开串口、清空显示时调用）。</summary>
    public void Stop()
    {
        _remainingTicks = 0;
        IsLit = true;
    }
}
