using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Spark.Engine.UI;

/// <summary>
/// 字符串级文本渲染器（P3 v1）：把整段文本经 SixLabors 栅格化为白字透明底的 RGBA8 纹理，
/// 以带纹理四边形绘制（着色经 <see cref="UIPrimitive.Color"/> tint）。字形图集与按字形复用留待后续优化。
/// 运行于逻辑线程；新纹理经 <see cref="UIManager.EnqueueTexture"/> 排队，由渲染线程上传。
/// </summary>
public sealed class TextRenderer
{
    private const float Dpi = 72f; // 1pt = 1px，与测量/渲染保持一致

    private readonly Font _font;
    private readonly Dictionary<string, int> _textureIds = new();
    private readonly Dictionary<int, Vector2> _textureSizes = new();
    private int _nextTextureId = 1;

    public TextRenderer(Font font)
    {
        _font = font;
    }

    /// <summary>测量文本的逻辑像素尺寸。</summary>
    public Vector2 Measure(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var size = TextMeasurer.MeasureSize(text, new TextOptions(_font) { Dpi = Dpi });
        return new Vector2(size.Width, size.Height);
    }

    /// <summary>在 <paramref name="position"/>（逻辑像素，左上）绘制着色文本。</summary>
    public void DrawText(UIManager ui, int targetId, string text, Vector2 position, Vector4 color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!_textureIds.TryGetValue(text, out int textureId))
        {
            textureId = CreateTexture(ui, text);
            _textureIds[text] = textureId;
        }

        var size = _textureSizes[textureId];
        ui.Primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Rect = new Vector4(position.X, position.Y, size.X, size.Y),
            UV = new Vector4(0f, 0f, 1f, 1f),
            Color = color,
            TextureId = textureId,
        });
    }

    private int CreateTexture(UIManager ui, string text)
    {
        var options = new RichTextOptions(_font) { Dpi = Dpi, Origin = new PointF(0f, 0f) };
        // MeasureBounds 给出含下伸部(descender)与右侧悬突的实际墨水包围盒；MeasureSize 只给
        // 「前向宽度 × 行高」，用它当纹理尺寸会把底部/右侧的像素裁掉。ceil(Right/Bottom) 覆盖完整包围盒，
        // 再各留 1px 余量，避免抗锯齿边缘被裁。
        var bounds = TextMeasurer.MeasureBounds(text, options);
        int width = System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Right) + 1);
        int height = System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Bottom) + 1);

        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.DrawText(options, text, Color.White));

        var rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);

        int id = _nextTextureId++;
        _textureSizes[id] = new Vector2(width, height);
        ui.EnqueueTexture(new UITextureUpload(id, (uint)width, (uint)height, rgba));
        return id;
    }
}
