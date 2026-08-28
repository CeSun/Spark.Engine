using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using Xunit;
using TextRenderer = Spark.Engine.UI.TextRenderer;

namespace Spark.Engine.Tests;

/// <summary>
/// 文本渲染器的测量一致性回归测试。
/// 锁定修复：Measure 返回的尺寸必须与 CreateTexture 生成的纹理尺寸完全一致，
/// 否则布局分配的高度小于实际绘制高度，垂直排列的文本会重叠。
/// </summary>
public class TextRendererTests
{
    private static TextRenderer CreateRenderer()
    {
        var family = SystemFonts.TryGet("Arial", out var f) ? f : SystemFonts.Families.First();
        return new TextRenderer(family.CreateFont(16f, FontStyle.Regular));
    }

    [Fact]
    public void Measure_Empty_ReturnsZero()
    {
        var renderer = CreateRenderer();
        Assert.Equal(Vector2.Zero, renderer.Measure(string.Empty));
        Assert.Equal(Vector2.Zero, renderer.Measure(null!));
    }

    [Fact]
    public void Measure_NonEmpty_ReturnsPositiveSize()
    {
        var renderer = CreateRenderer();
        var size = renderer.Measure("UIRenderView - Engine View Control");
        Assert.True(size.X > 0f);
        Assert.True(size.Y > 0f);
    }

    [Fact]
    public void Measure_MatchesTextureSize()
    {
        // Measure 必须返回与 CreateTexture 相同的尺寸公式：
        // (ceil(Right-Left)+2, ceil(Bottom-Top)+2)。用 SixLabors 直接计算期望值对照。
        var family = SystemFonts.TryGet("Arial", out var f) ? f : SystemFonts.Families.First();
        var font = family.CreateFont(16f, FontStyle.Regular);
        var renderer = new TextRenderer(font);

        const string text = "UIRenderView - Engine View Control";
        var options = new RichTextOptions(font) { Dpi = 72f, Origin = new PointF(0f, 0f) };
        var bounds = TextMeasurer.MeasureBounds(text, options);

        float expectedW = System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Right - bounds.Left) + 2);
        float expectedH = System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Bottom - bounds.Top) + 2);

        var actual = renderer.Measure(text);
        Assert.Equal(expectedW, actual.X, 2);
        Assert.Equal(expectedH, actual.Y, 2);
    }
}
