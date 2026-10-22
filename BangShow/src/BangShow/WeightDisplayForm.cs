using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using BangShow.Core.Display;

namespace BangShow;

/// <summary>
/// 地磅数值大屏：画面中只显示数值，数值正下方有一个绿色指示球。
/// <para>数值变化时指示球以 100ms 为间隔闪烁 3 次（约 0.5 秒）；数值稳定后停止闪烁、保持常亮。</para>
/// <para>无边框全屏：默认铺满主窗口所在屏幕，按 F11 / 左右方向键切换显示器，按 Esc 或双击退出。</para>
/// </summary>
public sealed class WeightDisplayForm : Form
{
    /// <summary>闪烁间隔：0.1 秒一次。</summary>
    private const int BlinkIntervalMilliseconds = 100;

    /// <summary>每次数值变化闪烁 3 次。</summary>
    private const int BlinkCount = 3;

    private const int FrameInset = 24;

    private static readonly Color BackgroundTop = Color.FromArgb(3, 9, 17);
    private static readonly Color BackgroundBottom = Color.FromArgb(10, 33, 54);
    private static readonly Color AccentBlue = Color.FromArgb(41, 182, 246);
    private static readonly Color DimText = Color.FromArgb(150, 120, 144, 156);

    private readonly BlinkIndicator _indicator = new(BlinkCount);
    private readonly System.Windows.Forms.Timer _blinkTimer = new();
    private readonly Screen[] _screens;

    private long? _weight;
    private string? _unit;
    private DateTime? _lastChangedAt;
    private int _screenIndex;

    /// <param name="targetScreen">
    /// 大屏所在屏幕；为 <c>null</c> 时使用主屏。多屏时可用 F11 / 左右方向键切换。
    /// </param>
    public WeightDisplayForm(Screen? targetScreen = null)
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = BackgroundTop;
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        ShowIcon = false;
        StartPosition = FormStartPosition.Manual;
        Text = "地磅数值";
        TopMost = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);

        _screens = Screen.AllScreens;
        _screenIndex = ResolveScreenIndex(targetScreen);

        _blinkTimer.Interval = BlinkIntervalMilliseconds;
        _blinkTimer.Tick += OnBlinkTimerTick;

        ApplyScreenBounds();
    }

    /// <summary>
    /// 更新显示数值（由主界面在串口数值变化时调用）。
    /// <para>与当前显示相同的数值不会重新触发闪烁，因此“数值稳定”时指示球保持常亮。</para>
    /// </summary>
    /// <param name="weight">已取整的数值。</param>
    /// <param name="unit">单位文本，可为 <c>null</c>。</param>
    public void UpdateWeight(long weight, string? unit)
    {
        if (_weight == weight && string.Equals(_unit, unit, StringComparison.Ordinal))
        {
            return;
        }

        _weight = weight;
        _unit = unit;
        _lastChangedAt = DateTime.Now;

        _indicator.Start();
        _blinkTimer.Start();
        Invalidate();
    }

    /// <summary>清空显示，回到“等待数据”状态并停止闪烁（重新打开串口时调用）。</summary>
    public void ResetWeight()
    {
        _blinkTimer.Stop();
        _indicator.Stop();
        _weight = null;
        _unit = null;
        _lastChangedAt = null;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blinkTimer.Stop();
            _blinkTimer.Tick -= OnBlinkTimerTick;
            _blinkTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        if (keyData is Keys.F11 or Keys.Right)
        {
            CycleScreen(1);
            return true;
        }

        if (keyData == Keys.Left)
        {
            CycleScreen(-1);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        Close();
    }

    private void OnBlinkTimerTick(object? sender, EventArgs e)
    {
        if (!_indicator.Advance())
        {
            _blinkTimer.Stop();
            return;
        }

        if (!_indicator.IsBlinking)
        {
            _blinkTimer.Stop();
        }

        Invalidate();
    }

    /// <summary>确定大屏所在屏幕：优先使用调用方给出的屏幕，找不到时退回主屏。</summary>
    private int ResolveScreenIndex(Screen? targetScreen)
    {
        if (_screens.Length == 0)
        {
            return 0;
        }

        if (targetScreen is not null)
        {
            int index = Array.FindIndex(
                _screens,
                screen => string.Equals(screen.DeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                return index;
            }
        }

        int primaryIndex = Array.FindIndex(_screens, screen => screen.Primary);
        return primaryIndex >= 0 ? primaryIndex : 0;
    }

    /// <summary>铺满当前选中的屏幕（无边框全屏）。</summary>
    private void ApplyScreenBounds()
    {
        Bounds = _screens.Length == 0
            ? new Rectangle(0, 0, 1280, 720)
            : _screens[_screenIndex].Bounds;
    }

    /// <summary>在多个显示器之间轮换显示（F11 / 左右方向键）。</summary>
    private void CycleScreen(int step)
    {
        if (_screens.Length <= 1)
        {
            return;
        }

        _screenIndex = ((_screenIndex + step) % _screens.Length + _screens.Length) % _screens.Length;
        ApplyScreenBounds();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        Rectangle client = ClientRectangle;
        if (client.Width < 160 || client.Height < 160)
        {
            return;
        }

        Rectangle frame = Rectangle.Inflate(client, -FrameInset, -FrameInset);

        PaintBackground(g, client);
        PaintTechFrame(g, frame);
        PaintHeader(g, frame);

        RectangleF digits = PaintValue(g, client);
        PaintIndicator(g, client, digits);
        PaintFooter(g, frame);
    }

    /// <summary>深蓝渐变背景 + 网格 + 中央柔光，营造仪表盘的科技感。</summary>
    private static void PaintBackground(Graphics g, Rectangle client)
    {
        using (var background = new LinearGradientBrush(client, BackgroundTop, BackgroundBottom, LinearGradientMode.Vertical))
        {
            g.FillRectangle(background, client);
        }

        int spacing = Math.Max(32, client.Height / 16);
        using var gridPen = new Pen(Color.FromArgb(14, 64, 150, 220), 1f);
        for (int x = spacing; x < client.Width; x += spacing)
        {
            g.DrawLine(gridPen, x, 0, x, client.Height);
        }

        for (int y = spacing; y < client.Height; y += spacing)
        {
            g.DrawLine(gridPen, 0, y, client.Width, y);
        }

        // 数值区域背后的柔光，让大字号数字更“发光”
        int width = (int)(client.Width * 0.62f);
        int height = (int)(client.Height * 0.42f);
        var glowBounds = new Rectangle(
            (client.Width / 2) - (width / 2),
            (int)(client.Height * 0.44f) - (height / 2),
            width,
            height);

        using var path = new GraphicsPath();
        path.AddEllipse(glowBounds);
        using var glow = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(32, 41, 121, 255),
            SurroundColors = [Color.FromArgb(0, 41, 121, 255)],
        };
        g.FillEllipse(glow, glowBounds);
    }

    /// <summary>细边框 + 四角高亮括号 + 顶部装饰线。</summary>
    private static void PaintTechFrame(Graphics g, Rectangle frame)
    {
        using (var borderPen = new Pen(Color.FromArgb(46, 33, 150, 243), 1f))
        {
            g.DrawRectangle(borderPen, frame);
        }

        int length = Math.Max(28, Math.Min(frame.Width, frame.Height) / 12);
        Rectangle corner = Rectangle.Inflate(frame, 10, 10);

        using var bracketPen = new Pen(Color.FromArgb(220, 41, 182, 246), 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        // 左上
        g.DrawLine(bracketPen, corner.Left, corner.Top, corner.Left + length, corner.Top);
        g.DrawLine(bracketPen, corner.Left, corner.Top, corner.Left, corner.Top + length);

        // 右上
        g.DrawLine(bracketPen, corner.Right, corner.Top, corner.Right - length, corner.Top);
        g.DrawLine(bracketPen, corner.Right, corner.Top, corner.Right, corner.Top + length);

        // 左下
        g.DrawLine(bracketPen, corner.Left, corner.Bottom, corner.Left + length, corner.Bottom);
        g.DrawLine(bracketPen, corner.Left, corner.Bottom, corner.Left, corner.Bottom - length);

        // 右下
        g.DrawLine(bracketPen, corner.Right, corner.Bottom, corner.Right - length, corner.Bottom);
        g.DrawLine(bracketPen, corner.Right, corner.Bottom, corner.Right, corner.Bottom - length);

        using var accentPen = new Pen(Color.FromArgb(150, 41, 182, 246), 2f);
        g.DrawLine(accentPen, frame.Left, frame.Top, frame.Left + (frame.Width * 0.26f), frame.Top);
    }

    /// <summary>左上角标题：中文主标题 + 英文副标题。</summary>
    private static void PaintHeader(Graphics g, Rectangle frame)
    {
        using var titleFont = new Font("Microsoft YaHei UI", Math.Max(18f, frame.Height * 0.028f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.FromArgb(232, 244, 253));
        g.DrawString("地磅数值", titleFont, titleBrush, frame.Left + 16, frame.Top + 8);

        using var subFont = new Font("Consolas", Math.Max(11f, frame.Height * 0.014f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var subBrush = new SolidBrush(Color.FromArgb(130, 100, 181, 246));
        g.DrawString("W E I G H B R I D G E   D I S P L A Y", subFont, subBrush, frame.Left + 18, frame.Top + 12 + titleFont.Size);
    }

    /// <summary>
    /// 居中绘制大号数值：字号随窗口高度自适应，数字过长时自动缩小以免超出边界。
    /// </summary>
    /// <returns>数值文本所占的矩形，供指示球定位。</returns>
    private RectangleF PaintValue(Graphics g, Rectangle client)
    {
        string text = _weight is { } weight ? weight.ToString(CultureInfo.InvariantCulture) : "----";
        float maxWidth = client.Width * 0.80f;
        float maxHeight = client.Height * 0.40f;

        float size = maxHeight;
        Font font;
        SizeF measured;
        while (true)
        {
            font = new Font("Consolas", size, FontStyle.Bold, GraphicsUnit.Pixel);
            measured = g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
            if (measured.Width <= maxWidth || size <= 48f)
            {
                break;
            }

            font.Dispose();
            size *= 0.88f;
        }

        using (font)
        using (var format = new StringFormat(StringFormat.GenericTypographic)
               {
                   Alignment = StringAlignment.Center,
                   LineAlignment = StringAlignment.Center,
               })
        {
            float centerY = client.Height * 0.45f;
            Font? unitFont = _unit is { Length: > 0 }
                ? new Font("Microsoft YaHei UI", Math.Max(12f, size * 0.22f), FontStyle.Bold, GraphicsUnit.Pixel)
                : null;

            // 数值 + 单位作为一个整体水平居中
            SizeF unitSize = unitFont is null ? SizeF.Empty : g.MeasureString(_unit!, unitFont);
            float gap = unitFont is null ? 0f : size * 0.08f;
            float left = (client.Width - (measured.Width + (unitFont is null ? 0f : gap + unitSize.Width))) / 2f;

            var digits = new RectangleF(left, centerY - (measured.Height / 2f), measured.Width, measured.Height);

            // GDI+ 在布局矩形刚好等于测量尺寸时会裁掉文字，绘制时向外留出余量
            var paintRect = RectangleF.Inflate(digits, size * 0.2f, size * 0.2f);

            if (_weight is null)
            {
                using var pendingBrush = new SolidBrush(Color.FromArgb(60, 129, 212, 250));
                g.DrawString(text, font, pendingBrush, paintRect, format);
            }
            else
            {
                // 外发光：同色文字沿 8 个方向偏移绘制，模拟数码管的辉光
                using (var glowBrush = new SolidBrush(Color.FromArgb(34, AccentBlue)))
                {
                    float offset = Math.Max(2f, size * 0.035f);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = Math.PI / 4 * i;
                        var shifted = new RectangleF(
                            paintRect.X + (float)(Math.Cos(angle) * offset),
                            paintRect.Y + (float)(Math.Sin(angle) * offset),
                            paintRect.Width,
                            paintRect.Height);
                        g.DrawString(text, font, glowBrush, shifted, format);
                    }
                }

                using var valueBrush = new LinearGradientBrush(
                    digits,
                    Color.FromArgb(255, 255, 255, 255),
                    Color.FromArgb(178, 214, 255),
                    LinearGradientMode.Vertical);
                g.DrawString(text, font, valueBrush, paintRect, format);
            }

            if (unitFont is not null)
            {
                using var unitBrush = new SolidBrush(Color.FromArgb(200, 100, 181, 246));
                using var unitFormat = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Far,
                };

                // 与数值底部对齐（留出余量，避免 GDI+ 裁字；单位整体上移一点更贴近基线）
                var unitRect = new RectangleF(
                    digits.Right + gap,
                    digits.Top,
                    unitSize.Width + (size * 0.2f),
                    digits.Height - (size * 0.1f));
                g.DrawString(_unit!, unitFont, unitBrush, unitRect, unitFormat);
            }

            unitFont?.Dispose();
            return digits;
        }
    }

    /// <summary>
    /// 数值正下方的绿色指示球：闪烁时明暗交替，数值稳定后常亮并带柔光。
    /// </summary>
    private void PaintIndicator(Graphics g, Rectangle client, RectangleF digits)
    {
        int radius = (int)Math.Clamp(client.Height * 0.022f, 11f, 28f);
        int centerX = client.Width / 2;
        int centerY = (int)(digits.Bottom + Math.Max(radius * 3f, client.Height * 0.052f));

        bool hasValue = _weight is not null;
        bool lit = _indicator.IsLit;

        if (hasValue && lit)
        {
            // 由内向外三层柔光（最外层半径为 4 * radius）
            for (int ring = 3; ring >= 1; ring--)
            {
                int haloRadius = radius + (ring * radius);
                using var haloBrush = new SolidBrush(Color.FromArgb(22 * (4 - ring), 0, 230, 118));
                g.FillEllipse(haloBrush, centerX - haloRadius, centerY - haloRadius, haloRadius * 2, haloRadius * 2);
            }
        }

        Color coreColor = !hasValue
            ? Color.FromArgb(96, 125, 139)
            : lit ? Color.FromArgb(0, 230, 118) : Color.FromArgb(27, 94, 62);

        using (var coreBrush = new SolidBrush(coreColor))
        {
            g.FillEllipse(coreBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);
        }

        if (hasValue && lit)
        {
            using var highlight = new SolidBrush(Color.FromArgb(170, 220, 255, 235));
            g.FillEllipse(highlight, centerX - (radius * 0.5f), centerY - (radius * 0.62f), radius * 0.75f, radius * 0.75f);
        }

        using var captionFont = new Font("Microsoft YaHei UI", Math.Max(13f, client.Height * 0.019f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var captionBrush = new SolidBrush(hasValue ? Color.FromArgb(190, 129, 212, 250) : DimText);
        using var captionFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
        };

        string caption = !hasValue
            ? "等待数据…"
            : _indicator.IsBlinking ? "数值变化中…" : "数值稳定";

        var captionBounds = new RectangleF(
            0,
            centerY + (radius * 4) + (client.Height * 0.006f),
            client.Width,
            captionFont.Size * 2f);
        g.DrawString(caption, captionFont, captionBrush, captionBounds, captionFormat);
    }

    /// <summary>底部提示与最近变化时刻。</summary>
    private void PaintFooter(Graphics g, Rectangle frame)
    {
        using var footerFont = new Font("Microsoft YaHei UI", Math.Max(11f, frame.Height * 0.015f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var footerBrush = new SolidBrush(DimText);

        float y = frame.Bottom - footerFont.Size - 10;
        g.DrawString("按 Esc 或双击退出 ｜ F11 切换屏幕", footerFont, footerBrush, frame.Left + 16, y);

        string timestamp = _lastChangedAt is { } time
            ? $"最近变化  {time:HH:mm:ss.fff}"
            : "最近变化  —";

        SizeF size = g.MeasureString(timestamp, footerFont);
        g.DrawString(timestamp, footerFont, footerBrush, frame.Right - 16 - size.Width, y);
    }
}
